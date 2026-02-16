// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Rdn
{
    /// <summary>
    /// Provides a high-performance API for forward-only, read-only access to the UTF-8 encoded RDN text.
    /// It processes the text sequentially with no caching and adheres strictly to the RDN RFC
    /// by default (https://tools.ietf.org/html/rfc8259). When it encounters invalid RDN, it throws
    /// a RdnException with basic error information like line number and byte position on the line.
    /// Since this type is a ref struct, it does not directly support async. However, it does provide
    /// support for reentrancy to read incomplete data, and continue reading once more data is presented.
    /// To be able to set max depth while reading OR allow skipping comments, create an instance of
    /// <see cref="RdnReaderState"/> and pass that in to the reader.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public ref partial struct Utf8RdnReader
    {
        private ReadOnlySpan<byte> _buffer;

        private readonly bool _isFinalBlock;
        private readonly bool _isInputSequence;

        private long _lineNumber;
        private long _bytePositionInLine;

        // bytes consumed in the current segment (not token)
        private int _consumed;
        private bool _inObject;
        private bool _isNotPrimitive;
        private RdnTokenType _tokenType;
        private RdnTokenType _previousTokenType;
        private RdnReaderOptions _readerOptions;
        private BitStack _bitStack;

        // Bit N=1 means depth N is a Set (vs Array). Used to disambiguate } as EndSet vs EndObject
        // when BitStack reports false (non-object). Only tracks depths 0-63; deeper sets always assumed.
        private long _setDepthMask;

        // Bit N=1 means depth N is a Map. Used to disambiguate } as EndMap.
        private long _mapDepthMask;

        // Bit N=1 means the next value at depth N is a map key (expect => after it).
        // Bit N=0 means the next value at depth N is a map value (expect , or } after it).
        private long _mapExpectArrowMask;

        // Bit N=1 means depth N is a Tuple. Tuples emit StartArray/EndArray but close with ')'.
        private long _tupleDepthMask;

        private long _totalConsumed;
        private bool _isLastSegment;
        private readonly bool _isMultiSegment;
        private bool _trailingCommaBeforeComment;

        private SequencePosition _nextPosition;
        private SequencePosition _currentPosition;
        private readonly ReadOnlySequence<byte> _sequence;

        private readonly bool IsLastSpan => _isFinalBlock && (!_isMultiSegment || _isLastSegment);

        internal readonly ReadOnlySequence<byte> OriginalSequence => _sequence;

        internal readonly ReadOnlySpan<byte> OriginalSpan => _sequence.IsEmpty ? _buffer : default;

        internal readonly int ValueLength => HasValueSequence ? checked((int)ValueSequence.Length) : ValueSpan.Length;

        internal readonly bool AllowMultipleValues => _readerOptions.AllowMultipleValues;

        /// <summary>
        /// Gets the value of the last processed token as a ReadOnlySpan&lt;byte&gt; slice
        /// of the input payload. If the RDN is provided within a ReadOnlySequence&lt;byte&gt;
        /// and the slice that represents the token value fits in a single segment, then
        /// <see cref="ValueSpan"/> will contain the sliced value since it can be represented as a span.
        /// Otherwise, the <see cref="ValueSequence"/> will contain the token value.
        /// </summary>
        /// <remarks>
        /// If <see cref="HasValueSequence"/> is true, <see cref="ValueSpan"/> contains useless data, likely for
        /// a previous single-segment token. Therefore, only access <see cref="ValueSpan"/> if <see cref="HasValueSequence"/> is false.
        /// Otherwise, the token value must be accessed from <see cref="ValueSequence"/>.
        /// </remarks>
        public ReadOnlySpan<byte> ValueSpan { get; private set; }

        /// <summary>
        /// Returns the total amount of bytes consumed by the <see cref="Utf8RdnReader"/> so far
        /// for the current instance of the <see cref="Utf8RdnReader"/> with the given UTF-8 encoded input text.
        /// </summary>
        public readonly long BytesConsumed
        {
            get
            {
#if DEBUG
                if (!_isInputSequence)
                {
                    Debug.Assert(_totalConsumed == 0);
                }
#endif
                return _totalConsumed + _consumed;
            }
        }

        /// <summary>
        /// Returns the index that the last processed RDN token starts at
        /// within the given UTF-8 encoded input text, skipping any white space.
        /// </summary>
        /// <remarks>
        /// For RDN strings (including property names), this points to before the start quote.
        /// For comments, this points to before the first comment delimiter (i.e. '/').
        /// </remarks>
        public long TokenStartIndex { get; private set; }

        /// <summary>
        /// Tracks the recursive depth of the nested objects / arrays within the RDN text
        /// processed so far. This provides the depth of the current token.
        /// </summary>
        public readonly int CurrentDepth
        {
            get
            {
                int readerDepth = _bitStack.CurrentDepth;
                if (TokenType is RdnTokenType.StartArray or RdnTokenType.StartObject or RdnTokenType.StartSet or RdnTokenType.StartMap)
                {
                    Debug.Assert(readerDepth >= 1);
                    readerDepth--;
                }
                return readerDepth;
            }
        }

        internal readonly bool IsInArray => !_inObject;

        /// <summary>
        /// Gets the type of the last processed RDN token in the UTF-8 encoded RDN text.
        /// </summary>
        public readonly RdnTokenType TokenType => _tokenType;

        /// <summary>
        /// Lets the caller know which of the two 'Value' properties to read to get the
        /// token value. For input data within a ReadOnlySpan&lt;byte&gt; this will
        /// always return false. For input data within a ReadOnlySequence&lt;byte&gt;, this
        /// will only return true if the token value straddles more than a single segment and
        /// hence couldn't be represented as a span.
        /// </summary>
        public bool HasValueSequence { get; private set; }

        /// <summary>
        /// Lets the caller know whether the current <see cref="ValueSpan" /> or <see cref="ValueSequence"/> properties
        /// contain escape sequences per RFC 8259 section 7, and therefore require unescaping before being consumed.
        /// </summary>
        public bool ValueIsEscaped { get; private set; }

        /// <summary>
        /// Returns the mode of this instance of the <see cref="Utf8RdnReader"/>.
        /// True when the reader was constructed with the input span containing the entire data to process.
        /// False when the reader was constructed knowing that the input span may contain partial data with more data to follow.
        /// </summary>
        public readonly bool IsFinalBlock => _isFinalBlock;

        /// <summary>
        /// Gets the value of the last processed token as a ReadOnlySpan&lt;byte&gt; slice
        /// of the input payload. If the RDN is provided within a ReadOnlySequence&lt;byte&gt;
        /// and the slice that represents the token value fits in a single segment, then
        /// <see cref="ValueSpan"/> will contain the sliced value since it can be represented as a span.
        /// Otherwise, the <see cref="ValueSequence"/> will contain the token value.
        /// </summary>
        /// <remarks>
        /// If <see cref="HasValueSequence"/> is false, <see cref="ValueSequence"/> contains useless data, likely for
        /// a previous multi-segment token. Therefore, only access <see cref="ValueSequence"/> if <see cref="HasValueSequence"/> is true.
        /// Otherwise, the token value must be accessed from <see cref="ValueSpan"/>.
        /// </remarks>
        public ReadOnlySequence<byte> ValueSequence { get; private set; }

        /// <summary>
        /// Returns the current <see cref="SequencePosition"/> within the provided UTF-8 encoded
        /// input ReadOnlySequence&lt;byte&gt;. If the <see cref="Utf8RdnReader"/> was constructed
        /// with a ReadOnlySpan&lt;byte&gt; instead, this will always return a default <see cref="SequencePosition"/>.
        /// </summary>
        public readonly SequencePosition Position
        {
            get
            {
                if (_isInputSequence)
                {
                    Debug.Assert(_currentPosition.GetObject() != null);
                    return _sequence.GetPosition(_consumed, _currentPosition);
                }
                return default;
            }
        }

        /// <summary>
        /// Returns the current snapshot of the <see cref="Utf8RdnReader"/> state which must
        /// be captured by the caller and passed back in to the <see cref="Utf8RdnReader"/> ctor with more data.
        /// Unlike the <see cref="Utf8RdnReader"/>, which is a ref struct, the state can survive
        /// across async/await boundaries and hence this type is required to provide support for reading
        /// in more data asynchronously before continuing with a new instance of the <see cref="Utf8RdnReader"/>.
        /// </summary>
        public readonly RdnReaderState CurrentState => new RdnReaderState
        (
            lineNumber: _lineNumber,
            bytePositionInLine: _bytePositionInLine,
            inObject: _inObject,
            isNotPrimitive: _isNotPrimitive,
            valueIsEscaped: ValueIsEscaped,
            trailingCommaBeforeComment: _trailingCommaBeforeComment,
            tokenType: _tokenType,
            previousTokenType: _previousTokenType,
            readerOptions: _readerOptions,
            bitStack: _bitStack
        );

        /// <summary>
        /// Constructs a new <see cref="Utf8RdnReader"/> instance.
        /// </summary>
        /// <param name="rdnData">The ReadOnlySpan&lt;byte&gt; containing the UTF-8 encoded RDN text to process.</param>
        /// <param name="isFinalBlock">True when the input span contains the entire data to process.
        /// Set to false only if it is known that the input span contains partial data with more data to follow.</param>
        /// <param name="state">If this is the first call to the ctor, pass in a default state. Otherwise,
        /// capture the state from the previous instance of the <see cref="Utf8RdnReader"/> and pass that back.</param>
        /// <remarks>
        /// Since this type is a ref struct, it is a stack-only type and all the limitations of ref structs apply to it.
        /// This is the reason why the ctor accepts a <see cref="RdnReaderState"/>.
        /// </remarks>
        public Utf8RdnReader(ReadOnlySpan<byte> rdnData, bool isFinalBlock, RdnReaderState state)
        {
            _buffer = rdnData;

            _isFinalBlock = isFinalBlock;
            _isInputSequence = false;

            _lineNumber = state._lineNumber;
            _bytePositionInLine = state._bytePositionInLine;
            _inObject = state._inObject;
            _isNotPrimitive = state._isNotPrimitive;
            ValueIsEscaped = state._valueIsEscaped;
            _trailingCommaBeforeComment = state._trailingCommaBeforeComment;
            _tokenType = state._tokenType;
            _previousTokenType = state._previousTokenType;
            _readerOptions = state._readerOptions;
            if (_readerOptions.MaxDepth == 0)
            {
                _readerOptions.MaxDepth = RdnReaderOptions.DefaultMaxDepth;  // If max depth is not set, revert to the default depth.
            }
            _bitStack = state._bitStack;

            _consumed = 0;
            TokenStartIndex = 0;
            _totalConsumed = 0;
            _isLastSegment = _isFinalBlock;
            _isMultiSegment = false;

            ValueSpan = ReadOnlySpan<byte>.Empty;

            _currentPosition = default;
            _nextPosition = default;
            _sequence = default;
            HasValueSequence = false;
            ValueSequence = ReadOnlySequence<byte>.Empty;
        }

        /// <summary>
        /// Constructs a new <see cref="Utf8RdnReader"/> instance.
        /// </summary>
        /// <param name="rdnData">The ReadOnlySpan&lt;byte&gt; containing the UTF-8 encoded RDN text to process.</param>
        /// <param name="options">Defines the customized behavior of the <see cref="Utf8RdnReader"/>
        /// that is different from the RDN RFC (for example how to handle comments or maximum depth allowed when reading).
        /// By default, the <see cref="Utf8RdnReader"/> follows the RDN RFC strictly (i.e. comments within the RDN are invalid) and reads up to a maximum depth of 64.</param>
        /// <remarks>
        ///   <para>
        ///     Since this type is a ref struct, it is a stack-only type and all the limitations of ref structs apply to it.
        ///   </para>
        ///   <para>
        ///     This assumes that the entire RDN payload is passed in (equivalent to <see cref="IsFinalBlock"/> = true)
        ///   </para>
        /// </remarks>
        public Utf8RdnReader(ReadOnlySpan<byte> rdnData, RdnReaderOptions options = default)
            : this(rdnData, isFinalBlock: true, new RdnReaderState(options))
        {
        }

        /// <summary>
        /// Read the next RDN token from input source.
        /// </summary>
        /// <returns>True if the token was read successfully, else false.</returns>
        /// <exception cref="RdnException">
        /// Thrown when an invalid RDN token is encountered according to the RDN RFC
        /// or if the current depth exceeds the recursive limit set by the max depth.
        /// </exception>
        public bool Read()
        {
            bool retVal = _isMultiSegment ? ReadMultiSegment() : ReadSingleSegment();

            if (!retVal)
            {
                if (_isFinalBlock && TokenType is RdnTokenType.None && !_readerOptions.AllowMultipleValues)
                {
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedRdnTokens);
                }
            }
            return retVal;
        }

        /// <summary>
        /// Skips the children of the current RDN token.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the reader was given partial data with more data to follow (i.e. <see cref="IsFinalBlock"/> is false).
        /// </exception>
        /// <exception cref="RdnException">
        /// Thrown when an invalid RDN token is encountered while skipping, according to the RDN RFC,
        /// or if the current depth exceeds the recursive limit set by the max depth.
        /// </exception>
        /// <remarks>
        /// When <see cref="TokenType"/> is <see cref="RdnTokenType.PropertyName" />, the reader first moves to the property value.
        /// When <see cref="TokenType"/> (originally, or after advancing) is <see cref="RdnTokenType.StartObject" /> or
        /// <see cref="RdnTokenType.StartArray" />, the reader advances to the matching
        /// <see cref="RdnTokenType.EndObject" /> or <see cref="RdnTokenType.EndArray" />.
        ///
        /// For all other token types, the reader does not move. After the next call to <see cref="Read"/>, the reader will be at
        /// the next value (when in an array), the next property name (when in an object), or the end array/object token.
        /// </remarks>
        public void Skip()
        {
            if (!_isFinalBlock)
            {
                ThrowHelper.ThrowInvalidOperationException_CannotSkipOnPartial();
            }

            SkipHelper();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipHelper()
        {
            Debug.Assert(_isFinalBlock);

            if (TokenType is RdnTokenType.PropertyName)
            {
                bool result = Read();
                // Since _isFinalBlock == true here, and the RDN token is not a primitive value or comment.
                // Read() is guaranteed to return true OR throw for invalid/incomplete data.
                Debug.Assert(result);
            }

            if (TokenType is RdnTokenType.StartObject or RdnTokenType.StartArray or RdnTokenType.StartSet or RdnTokenType.StartMap)
            {
                int depth = CurrentDepth;
                do
                {
                    bool result = Read();
                    // Since _isFinalBlock == true here, and the RDN token is not a primitive value or comment.
                    // Read() is guaranteed to return true OR throw for invalid/incomplete data.
                    Debug.Assert(result);
                }
                while (depth < CurrentDepth);
            }
        }

        /// <summary>
        /// Tries to skip the children of the current RDN token.
        /// </summary>
        /// <returns>True if there was enough data for the children to be skipped successfully, else false.</returns>
        /// <exception cref="RdnException">
        /// Thrown when an invalid RDN token is encountered while skipping, according to the RDN RFC,
        /// or if the current depth exceeds the recursive limit set by the max depth.
        /// </exception>
        /// <remarks>
        ///   <para>
        ///     If the reader did not have enough data to completely skip the children of the current token,
        ///     it will be reset to the state it was in before the method was called.
        ///   </para>
        ///   <para>
        ///     When <see cref="TokenType"/> is <see cref="RdnTokenType.PropertyName" />, the reader first moves to the property value.
        ///     When <see cref="TokenType"/> (originally, or after advancing) is <see cref="RdnTokenType.StartObject" /> or
        ///     <see cref="RdnTokenType.StartArray" />, the reader advances to the matching
        ///     <see cref="RdnTokenType.EndObject" /> or <see cref="RdnTokenType.EndArray" />.
        ///
        ///     For all other token types, the reader does not move. After the next call to <see cref="Read"/>, the reader will be at
        ///     the next value (when in an array), the next property name (when in an object), or the end array/object token.
        ///   </para>
        /// </remarks>
        public bool TrySkip()
        {
            if (_isFinalBlock)
            {
                SkipHelper();
                return true;
            }

            Utf8RdnReader restore = this;
            bool success = TrySkipPartial(targetDepth: CurrentDepth);
            if (!success)
            {
                // Roll back the reader if it contains partial data.
                this = restore;
            }

            return success;
        }

        /// <summary>
        /// Tries to skip the children of the current RDN token, advancing the reader even if there is not enough data.
        /// The skip operation can be resumed later, provided that the same <paramref name="targetDepth" /> is passed.
        /// </summary>
        /// <param name="targetDepth">The target depth we want to eventually skip to.</param>
        /// <returns>True if the entire RDN value has been skipped.</returns>
        internal bool TrySkipPartial(int targetDepth)
        {
            Debug.Assert(0 <= targetDepth && targetDepth <= CurrentDepth);

            if (targetDepth == CurrentDepth)
            {
                // This is the first call to TrySkipHelper.
                if (TokenType is RdnTokenType.PropertyName)
                {
                    // Skip any property name tokens preceding the value.
                    if (!Read())
                    {
                        return false;
                    }
                }

                if (TokenType is not (RdnTokenType.StartObject or RdnTokenType.StartArray or RdnTokenType.StartSet or RdnTokenType.StartMap))
                {
                    // The next value is not an object, array, or set, so there is nothing to skip.
                    return true;
                }
            }

            // Start or resume iterating through the RDN object or array.
            do
            {
                if (!Read())
                {
                    return false;
                }
            }
            while (targetDepth < CurrentDepth);

            Debug.Assert(targetDepth == CurrentDepth);
            return true;
        }

        /// <summary>
        /// Compares the UTF-8 encoded text to the unescaped RDN token value in the source and returns true if they match.
        /// </summary>
        /// <param name="utf8Text">The UTF-8 encoded text to compare against.</param>
        /// <returns>True if the RDN token value in the source matches the UTF-8 encoded look up text.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to find a text match on a RDN token that is not a string
        /// (i.e. other than <see cref="RdnTokenType.String"/> or <see cref="RdnTokenType.PropertyName"/>).
        /// <seealso cref="TokenType" />
        /// </exception>
        /// <remarks>
        ///   <para>
        ///     If the look up text is invalid UTF-8 text, the method will return false since you cannot have
        ///     invalid UTF-8 within the RDN payload.
        ///   </para>
        ///   <para>
        ///     The comparison of the RDN token value in the source and the look up text is done by first unescaping the RDN value in source,
        ///     if required. The look up text is matched as is, without any modifications to it.
        ///   </para>
        /// </remarks>
        public readonly bool ValueTextEquals(ReadOnlySpan<byte> utf8Text)
        {
            if (!IsTokenTypeString(TokenType))
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedStringComparison(TokenType);
            }

            return TextEqualsHelper(utf8Text);
        }

        /// <summary>
        /// Compares the string text to the unescaped RDN token value in the source and returns true if they match.
        /// </summary>
        /// <param name="text">The text to compare against.</param>
        /// <returns>True if the RDN token value in the source matches the look up text.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to find a text match on a RDN token that is not a string
        /// (i.e. other than <see cref="RdnTokenType.String"/> or <see cref="RdnTokenType.PropertyName"/>).
        /// <seealso cref="TokenType" />
        /// </exception>
        /// <remarks>
        ///   <para>
        ///     If the look up text is invalid UTF-8 text, the method will return false since you cannot have
        ///     invalid UTF-8 within the RDN payload.
        ///   </para>
        ///   <para>
        ///     The comparison of the RDN token value in the source and the look up text is done by first unescaping the RDN value in source,
        ///     if required. The look up text is matched as is, without any modifications to it.
        ///   </para>
        /// </remarks>
        public readonly bool ValueTextEquals(string? text)
        {
            return ValueTextEquals(text.AsSpan());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly bool TextEqualsHelper(ReadOnlySpan<byte> otherUtf8Text)
        {
            if (HasValueSequence)
            {
                return CompareToSequence(otherUtf8Text);
            }

            if (ValueIsEscaped)
            {
                return UnescapeAndCompare(otherUtf8Text);
            }

            return otherUtf8Text.SequenceEqual(ValueSpan);
        }

        /// <summary>
        /// Compares the text to the unescaped RDN token value in the source and returns true if they match.
        /// </summary>
        /// <param name="text">The text to compare against.</param>
        /// <returns>True if the RDN token value in the source matches the look up text.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to find a text match on a RDN token that is not a string
        /// (i.e. other than <see cref="RdnTokenType.String"/> or <see cref="RdnTokenType.PropertyName"/>).
        /// <seealso cref="TokenType" />
        /// </exception>
        /// <remarks>
        ///   <para>
        ///     If the look up text is invalid or incomplete UTF-16 text (i.e. unpaired surrogates), the method will return false
        ///     since you cannot have invalid UTF-16 within the RDN payload.
        ///   </para>
        ///   <para>
        ///     The comparison of the RDN token value in the source and the look up text is done by first unescaping the RDN value in source,
        ///     if required. The look up text is matched as is, without any modifications to it.
        ///   </para>
        /// </remarks>
        public readonly bool ValueTextEquals(ReadOnlySpan<char> text)
        {
            if (!IsTokenTypeString(TokenType))
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedStringComparison(TokenType);
            }

            if (MatchNotPossible(text.Length))
            {
                return false;
            }

            byte[]? otherUtf8TextArray = null;

            scoped Span<byte> otherUtf8Text;

            int length = checked(text.Length * RdnConstants.MaxExpansionFactorWhileTranscoding);

            if (length > RdnConstants.StackallocByteThreshold)
            {
                otherUtf8TextArray = ArrayPool<byte>.Shared.Rent(length);
                otherUtf8Text = otherUtf8TextArray;
            }
            else
            {
                otherUtf8Text = stackalloc byte[RdnConstants.StackallocByteThreshold];
            }

            OperationStatus status = RdnWriterHelper.ToUtf8(text, otherUtf8Text, out int written);
            Debug.Assert(status != OperationStatus.DestinationTooSmall);
            bool result;
            if (status == OperationStatus.InvalidData)
            {
                result = false;
            }
            else
            {
                Debug.Assert(status == OperationStatus.Done);
                result = TextEqualsHelper(otherUtf8Text.Slice(0, written));
            }

            if (otherUtf8TextArray != null)
            {
                otherUtf8Text.Slice(0, written).Clear();
                ArrayPool<byte>.Shared.Return(otherUtf8TextArray);
            }

            return result;
        }

        private readonly bool CompareToSequence(ReadOnlySpan<byte> other)
        {
            Debug.Assert(HasValueSequence);

            if (ValueIsEscaped)
            {
                return UnescapeSequenceAndCompare(other);
            }

            ReadOnlySequence<byte> localSequence = ValueSequence;

            Debug.Assert(!localSequence.IsSingleSegment);

            if (localSequence.Length != other.Length)
            {
                return false;
            }

            int matchedSoFar = 0;

            foreach (ReadOnlyMemory<byte> memory in localSequence)
            {
                ReadOnlySpan<byte> span = memory.Span;

                if (other.Slice(matchedSoFar).StartsWith(span))
                {
                    matchedSoFar += span.Length;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        private readonly bool UnescapeAndCompare(ReadOnlySpan<byte> other)
        {
            Debug.Assert(!HasValueSequence);
            ReadOnlySpan<byte> localSpan = ValueSpan;

            if (localSpan.Length < other.Length || localSpan.Length / RdnConstants.MaxExpansionFactorWhileEscaping > other.Length)
            {
                return false;
            }

            int idx = localSpan.IndexOf(RdnConstants.BackSlash);
            Debug.Assert(idx != -1);

            if (!other.StartsWith(localSpan.Slice(0, idx)))
            {
                return false;
            }

            return RdnReaderHelper.UnescapeAndCompare(localSpan.Slice(idx), other.Slice(idx));
        }

        private readonly bool UnescapeSequenceAndCompare(ReadOnlySpan<byte> other)
        {
            Debug.Assert(HasValueSequence);
            Debug.Assert(!ValueSequence.IsSingleSegment);

            ReadOnlySequence<byte> localSequence = ValueSequence;
            long sequenceLength = localSequence.Length;

            // The RDN token value will at most shrink by 6 when unescaping.
            // If it is still larger than the lookup string, there is no value in unescaping and doing the comparison.
            if (sequenceLength < other.Length || sequenceLength / RdnConstants.MaxExpansionFactorWhileEscaping > other.Length)
            {
                return false;
            }

            int matchedSoFar = 0;

            bool result = false;

            foreach (ReadOnlyMemory<byte> memory in localSequence)
            {
                ReadOnlySpan<byte> span = memory.Span;

                int idx = span.IndexOf(RdnConstants.BackSlash);

                if (idx != -1)
                {
                    if (!other.Slice(matchedSoFar).StartsWith(span.Slice(0, idx)))
                    {
                        break;
                    }
                    matchedSoFar += idx;

                    other = other.Slice(matchedSoFar);
                    localSequence = localSequence.Slice(matchedSoFar);

                    if (localSequence.IsSingleSegment)
                    {
                        result = RdnReaderHelper.UnescapeAndCompare(localSequence.First.Span, other);
                    }
                    else
                    {
                        result = RdnReaderHelper.UnescapeAndCompare(localSequence, other);
                    }
                    break;
                }

                if (!other.Slice(matchedSoFar).StartsWith(span))
                {
                    break;
                }
                matchedSoFar += span.Length;
            }

            return result;
        }

        // Returns true if the TokenType is a primitive string "value", i.e. PropertyName or String
        // Otherwise, return false.
        private static bool IsTokenTypeString(RdnTokenType tokenType)
        {
            return tokenType == RdnTokenType.PropertyName || tokenType == RdnTokenType.String;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly bool MatchNotPossible(int charTextLength)
        {
            if (HasValueSequence)
            {
                return MatchNotPossibleSequence(charTextLength);
            }

            int sourceLength = ValueSpan.Length;

            // Transcoding from UTF-16 to UTF-8 will change the length by somwhere between 1x and 3x.
            // Unescaping the token value will at most shrink its length by 6x.
            // There is no point incurring the transcoding/unescaping/comparing cost if:
            // - The token value is smaller than charTextLength
            // - The token value needs to be transcoded AND unescaped and it is more than 6x larger than charTextLength
            //      - For an ASCII UTF-16 characters, transcoding = 1x, escaping = 6x => 6x factor
            //      - For non-ASCII UTF-16 characters within the BMP, transcoding = 2-3x, but they are represented as a single escaped hex value, \uXXXX => 6x factor
            //      - For non-ASCII UTF-16 characters outside of the BMP, transcoding = 4x, but the surrogate pair (2 characters) are represented by 16 bytes \uXXXX\uXXXX => 6x factor
            // - The token value needs to be transcoded, but NOT escaped and it is more than 3x larger than charTextLength
            //      - For an ASCII UTF-16 characters, transcoding = 1x,
            //      - For non-ASCII UTF-16 characters within the BMP, transcoding = 2-3x,
            //      - For non-ASCII UTF-16 characters outside of the BMP, transcoding = 2x, (surrogate pairs - 2 characters transcode to 4 UTF-8 bytes)

            if (sourceLength < charTextLength
                || sourceLength / (ValueIsEscaped ? RdnConstants.MaxExpansionFactorWhileEscaping : RdnConstants.MaxExpansionFactorWhileTranscoding) > charTextLength)
            {
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private readonly bool MatchNotPossibleSequence(int charTextLength)
        {
            long sourceLength = ValueSequence.Length;

            if (sourceLength < charTextLength
                || sourceLength / (ValueIsEscaped ? RdnConstants.MaxExpansionFactorWhileEscaping : RdnConstants.MaxExpansionFactorWhileTranscoding) > charTextLength)
            {
                return true;
            }
            return false;
        }

        private void StartObject()
        {
            if (_bitStack.CurrentDepth >= _readerOptions.MaxDepth)
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ObjectDepthTooLarge);

            _bitStack.PushTrue();

            ValueSpan = _buffer.Slice(_consumed, 1);
            _consumed++;
            _bytePositionInLine++;
            _tokenType = RdnTokenType.StartObject;
            _inObject = true;
        }

        private void EndObject()
        {
            if (!_inObject || _bitStack.CurrentDepth <= 0)
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.MismatchedObjectArray, RdnConstants.CloseBrace);

            if (_trailingCommaBeforeComment)
            {
                if (!_readerOptions.AllowTrailingCommas)
                {
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeObjectEnd);
                }
                _trailingCommaBeforeComment = false;
            }

            _tokenType = RdnTokenType.EndObject;
            ValueSpan = _buffer.Slice(_consumed, 1);

            UpdateBitStackOnEndToken();
        }

        private void StartArray()
        {
            if (_bitStack.CurrentDepth >= _readerOptions.MaxDepth)
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ArrayDepthTooLarge);

            _bitStack.PushFalse();

            ValueSpan = _buffer.Slice(_consumed, 1);
            _consumed++;
            _bytePositionInLine++;
            _tokenType = RdnTokenType.StartArray;
            _inObject = false;
        }

        private void EndArray()
        {
            if (_inObject || _bitStack.CurrentDepth <= 0 || IsCurrentDepthSet() || IsCurrentDepthMap() || IsCurrentDepthTuple())
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.MismatchedObjectArray, RdnConstants.CloseBracket);

            if (_trailingCommaBeforeComment)
            {
                if (!_readerOptions.AllowTrailingCommas)
                {
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeArrayEnd);
                }
                _trailingCommaBeforeComment = false;
            }

            _tokenType = RdnTokenType.EndArray;
            ValueSpan = _buffer.Slice(_consumed, 1);

            UpdateBitStackOnEndToken();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateBitStackOnEndToken()
        {
            _consumed++;
            _bytePositionInLine++;
            _inObject = _bitStack.Pop();
        }

        /// <summary>
        /// Disambiguates '{' — determines whether it starts an Object or an implicit Set.
        /// Empty {} → Object. First value is non-string → Set. First value is string → look ahead for separator.
        /// </summary>
        private void ConsumeBrace()
        {
            ReadOnlySpan<byte> localBuffer = _buffer;
            int pos = _consumed + 1; // skip past '{'

            // Skip whitespace after '{'
            while (pos < localBuffer.Length && localBuffer[pos] is RdnConstants.Space or RdnConstants.CarriageReturn or RdnConstants.LineFeed or RdnConstants.Tab)
            {
                pos++;
            }

            if (pos >= localBuffer.Length)
            {
                // Not enough data to disambiguate — default to Object (safe for incomplete data)
                StartObject();
                return;
            }

            byte peek = localBuffer[pos];

            // Empty {} → Object
            if (peek == RdnConstants.CloseBrace)
            {
                StartObject();
                return;
            }

            // Non-string first value → need to scan past it to check for => (Map) vs , or } (Set)
            if (peek != RdnConstants.Quote)
            {
                int afterValue = ScanPastNonStringValue(localBuffer, pos);
                if (afterValue < 0)
                {
                    // Couldn't determine — default to Set
                    StartSet();
                    return;
                }

                // Skip whitespace after the value
                while (afterValue < localBuffer.Length && localBuffer[afterValue] is RdnConstants.Space or RdnConstants.CarriageReturn or RdnConstants.LineFeed or RdnConstants.Tab)
                {
                    afterValue++;
                }

                if (afterValue < localBuffer.Length && localBuffer[afterValue] == RdnConstants.Equals)
                {
                    StartMap();
                }
                else
                {
                    StartSet();
                }
                return;
            }

            // First value is a string — need to scan past it and check separator
            int endQuote = ScanPastRdnString(localBuffer, pos);
            if (endQuote < 0)
            {
                // Couldn't find end of string — default to Object
                StartObject();
                return;
            }

            // Skip whitespace after the string
            int afterString = endQuote + 1;
            while (afterString < localBuffer.Length && localBuffer[afterString] is RdnConstants.Space or RdnConstants.CarriageReturn or RdnConstants.LineFeed or RdnConstants.Tab)
            {
                afterString++;
            }

            if (afterString >= localBuffer.Length)
            {
                // Not enough data — default to Object
                StartObject();
                return;
            }

            byte separator = localBuffer[afterString];
            if (separator == RdnConstants.KeyValueSeparator)
            {
                // "key": → Object
                StartObject();
            }
            else if (separator == RdnConstants.ListSeparator || separator == RdnConstants.CloseBrace)
            {
                // "val", or "val"} → Set
                StartSet();
            }
            else if (separator == RdnConstants.Equals)
            {
                // "key"=> → Map
                StartMap();
            }
            else
            {
                StartObject();
            }
        }

        /// <summary>
        /// Lightweight scan to find the closing quote of a RDN string starting at position pos.
        /// Returns the index of the closing quote, or -1 if not found.
        /// Does not validate — just skips backslash-escapes.
        /// </summary>
        private static int ScanPastRdnString(ReadOnlySpan<byte> buffer, int pos)
        {
            Debug.Assert(buffer[pos] == RdnConstants.Quote);
            pos++; // skip opening quote

            while (pos < buffer.Length)
            {
                byte b = buffer[pos];
                if (b == RdnConstants.Quote)
                {
                    return pos;
                }
                if (b == RdnConstants.BackSlash)
                {
                    pos++; // skip escaped character
                }
                pos++;
            }

            return -1; // not found
        }

        private void StartSet()
        {
            if (_bitStack.CurrentDepth >= _readerOptions.MaxDepth)
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ArrayDepthTooLarge);

            _bitStack.PushFalse(); // Sets push false like arrays

            // Mark this depth as a Set
            int depth = _bitStack.CurrentDepth;
            if (depth < 64)
            {
                _setDepthMask |= (1L << depth);
            }

            ValueSpan = _buffer.Slice(_consumed, 1);
            _consumed++;
            _bytePositionInLine++;
            _tokenType = RdnTokenType.StartSet;
            _inObject = false;
        }

        private bool ConsumeExplicitSet()
        {
            ReadOnlySpan<byte> span = _buffer.Slice(_consumed);
            if (span.Length < 4)
            {
                if (IsLastSpan)
                {
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, span[0]);
                }
                return false;
            }

            if (span[0] != (byte)'S' || span[1] != (byte)'e' || span[2] != (byte)'t' || span[3] != RdnConstants.OpenBrace)
            {
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, span[0]);
            }

            StartExplicitSet();
            return true;
        }

        private void StartExplicitSet()
        {
            if (_bitStack.CurrentDepth >= _readerOptions.MaxDepth)
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ArrayDepthTooLarge);

            _bitStack.PushFalse();

            int depth = _bitStack.CurrentDepth;
            if (depth < 64)
            {
                _setDepthMask |= (1L << depth);
            }

            ValueSpan = _buffer.Slice(_consumed, 4); // "Set{"
            _consumed += 4;
            _bytePositionInLine += 4;
            _tokenType = RdnTokenType.StartSet;
            _inObject = false;
        }

        private void EndSet()
        {
            if (_inObject || _bitStack.CurrentDepth <= 0)
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.MismatchedObjectArray, RdnConstants.CloseBrace);

            if (_trailingCommaBeforeComment)
            {
                if (!_readerOptions.AllowTrailingCommas)
                {
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeArrayEnd);
                }
                _trailingCommaBeforeComment = false;
            }

            // Clear the set depth bit
            int depth = _bitStack.CurrentDepth;
            if (depth < 64)
            {
                _setDepthMask &= ~(1L << depth);
            }

            _tokenType = RdnTokenType.EndSet;
            ValueSpan = _buffer.Slice(_consumed, 1);

            UpdateBitStackOnEndToken();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly bool IsCurrentDepthSet()
        {
            int depth = _bitStack.CurrentDepth;
            return depth < 64 ? (_setDepthMask & (1L << depth)) != 0 : false;
        }

        private void StartMap()
        {
            if (_bitStack.CurrentDepth >= _readerOptions.MaxDepth)
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ArrayDepthTooLarge);

            _bitStack.PushFalse(); // Maps push false like arrays/sets

            int depth = _bitStack.CurrentDepth;
            if (depth < 64)
            {
                _mapDepthMask |= (1L << depth);
                _mapExpectArrowMask |= (1L << depth); // First value is a key
            }

            ValueSpan = _buffer.Slice(_consumed, 1);
            _consumed++;
            _bytePositionInLine++;
            _tokenType = RdnTokenType.StartMap;
            _inObject = false;
        }

        private bool ConsumeExplicitMap()
        {
            ReadOnlySpan<byte> span = _buffer.Slice(_consumed);
            if (span.Length < 4)
            {
                if (IsLastSpan)
                {
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, span[0]);
                }
                return false;
            }

            if (span[0] != (byte)'M' || span[1] != (byte)'a' || span[2] != (byte)'p' || span[3] != RdnConstants.OpenBrace)
            {
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, span[0]);
            }

            StartExplicitMap();
            return true;
        }

        private void StartExplicitMap()
        {
            if (_bitStack.CurrentDepth >= _readerOptions.MaxDepth)
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ArrayDepthTooLarge);

            _bitStack.PushFalse();

            int depth = _bitStack.CurrentDepth;
            if (depth < 64)
            {
                _mapDepthMask |= (1L << depth);
                _mapExpectArrowMask |= (1L << depth); // First value is a key
            }

            ValueSpan = _buffer.Slice(_consumed, 4); // "Map{"
            _consumed += 4;
            _bytePositionInLine += 4;
            _tokenType = RdnTokenType.StartMap;
            _inObject = false;
        }

        private void EndMap()
        {
            if (_inObject || _bitStack.CurrentDepth <= 0)
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.MismatchedObjectArray, RdnConstants.CloseBrace);

            if (_trailingCommaBeforeComment)
            {
                if (!_readerOptions.AllowTrailingCommas)
                {
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeArrayEnd);
                }
                _trailingCommaBeforeComment = false;
            }

            int depth = _bitStack.CurrentDepth;
            if (depth < 64)
            {
                _mapDepthMask &= ~(1L << depth);
                _mapExpectArrowMask &= ~(1L << depth);
            }

            _tokenType = RdnTokenType.EndMap;
            ValueSpan = _buffer.Slice(_consumed, 1);

            UpdateBitStackOnEndToken();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly bool IsCurrentDepthMap()
        {
            int depth = _bitStack.CurrentDepth;
            return depth < 64 ? (_mapDepthMask & (1L << depth)) != 0 : false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly bool IsMapExpectingArrow()
        {
            int depth = _bitStack.CurrentDepth;
            return depth < 64 ? (_mapExpectArrowMask & (1L << depth)) != 0 : false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetMapArrowExpect()
        {
            int depth = _bitStack.CurrentDepth;
            if (depth < 64)
            {
                _mapExpectArrowMask |= (1L << depth);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ClearMapArrowExpect()
        {
            int depth = _bitStack.CurrentDepth;
            if (depth < 64)
            {
                _mapExpectArrowMask &= ~(1L << depth);
            }
        }

        private void StartTuple()
        {
            if (_bitStack.CurrentDepth >= _readerOptions.MaxDepth)
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ArrayDepthTooLarge);

            _bitStack.PushFalse();

            int depth = _bitStack.CurrentDepth;
            if (depth < 64)
            {
                _tupleDepthMask |= (1L << depth);
            }

            ValueSpan = _buffer.Slice(_consumed, 1);
            _consumed++;
            _bytePositionInLine++;
            _tokenType = RdnTokenType.StartArray; // Tuples emit StartArray
            _inObject = false;
        }

        private void EndTuple()
        {
            if (_inObject || _bitStack.CurrentDepth <= 0 || !IsCurrentDepthTuple())
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.MismatchedObjectArray, RdnConstants.CloseParen);

            if (_trailingCommaBeforeComment)
            {
                if (!_readerOptions.AllowTrailingCommas)
                {
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeArrayEnd);
                }
                _trailingCommaBeforeComment = false;
            }

            int depth = _bitStack.CurrentDepth;
            if (depth < 64)
            {
                _tupleDepthMask &= ~(1L << depth);
            }

            _tokenType = RdnTokenType.EndArray; // Tuples emit EndArray
            ValueSpan = _buffer.Slice(_consumed, 1);

            UpdateBitStackOnEndToken();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly bool IsCurrentDepthTuple()
        {
            int depth = _bitStack.CurrentDepth;
            return depth < 64 ? (_tupleDepthMask & (1L << depth)) != 0 : false;
        }

        /// <summary>
        /// Consumes the => arrow separator in a Map. Expects current position at '='.
        /// </summary>
        private bool ConsumeMapArrow()
        {
            ReadOnlySpan<byte> localBuffer = _buffer;
            if (_consumed + 1 >= localBuffer.Length)
            {
                if (IsLastSpan)
                {
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, localBuffer[_consumed]);
                }
                return false;
            }

            if (localBuffer[_consumed] != RdnConstants.Equals || localBuffer[_consumed + 1] != RdnConstants.GreaterThan)
            {
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, localBuffer[_consumed]);
            }

            _consumed += 2;
            _bytePositionInLine += 2;
            return true;
        }

        /// <summary>
        /// Lightweight scan to find the byte position after a non-string value.
        /// Returns the index of the first byte after the value, or -1 if not determinable.
        /// Used for brace disambiguation when the first value in {} is not a string.
        /// </summary>
        private static int ScanPastNonStringValue(ReadOnlySpan<byte> buffer, int pos)
        {
            if (pos >= buffer.Length)
                return -1;

            byte first = buffer[pos];

            // NaN (3), Infinity (8)
            if (first == (byte)'N') return pos + 3 <= buffer.Length ? pos + 3 : -1;
            if (first == (byte)'I') return pos + 8 <= buffer.Length ? pos + 8 : -1;

            // Numbers: scan digits, -, +, ., e, E (also check for -Infinity)
            if (RdnHelpers.IsDigit(first) || first == (byte)'-')
            {
                // Check for -Infinity
                if (first == (byte)'-' && pos + 1 < buffer.Length && buffer[pos + 1] == (byte)'I')
                {
                    return pos + 9 <= buffer.Length ? pos + 9 : -1;
                }
                pos++;
                while (pos < buffer.Length)
                {
                    byte b = buffer[pos];
                    if (RdnHelpers.IsDigit(b) || b == (byte)'.' || b == (byte)'e' || b == (byte)'E' || b == (byte)'+' || b == (byte)'-')
                    {
                        pos++;
                    }
                    else
                    {
                        break;
                    }
                }
                return pos;
            }

            // true (4), false (5), null (4)
            if (first == (byte)'t') return pos + 4 <= buffer.Length ? pos + 4 : -1;
            if (first == (byte)'f') return pos + 5 <= buffer.Length ? pos + 5 : -1;
            if (first == (byte)'n') return pos + 4 <= buffer.Length ? pos + 4 : -1;

            // @-prefixed RDN literals: scan until delimiter
            if (first == RdnConstants.AtSign)
            {
                pos++;
                while (pos < buffer.Length)
                {
                    byte b = buffer[pos];
                    if (b == RdnConstants.ListSeparator || b == RdnConstants.CloseBrace || b == RdnConstants.CloseBracket ||
                        b == RdnConstants.Space || b == RdnConstants.CarriageReturn || b == RdnConstants.LineFeed || b == RdnConstants.Tab ||
                        b == RdnConstants.Equals)
                    {
                        break;
                    }
                    pos++;
                }
                return pos;
            }

            // Nested containers: track depth
            if (first == RdnConstants.OpenBrace || first == RdnConstants.OpenBracket)
            {
                int depth = 1;
                pos++;
                bool inString = false;
                while (pos < buffer.Length && depth > 0)
                {
                    byte b = buffer[pos];
                    if (inString)
                    {
                        if (b == RdnConstants.BackSlash)
                        {
                            pos++; // skip escaped char
                        }
                        else if (b == RdnConstants.Quote)
                        {
                            inString = false;
                        }
                    }
                    else
                    {
                        if (b == RdnConstants.Quote) inString = true;
                        else if (b == RdnConstants.OpenBrace || b == RdnConstants.OpenBracket) depth++;
                        else if (b == RdnConstants.CloseBrace || b == RdnConstants.CloseBracket) depth--;
                    }
                    pos++;
                }
                return depth == 0 ? pos : -1;
            }

            // Set{ or Map{ prefixed
            if ((first == (byte)'S' && pos + 3 < buffer.Length && buffer[pos + 1] == (byte)'e' && buffer[pos + 2] == (byte)'t' && buffer[pos + 3] == RdnConstants.OpenBrace) ||
                (first == (byte)'M' && pos + 3 < buffer.Length && buffer[pos + 1] == (byte)'a' && buffer[pos + 2] == (byte)'p' && buffer[pos + 3] == RdnConstants.OpenBrace))
            {
                pos += 4; // skip prefix + {
                int depth = 1;
                bool inString = false;
                while (pos < buffer.Length && depth > 0)
                {
                    byte b = buffer[pos];
                    if (inString)
                    {
                        if (b == RdnConstants.BackSlash) pos++;
                        else if (b == RdnConstants.Quote) inString = false;
                    }
                    else
                    {
                        if (b == RdnConstants.Quote) inString = true;
                        else if (b == RdnConstants.OpenBrace || b == RdnConstants.OpenBracket) depth++;
                        else if (b == RdnConstants.CloseBrace || b == RdnConstants.CloseBracket) depth--;
                    }
                    pos++;
                }
                return depth == 0 ? pos : -1;
            }

            return -1;
        }

        private bool ReadSingleSegment()
        {
            bool retVal = false;
            ValueSpan = default;
            ValueIsEscaped = false;

            if (!HasMoreData())
            {
                goto Done;
            }

            byte first = _buffer[_consumed];

            // This check is done as an optimization to avoid calling SkipWhiteSpace when not necessary.
            // SkipWhiteSpace only skips the whitespace characters as defined by RDN RFC 8259 section 2.
            // We do not validate if 'first' is an invalid RDN byte here (such as control characters).
            // Those cases are captured in ConsumeNextToken and ConsumeValue.
            if (first <= RdnConstants.Space)
            {
                SkipWhiteSpace();
                if (!HasMoreData())
                {
                    goto Done;
                }
                first = _buffer[_consumed];
            }

            TokenStartIndex = _consumed;

            if (_tokenType == RdnTokenType.None)
            {
                goto ReadFirstToken;
            }

            if (first == RdnConstants.Slash)
            {
                // In value position, route to ConsumeValue which handles regex/comment disambiguation
                if (_tokenType is RdnTokenType.StartArray or RdnTokenType.StartSet or RdnTokenType.StartMap or RdnTokenType.PropertyName)
                {
                    retVal = ConsumeValue(first);
                    goto Done;
                }
                // In structural position (StartObject, after values), route to comment handling
                retVal = ConsumeNextTokenOrRollback(first);
                goto Done;
            }

            if (_tokenType == RdnTokenType.StartObject)
            {
                if (first == RdnConstants.CloseBrace)
                {
                    EndObject();
                }
                else
                {
                    if (first != RdnConstants.Quote)
                    {
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyNotFound, first);
                    }

                    int prevConsumed = _consumed;
                    long prevPosition = _bytePositionInLine;
                    long prevLineNumber = _lineNumber;
                    retVal = ConsumePropertyName();
                    if (!retVal)
                    {
                        // roll back potential changes
                        _consumed = prevConsumed;
                        _tokenType = RdnTokenType.StartObject;
                        _bytePositionInLine = prevPosition;
                        _lineNumber = prevLineNumber;
                    }
                    goto Done;
                }
            }
            else if (_tokenType == RdnTokenType.StartSet)
            {
                if (first == RdnConstants.CloseBrace)
                {
                    EndSet();
                }
                else
                {
                    retVal = ConsumeValue(first);
                    goto Done;
                }
            }
            else if (_tokenType == RdnTokenType.StartMap)
            {
                if (first == RdnConstants.CloseBrace)
                {
                    EndMap();
                }
                else
                {
                    retVal = ConsumeValue(first);
                    goto Done;
                }
            }
            else if (_tokenType == RdnTokenType.StartArray)
            {
                if (first == RdnConstants.CloseBracket)
                {
                    EndArray();
                }
                else if (first == RdnConstants.CloseParen && IsCurrentDepthTuple())
                {
                    EndTuple();
                }
                else
                {
                    retVal = ConsumeValue(first);
                    goto Done;
                }
            }
            else if (_tokenType == RdnTokenType.PropertyName)
            {
                retVal = ConsumeValue(first);
                goto Done;
            }
            else
            {
                retVal = ConsumeNextTokenOrRollback(first);
                goto Done;
            }

            retVal = true;

        Done:
            return retVal;

        ReadFirstToken:
            retVal = ReadFirstToken(first);
            goto Done;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasMoreData()
        {
            if (_consumed >= (uint)_buffer.Length)
            {
                if (_isNotPrimitive && IsLastSpan)
                {
                    if (_bitStack.CurrentDepth != 0)
                    {
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ZeroDepthAtEnd);
                    }

                    if (_readerOptions.CommentHandling == RdnCommentHandling.Allow && _tokenType == RdnTokenType.Comment)
                    {
                        return false;
                    }

                    if (_tokenType is not RdnTokenType.EndArray and not RdnTokenType.EndObject and not RdnTokenType.EndSet and not RdnTokenType.EndMap)
                    {
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.InvalidEndOfRdnNonPrimitive);
                    }
                }
                return false;
            }
            return true;
        }

        // Unlike the parameter-less overload of HasMoreData, if there is no more data when this method is called, we know the RDN input is invalid.
        // This is because, this method is only called after a ',' (i.e. we expect a value/property name) or after
        // a property name, which means it must be followed by a value.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasMoreData(ExceptionResource resource)
        {
            if (_consumed >= (uint)_buffer.Length)
            {
                if (IsLastSpan)
                {
                    ThrowHelper.ThrowRdnReaderException(ref this, resource);
                }
                return false;
            }
            return true;
        }

        private bool ReadFirstToken(byte first)
        {
            if (first == RdnConstants.OpenBrace)
            {
                ConsumeBrace();
                _isNotPrimitive = true;
            }
            else if (first == RdnConstants.OpenBracket)
            {
                _bitStack.ResetFirstBit();
                _tokenType = RdnTokenType.StartArray;
                ValueSpan = _buffer.Slice(_consumed, 1);
                _consumed++;
                _bytePositionInLine++;
                _isNotPrimitive = true;
            }
            else if (first == RdnConstants.OpenParen)
            {
                StartTuple();
                _isNotPrimitive = true;
            }
            else
            {
                // Create local copy to avoid bounds checks.
                ReadOnlySpan<byte> localBuffer = _buffer;

                if (RdnHelpers.IsDigit(first) || first == '-')
                {
                    if (first == '-')
                    {
                        int nextIdx = _consumed + 1;
                        if (nextIdx < localBuffer.Length && localBuffer[nextIdx] == (byte)'I')
                        {
                            if (!ConsumeLiteral(RdnConstants.NegativeInfinityValue, RdnTokenType.Number))
                            {
                                return false;
                            }
                            goto DoneReadingFirstToken;
                        }
                        if (nextIdx >= localBuffer.Length && !IsLastSpan)
                        {
                            return false; // need more data
                        }
                    }
                    if (!TryGetNumber(localBuffer.Slice(_consumed), out int numberOfBytes, out bool isBigInteger2))
                    {
                        return false;
                    }
                    _tokenType = isBigInteger2 ? RdnTokenType.RdnBigInteger : RdnTokenType.Number;
                    _consumed += numberOfBytes;
                    _bytePositionInLine += numberOfBytes;
                }
                else if (!ConsumeValue(first))
                {
                    return false;
                }

            DoneReadingFirstToken:
                _isNotPrimitive = _tokenType is RdnTokenType.StartObject or RdnTokenType.StartArray or RdnTokenType.StartSet or RdnTokenType.StartMap;
                // Intentionally fall out of the if-block to return true
            }
            return true;
        }

        private void SkipWhiteSpace()
        {
            // Create local copy to avoid bounds checks.
            ReadOnlySpan<byte> localBuffer = _buffer;
            for (; _consumed < localBuffer.Length; _consumed++)
            {
                byte val = localBuffer[_consumed];

                // RDN RFC 8259 section 2 says only these 4 characters count, not all of the Unicode definitions of whitespace.
                if (val is not RdnConstants.Space and
                           not RdnConstants.CarriageReturn and
                           not RdnConstants.LineFeed and
                           not RdnConstants.Tab)
                {
                    break;
                }

                if (val == RdnConstants.LineFeed)
                {
                    _lineNumber++;
                    _bytePositionInLine = 0;
                }
                else
                {
                    _bytePositionInLine++;
                }
            }
        }

        /// <summary>
        /// This method contains the logic for processing the next value token and determining
        /// what type of data it is.
        /// </summary>
        private bool ConsumeValue(byte marker)
        {
            while (true)
            {
                Debug.Assert((_trailingCommaBeforeComment && _readerOptions.CommentHandling == RdnCommentHandling.Allow) || !_trailingCommaBeforeComment);
                Debug.Assert((_trailingCommaBeforeComment && marker != RdnConstants.Slash) || !_trailingCommaBeforeComment);
                _trailingCommaBeforeComment = false;

                if (marker == RdnConstants.Quote)
                {
                    return ConsumeString();
                }
                else if (marker == RdnConstants.OpenBrace)
                {
                    ConsumeBrace();
                }
                else if (marker == RdnConstants.OpenBracket)
                {
                    StartArray();
                }
                else if (marker == RdnConstants.OpenParen)
                {
                    StartTuple();
                }
                else if (RdnHelpers.IsDigit(marker) || marker == '-')
                {
                    if (marker == '-')
                    {
                        int nextIdx = _consumed + 1;
                        if (nextIdx < _buffer.Length && _buffer[nextIdx] == (byte)'I')
                        {
                            return ConsumeLiteral(RdnConstants.NegativeInfinityValue, RdnTokenType.Number);
                        }
                        if (nextIdx >= _buffer.Length && !IsLastSpan)
                        {
                            return false; // need more data
                        }
                    }
                    return ConsumeNumber();
                }
                else if (marker == (byte)'N')
                {
                    return ConsumeLiteral(RdnConstants.NaNValue, RdnTokenType.Number);
                }
                else if (marker == (byte)'I')
                {
                    return ConsumeLiteral(RdnConstants.PositiveInfinityValue, RdnTokenType.Number);
                }
                else if (marker == 'f')
                {
                    return ConsumeLiteral(RdnConstants.FalseValue, RdnTokenType.False);
                }
                else if (marker == 't')
                {
                    return ConsumeLiteral(RdnConstants.TrueValue, RdnTokenType.True);
                }
                else if (marker == 'n')
                {
                    return ConsumeLiteral(RdnConstants.NullValue, RdnTokenType.Null);
                }
                else if (marker == RdnConstants.AtSign)
                {
                    return ConsumeRdnLiteral();
                }
                else if (marker == RdnConstants.LetterS)
                {
                    return ConsumeExplicitSet();
                }
                else if (marker == RdnConstants.LetterM)
                {
                    return ConsumeExplicitMap();
                }
                else if (marker == RdnConstants.LetterB)
                {
                    return ConsumeBinaryB64();
                }
                else if (marker == RdnConstants.LetterX)
                {
                    return ConsumeBinaryHex();
                }
                else if (marker == RdnConstants.Slash)
                {
                    // Peek ahead: if next char is / or *, it may be a comment
                    int nextIdx = _consumed + 1;
                    if (nextIdx < _buffer.Length)
                    {
                        byte next = _buffer[nextIdx];
                        if ((next == RdnConstants.Slash || next == RdnConstants.Asterisk) && _readerOptions.CommentHandling != RdnCommentHandling.Disallow)
                        {
                            if (_readerOptions.CommentHandling == RdnCommentHandling.Allow)
                            {
                                return ConsumeComment();
                            }
                            else
                            {
                                Debug.Assert(_readerOptions.CommentHandling == RdnCommentHandling.Skip);
                                if (SkipComment())
                                {
                                    if (_consumed >= (uint)_buffer.Length)
                                    {
                                        if (_isNotPrimitive && IsLastSpan && _tokenType != RdnTokenType.EndArray && _tokenType != RdnTokenType.EndObject && _tokenType != RdnTokenType.EndSet)
                                        {
                                            ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.InvalidEndOfRdnNonPrimitive);
                                        }
                                        return false;
                                    }

                                    marker = _buffer[_consumed];

                                    if (marker <= RdnConstants.Space)
                                    {
                                        SkipWhiteSpace();
                                        if (!HasMoreData())
                                        {
                                            return false;
                                        }
                                        marker = _buffer[_consumed];
                                    }

                                    TokenStartIndex = _consumed;
                                    continue;
                                }
                                return false;
                            }
                        }
                    }
                    else if (!_isFinalBlock)
                    {
                        return false; // Need more data to distinguish regex from comment
                    }
                    // Not a comment → must be regex
                    return ConsumeRegex();
                }
                else
                {
                    switch (_readerOptions.CommentHandling)
                    {
                        case RdnCommentHandling.Disallow:
                            break;
                        case RdnCommentHandling.Allow:
                            if (marker == RdnConstants.Slash)
                            {
                                return ConsumeComment();
                            }
                            break;
                        default:
                            Debug.Assert(_readerOptions.CommentHandling == RdnCommentHandling.Skip);
                            if (marker == RdnConstants.Slash)
                            {
                                if (SkipComment())
                                {
                                    if (_consumed >= (uint)_buffer.Length)
                                    {
                                        if (_isNotPrimitive && IsLastSpan && _tokenType != RdnTokenType.EndArray && _tokenType != RdnTokenType.EndObject && _tokenType != RdnTokenType.EndSet)
                                        {
                                            ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.InvalidEndOfRdnNonPrimitive);
                                        }
                                        return false;
                                    }

                                    marker = _buffer[_consumed];

                                    // This check is done as an optimization to avoid calling SkipWhiteSpace when not necessary.
                                    if (marker <= RdnConstants.Space)
                                    {
                                        SkipWhiteSpace();
                                        if (!HasMoreData())
                                        {
                                            return false;
                                        }
                                        marker = _buffer[_consumed];
                                    }

                                    TokenStartIndex = _consumed;

                                    // Skip comments and consume the actual RDN value.
                                    continue;
                                }
                                return false;
                            }
                            break;
                    }
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, marker);
                }
                break;
            }
            return true;
        }

        // Consumes 'null', 'true', 'false', 'NaN', 'Infinity', or '-Infinity'
        private bool ConsumeLiteral(ReadOnlySpan<byte> literal, RdnTokenType tokenType)
        {
            ReadOnlySpan<byte> span = _buffer.Slice(_consumed);
            Debug.Assert(span.Length > 0);
            Debug.Assert(span[0] == 'n' || span[0] == 't' || span[0] == 'f' || span[0] == 'N' || span[0] == 'I' || span[0] == '-');

            if (!span.StartsWith(literal))
            {
                return CheckLiteral(span, literal);
            }

            ValueSpan = span.Slice(0, literal.Length);
            _tokenType = tokenType;
            _consumed += literal.Length;
            _bytePositionInLine += literal.Length;
            return true;
        }

        private bool CheckLiteral(ReadOnlySpan<byte> span, ReadOnlySpan<byte> literal)
        {
            Debug.Assert(span.Length > 0 && span[0] == literal[0]);

            int indexOfFirstMismatch = 0;

            for (int i = 1; i < literal.Length; i++)
            {
                if (span.Length > i)
                {
                    if (span[i] != literal[i])
                    {
                        _bytePositionInLine += i;
                        ThrowInvalidLiteral(span);
                    }
                }
                else
                {
                    indexOfFirstMismatch = i;
                    break;
                }
            }

            Debug.Assert(indexOfFirstMismatch > 0 && indexOfFirstMismatch < literal.Length);

            if (IsLastSpan)
            {
                _bytePositionInLine += indexOfFirstMismatch;
                ThrowInvalidLiteral(span);
            }
            return false;
        }

        private void ThrowInvalidLiteral(ReadOnlySpan<byte> span)
        {
            byte firstByte = span[0];

            ExceptionResource resource;
            switch (firstByte)
            {
                case (byte)'t':
                    resource = ExceptionResource.ExpectedTrue;
                    break;
                case (byte)'f':
                    resource = ExceptionResource.ExpectedFalse;
                    break;
                case (byte)'N':
                    resource = ExceptionResource.ExpectedNaN;
                    break;
                case (byte)'I':
                case (byte)'-':
                    resource = ExceptionResource.ExpectedInfinity;
                    break;
                default:
                    Debug.Assert(firstByte == 'n');
                    resource = ExceptionResource.ExpectedNull;
                    break;
            }
            ThrowHelper.ThrowRdnReaderException(ref this, resource, bytes: span);
        }

        private bool ConsumeNumber()
        {
            if (!TryGetNumber(_buffer.Slice(_consumed), out int consumed, out bool isBigInteger))
            {
                return false;
            }

            _tokenType = isBigInteger ? RdnTokenType.RdnBigInteger : RdnTokenType.Number;
            _consumed += consumed;
            _bytePositionInLine += consumed;

            if (_consumed >= (uint)_buffer.Length)
            {
                Debug.Assert(IsLastSpan);

                // If there is no more data, and the RDN is not a single value, throw.
                if (_isNotPrimitive)
                {
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedEndOfDigitNotFound, _buffer[_consumed - 1]);
                }
            }

            // If there is more data and the RDN is not a single value, assert that there is an end of number delimiter.
            // Else, if either the RDN is a single value XOR if there is no more data, don't assert anything since there won't always be an end of number delimiter.
            Debug.Assert(
                ((_consumed < _buffer.Length) &&
                !_isNotPrimitive &&
                RdnConstants.Delimiters.Contains(_buffer[_consumed]))
                || (_isNotPrimitive ^ (_consumed >= (uint)_buffer.Length)));

            return true;
        }

        private bool ConsumePropertyName()
        {
            _trailingCommaBeforeComment = false;

            if (!ConsumeString())
            {
                return false;
            }

            if (!HasMoreData(ExceptionResource.ExpectedValueAfterPropertyNameNotFound))
            {
                return false;
            }

            byte first = _buffer[_consumed];

            // This check is done as an optimization to avoid calling SkipWhiteSpace when not necessary.
            // We do not validate if 'first' is an invalid RDN byte here (such as control characters).
            // Those cases are captured below where we only accept ':'.
            if (first <= RdnConstants.Space)
            {
                SkipWhiteSpace();
                if (!HasMoreData(ExceptionResource.ExpectedValueAfterPropertyNameNotFound))
                {
                    return false;
                }
                first = _buffer[_consumed];
            }

            // The next character must be a key / value separator. Validate and skip.
            if (first != RdnConstants.KeyValueSeparator)
            {
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedSeparatorAfterPropertyNameNotFound, first);
            }

            _consumed++;
            _bytePositionInLine++;
            _tokenType = RdnTokenType.PropertyName;
            return true;
        }

        private bool ConsumeString()
        {
            Debug.Assert(_buffer.Length >= _consumed + 1);
            Debug.Assert(_buffer[_consumed] == RdnConstants.Quote);

            // Create local copy to avoid bounds checks.
            ReadOnlySpan<byte> localBuffer = _buffer.Slice(_consumed + 1);

            // Vectorized search for either quote, backslash, or any control character.
            // If the first found byte is a quote, we have reached an end of string, and
            // can avoid validation.
            // Otherwise, in the uncommon case, iterate one character at a time and validate.
            int idx = localBuffer.IndexOfQuoteOrAnyControlOrBackSlash();

            if (idx >= 0)
            {
                byte foundByte = localBuffer[idx];
                if (foundByte == RdnConstants.Quote)
                {
                    _bytePositionInLine += idx + 2; // Add 2 for the start and end quotes.
                    ValueSpan = localBuffer.Slice(0, idx);
                    ValueIsEscaped = false;
                    _tokenType = RdnTokenType.String;
                    _consumed += idx + 2;
                    return true;
                }
                else
                {
                    return ConsumeStringAndValidate(localBuffer, idx);
                }
            }
            else
            {
                if (IsLastSpan)
                {
                    _bytePositionInLine += localBuffer.Length + 1;  // Account for the start quote
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.EndOfStringNotFound);
                }
                return false;
            }
        }

        // Found a backslash or control characters which are considered invalid within a string.
        // Search through the rest of the string one byte at a time.
        // https://tools.ietf.org/html/rfc8259#section-7
        private bool ConsumeStringAndValidate(ReadOnlySpan<byte> data, int idx)
        {
            Debug.Assert(idx >= 0 && idx < data.Length);
            Debug.Assert(data[idx] != RdnConstants.Quote);
            Debug.Assert(data[idx] == RdnConstants.BackSlash || data[idx] < RdnConstants.Space);

            long prevLineBytePosition = _bytePositionInLine;
            long prevLineNumber = _lineNumber;

            _bytePositionInLine += idx + 1; // Add 1 for the first quote

            bool nextCharEscaped = false;
            for (; idx < data.Length; idx++)
            {
                byte currentByte = data[idx];
                if (currentByte == RdnConstants.Quote)
                {
                    if (!nextCharEscaped)
                    {
                        goto Done;
                    }
                    nextCharEscaped = false;
                }
                else if (currentByte == RdnConstants.BackSlash)
                {
                    nextCharEscaped = !nextCharEscaped;
                }
                else if (nextCharEscaped)
                {
                    int index = RdnConstants.EscapableChars.IndexOf(currentByte);
                    if (index == -1)
                    {
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.InvalidCharacterAfterEscapeWithinString, currentByte);
                    }

                    if (currentByte == 'u')
                    {
                        // Expecting 4 hex digits to follow the escaped 'u'
                        _bytePositionInLine++;  // move past the 'u'
                        if (ValidateHexDigits(data, idx + 1))
                        {
                            idx += 4;   // Skip the 4 hex digits, the for loop accounts for idx incrementing past the 'u'
                        }
                        else
                        {
                            // We found less than 4 hex digits. Check if there is more data to follow, otherwise throw.
                            idx = data.Length;
                            break;
                        }

                    }
                    nextCharEscaped = false;
                }
                else if (currentByte < RdnConstants.Space)
                {
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.InvalidCharacterWithinString, currentByte);
                }

                _bytePositionInLine++;
            }

            if (idx >= data.Length)
            {
                if (IsLastSpan)
                {
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.EndOfStringNotFound);
                }
                _lineNumber = prevLineNumber;
                _bytePositionInLine = prevLineBytePosition;
                return false;
            }

        Done:
            _bytePositionInLine++;  // Add 1 for the end quote
            ValueSpan = data.Slice(0, idx);
            ValueIsEscaped = true;
            _tokenType = RdnTokenType.String;
            _consumed += idx + 2;
            return true;
        }

        private bool ValidateHexDigits(ReadOnlySpan<byte> data, int idx)
        {
            for (int j = idx; j < data.Length; j++)
            {
                byte nextByte = data[j];
                if (!RdnReaderHelper.IsHexDigit(nextByte))
                {
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.InvalidHexCharacterWithinString, nextByte);
                }
                if (j - idx >= 3)
                {
                    return true;
                }
                _bytePositionInLine++;
            }

            return false;
        }

        // https://tools.ietf.org/html/rfc7159#section-6
        private bool TryGetNumber(ReadOnlySpan<byte> data, out int consumed, out bool isBigInteger)
        {
            // TODO: https://github.com/dotnet/runtime/issues/27837
            Debug.Assert(data.Length > 0);

            consumed = 0;
            isBigInteger = false;
            int i = 0;
            bool hasDecimalOrExponent = false;

            ConsumeNumberResult signResult = ConsumeNegativeSign(ref data, ref i);
            if (signResult == ConsumeNumberResult.NeedMoreData)
            {
                return false;
            }

            Debug.Assert(signResult == ConsumeNumberResult.OperationIncomplete);

            byte nextByte = data[i];
            Debug.Assert(nextByte >= '0' && nextByte <= '9');

            if (nextByte == '0')
            {
                ConsumeNumberResult result = ConsumeZero(ref data, ref i);
                if (result == ConsumeNumberResult.NeedMoreData)
                {
                    return false;
                }
                if (result == ConsumeNumberResult.Success)
                {
                    goto Done;
                }

                Debug.Assert(result == ConsumeNumberResult.OperationIncomplete);
                nextByte = data[i];

                // BigInteger: 0n
                if (nextByte == (byte)'n')
                {
                    isBigInteger = true;
                    // ValueSpan = digits only (without 'n'), consumed includes 'n'
                    ValueSpan = data.Slice(0, i);
                    consumed = i + 1; // skip the 'n'
                    return true;
                }
            }
            else
            {
                i++;
                ConsumeNumberResult result = ConsumeIntegerDigits(ref data, ref i);
                if (result == ConsumeNumberResult.NeedMoreData)
                {
                    return false;
                }
                if (result == ConsumeNumberResult.Success)
                {
                    goto Done;
                }

                Debug.Assert(result == ConsumeNumberResult.OperationIncomplete);
                nextByte = data[i];

                // BigInteger: <digits>n
                if (nextByte == (byte)'n')
                {
                    isBigInteger = true;
                    ValueSpan = data.Slice(0, i);
                    consumed = i + 1; // skip the 'n'
                    return true;
                }

                if (nextByte != '.' && nextByte != 'E' && nextByte != 'e')
                {
                    _bytePositionInLine += i;
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedEndOfDigitNotFound, nextByte);
                }
            }

            Debug.Assert(nextByte == '.' || nextByte == 'E' || nextByte == 'e');
            hasDecimalOrExponent = true;

            if (nextByte == '.')
            {
                i++;
                ConsumeNumberResult result = ConsumeDecimalDigits(ref data, ref i);
                if (result == ConsumeNumberResult.NeedMoreData)
                {
                    return false;
                }
                if (result == ConsumeNumberResult.Success)
                {
                    goto Done;
                }

                Debug.Assert(result == ConsumeNumberResult.OperationIncomplete);
                nextByte = data[i];
                if (nextByte != 'E' && nextByte != 'e')
                {
                    _bytePositionInLine += i;
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedNextDigitEValueNotFound, nextByte);
                }
            }

            Debug.Assert(nextByte == 'E' || nextByte == 'e');
            i++;

            signResult = ConsumeSign(ref data, ref i);
            if (signResult == ConsumeNumberResult.NeedMoreData)
            {
                return false;
            }

            Debug.Assert(signResult == ConsumeNumberResult.OperationIncomplete);

            i++;
            ConsumeNumberResult resultExponent = ConsumeIntegerDigits(ref data, ref i);
            if (resultExponent == ConsumeNumberResult.NeedMoreData)
            {
                return false;
            }
            if (resultExponent == ConsumeNumberResult.Success)
            {
                goto Done;
            }

            Debug.Assert(resultExponent == ConsumeNumberResult.OperationIncomplete);

            _bytePositionInLine += i;
            ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedEndOfDigitNotFound, data[i]);

        Done:
            ValueSpan = data.Slice(0, i);
            consumed = i;
            return true;
        }

        private ConsumeNumberResult ConsumeNegativeSign(ref ReadOnlySpan<byte> data, scoped ref int i)
        {
            byte nextByte = data[i];

            if (nextByte == '-')
            {
                i++;
                if (i >= data.Length)
                {
                    if (IsLastSpan)
                    {
                        _bytePositionInLine += i;
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.RequiredDigitNotFoundEndOfData);
                    }
                    return ConsumeNumberResult.NeedMoreData;
                }

                nextByte = data[i];
                if (!RdnHelpers.IsDigit(nextByte))
                {
                    _bytePositionInLine += i;
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.RequiredDigitNotFoundAfterSign, nextByte);
                }
            }
            return ConsumeNumberResult.OperationIncomplete;
        }

        private ConsumeNumberResult ConsumeZero(ref ReadOnlySpan<byte> data, scoped ref int i)
        {
            Debug.Assert(data[i] == (byte)'0');
            i++;
            byte nextByte;
            if (i < data.Length)
            {
                nextByte = data[i];
                if (RdnConstants.Delimiters.Contains(nextByte))
                {
                    return ConsumeNumberResult.Success;
                }
            }
            else
            {
                if (IsLastSpan)
                {
                    // A payload containing a single value: "0" is valid
                    // If we are dealing with multi-value RDN,
                    // ConsumeNumber will validate that we have a delimiter following the "0".
                    return ConsumeNumberResult.Success;
                }
                else
                {
                    return ConsumeNumberResult.NeedMoreData;
                }
            }
            nextByte = data[i];
            if (nextByte != '.' && nextByte != 'E' && nextByte != 'e' && nextByte != 'n')
            {
                _bytePositionInLine += i;
                ThrowHelper.ThrowRdnReaderException(ref this,
                    RdnHelpers.IsInRangeInclusive(nextByte, '0', '9') ? ExceptionResource.InvalidLeadingZeroInNumber : ExceptionResource.ExpectedEndOfDigitNotFound,
                    nextByte);
            }

            return ConsumeNumberResult.OperationIncomplete;
        }

        private ConsumeNumberResult ConsumeIntegerDigits(ref ReadOnlySpan<byte> data, scoped ref int i)
        {
            byte nextByte = default;
            for (; i < data.Length; i++)
            {
                nextByte = data[i];
                if (!RdnHelpers.IsDigit(nextByte))
                {
                    break;
                }
            }
            if (i >= data.Length)
            {
                if (IsLastSpan)
                {
                    // A payload containing a single value of integers (e.g. "12") is valid
                    // If we are dealing with multi-value RDN,
                    // ConsumeNumber will validate that we have a delimiter following the integer.
                    return ConsumeNumberResult.Success;
                }
                else
                {
                    return ConsumeNumberResult.NeedMoreData;
                }
            }
            if (RdnConstants.Delimiters.Contains(nextByte))
            {
                return ConsumeNumberResult.Success;
            }

            return ConsumeNumberResult.OperationIncomplete;
        }

        private ConsumeNumberResult ConsumeDecimalDigits(ref ReadOnlySpan<byte> data, scoped ref int i)
        {
            if (i >= data.Length)
            {
                if (IsLastSpan)
                {
                    _bytePositionInLine += i;
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.RequiredDigitNotFoundEndOfData);
                }
                return ConsumeNumberResult.NeedMoreData;
            }
            byte nextByte = data[i];
            if (!RdnHelpers.IsDigit(nextByte))
            {
                _bytePositionInLine += i;
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.RequiredDigitNotFoundAfterDecimal, nextByte);
            }
            i++;

            return ConsumeIntegerDigits(ref data, ref i);
        }

        private ConsumeNumberResult ConsumeSign(ref ReadOnlySpan<byte> data, scoped ref int i)
        {
            if (i >= data.Length)
            {
                if (IsLastSpan)
                {
                    _bytePositionInLine += i;
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.RequiredDigitNotFoundEndOfData);
                }
                return ConsumeNumberResult.NeedMoreData;
            }

            byte nextByte = data[i];
            if (nextByte == '+' || nextByte == '-')
            {
                i++;
                if (i >= data.Length)
                {
                    if (IsLastSpan)
                    {
                        _bytePositionInLine += i;
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.RequiredDigitNotFoundEndOfData);
                    }
                    return ConsumeNumberResult.NeedMoreData;
                }
                nextByte = data[i];
            }

            if (!RdnHelpers.IsDigit(nextByte))
            {
                _bytePositionInLine += i;
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.RequiredDigitNotFoundAfterSign, nextByte);
            }

            return ConsumeNumberResult.OperationIncomplete;
        }

        private bool ConsumeNextTokenOrRollback(byte marker)
        {
            int prevConsumed = _consumed;
            long prevPosition = _bytePositionInLine;
            long prevLineNumber = _lineNumber;
            RdnTokenType prevTokenType = _tokenType;
            bool prevTrailingCommaBeforeComment = _trailingCommaBeforeComment;
            ConsumeTokenResult result = ConsumeNextToken(marker);
            if (result == ConsumeTokenResult.Success)
            {
                return true;
            }
            if (result == ConsumeTokenResult.NotEnoughDataRollBackState)
            {
                _consumed = prevConsumed;
                _tokenType = prevTokenType;
                _bytePositionInLine = prevPosition;
                _lineNumber = prevLineNumber;
                _trailingCommaBeforeComment = prevTrailingCommaBeforeComment;
            }
            return false;
        }

        /// <summary>
        /// This method consumes the next token regardless of whether we are inside an object or an array.
        /// For an object, it reads the next property name token. For an array, it just reads the next value.
        /// </summary>
        private ConsumeTokenResult ConsumeNextToken(byte marker)
        {
            if (_readerOptions.CommentHandling != RdnCommentHandling.Disallow)
            {
                if (_readerOptions.CommentHandling == RdnCommentHandling.Allow)
                {
                    if (marker == RdnConstants.Slash)
                    {
                        return ConsumeComment() ? ConsumeTokenResult.Success : ConsumeTokenResult.NotEnoughDataRollBackState;
                    }
                    if (_tokenType == RdnTokenType.Comment)
                    {
                        return ConsumeNextTokenFromLastNonCommentToken();
                    }
                }
                else
                {
                    Debug.Assert(_readerOptions.CommentHandling == RdnCommentHandling.Skip);
                    return ConsumeNextTokenUntilAfterAllCommentsAreSkipped(marker);
                }
            }

            if (_bitStack.CurrentDepth == 0)
            {
                if (_readerOptions.AllowMultipleValues)
                {
                    return ReadFirstToken(marker) ? ConsumeTokenResult.Success : ConsumeTokenResult.NotEnoughDataRollBackState;
                }

                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedEndAfterSingleRdn, marker);
            }

            // Map arrow: after reading a map key, consume => and then read the value
            if (marker == RdnConstants.Equals && IsCurrentDepthMap() && IsMapExpectingArrow())
            {
                if (!ConsumeMapArrow())
                {
                    return ConsumeTokenResult.NotEnoughDataRollBackState;
                }
                ClearMapArrowExpect();

                // Skip whitespace after =>
                if (_consumed >= (uint)_buffer.Length)
                {
                    if (IsLastSpan)
                    {
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyOrValueNotFound);
                    }
                    return ConsumeTokenResult.NotEnoughDataRollBackState;
                }
                byte first = _buffer[_consumed];
                if (first <= RdnConstants.Space)
                {
                    SkipWhiteSpace();
                    if (!HasMoreData(ExceptionResource.ExpectedStartOfPropertyOrValueNotFound))
                    {
                        return ConsumeTokenResult.NotEnoughDataRollBackState;
                    }
                    first = _buffer[_consumed];
                }
                TokenStartIndex = _consumed;
                return ConsumeValue(first) ? ConsumeTokenResult.Success : ConsumeTokenResult.NotEnoughDataRollBackState;
            }

            if (marker == RdnConstants.ListSeparator)
            {
                _consumed++;
                _bytePositionInLine++;

                // After comma in a map, the next value is a key
                if (IsCurrentDepthMap())
                {
                    SetMapArrowExpect();
                }

                if (_consumed >= (uint)_buffer.Length)
                {
                    if (IsLastSpan)
                    {
                        _consumed--;
                        _bytePositionInLine--;
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyOrValueNotFound);
                    }
                    return ConsumeTokenResult.NotEnoughDataRollBackState;
                }
                byte first = _buffer[_consumed];

                // This check is done as an optimization to avoid calling SkipWhiteSpace when not necessary.
                if (first <= RdnConstants.Space)
                {
                    SkipWhiteSpace();
                    // The next character must be a start of a property name or value.
                    if (!HasMoreData(ExceptionResource.ExpectedStartOfPropertyOrValueNotFound))
                    {
                        return ConsumeTokenResult.NotEnoughDataRollBackState;
                    }
                    first = _buffer[_consumed];
                }

                TokenStartIndex = _consumed;

                if (_readerOptions.CommentHandling == RdnCommentHandling.Allow && first == RdnConstants.Slash)
                {
                    if (_inObject)
                    {
                        // In object context: / after comma must be a comment (property names are always quoted)
                        _trailingCommaBeforeComment = true;
                        return ConsumeComment() ? ConsumeTokenResult.Success : ConsumeTokenResult.NotEnoughDataRollBackState;
                    }
                    // In array/set/map: fall through to ConsumeValue which handles regex/comment disambiguation
                }

                if (_inObject)
                {
                    if (first != RdnConstants.Quote)
                    {
                        if (first == RdnConstants.CloseBrace)
                        {
                            if (_readerOptions.AllowTrailingCommas)
                            {
                                EndObject();
                                return ConsumeTokenResult.Success;
                            }
                            ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeObjectEnd);
                        }
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyNotFound, first);
                    }
                    return ConsumePropertyName() ? ConsumeTokenResult.Success : ConsumeTokenResult.NotEnoughDataRollBackState;
                }
                else
                {
                    if (first == RdnConstants.CloseBracket)
                    {
                        if (_readerOptions.AllowTrailingCommas)
                        {
                            EndArray();
                            return ConsumeTokenResult.Success;
                        }
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeArrayEnd);
                    }
                    if (first == RdnConstants.CloseBrace && IsCurrentDepthSet())
                    {
                        if (_readerOptions.AllowTrailingCommas)
                        {
                            EndSet();
                            return ConsumeTokenResult.Success;
                        }
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeArrayEnd);
                    }
                    if (first == RdnConstants.CloseBrace && IsCurrentDepthMap())
                    {
                        if (_readerOptions.AllowTrailingCommas)
                        {
                            EndMap();
                            return ConsumeTokenResult.Success;
                        }
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeArrayEnd);
                    }
                    if (first == RdnConstants.CloseParen && IsCurrentDepthTuple())
                    {
                        if (_readerOptions.AllowTrailingCommas)
                        {
                            EndTuple();
                            return ConsumeTokenResult.Success;
                        }
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeArrayEnd);
                    }
                    return ConsumeValue(first) ? ConsumeTokenResult.Success : ConsumeTokenResult.NotEnoughDataRollBackState;
                }
            }
            else if (marker == RdnConstants.CloseBrace)
            {
                if (_inObject)
                {
                    EndObject();
                }
                else if (IsCurrentDepthSet())
                {
                    EndSet();
                }
                else if (IsCurrentDepthMap())
                {
                    EndMap();
                }
                else
                {
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.MismatchedObjectArray, RdnConstants.CloseBrace);
                }
            }
            else if (marker == RdnConstants.CloseBracket)
            {
                EndArray();
            }
            else if (marker == RdnConstants.CloseParen)
            {
                EndTuple();
            }
            else
            {
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.FoundInvalidCharacter, marker);
            }
            return ConsumeTokenResult.Success;
        }

        private ConsumeTokenResult ConsumeNextTokenFromLastNonCommentToken()
        {
            Debug.Assert(_readerOptions.CommentHandling == RdnCommentHandling.Allow);
            Debug.Assert(_tokenType == RdnTokenType.Comment);

            if (RdnReaderHelper.IsTokenTypePrimitive(_previousTokenType))
            {
                _tokenType = _inObject ? RdnTokenType.StartObject : (IsCurrentDepthMap() ? RdnTokenType.StartMap : (IsCurrentDepthSet() ? RdnTokenType.StartSet : RdnTokenType.StartArray));
            }
            else
            {
                _tokenType = _previousTokenType;
            }

            Debug.Assert(_tokenType != RdnTokenType.Comment);

            if (!HasMoreData())
            {
                goto RollBack;
            }

            byte first = _buffer[_consumed];

            // This check is done as an optimization to avoid calling SkipWhiteSpace when not necessary.
            if (first <= RdnConstants.Space)
            {
                SkipWhiteSpace();
                if (!HasMoreData())
                {
                    goto RollBack;
                }
                first = _buffer[_consumed];
            }

            if (_bitStack.CurrentDepth == 0 && _tokenType != RdnTokenType.None)
            {
                if (_readerOptions.AllowMultipleValues)
                {
                    return ReadFirstToken(first) ? ConsumeTokenResult.Success : ConsumeTokenResult.NotEnoughDataRollBackState;
                }

                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedEndAfterSingleRdn, first);
            }

            // Note: first CAN be Slash here for regex values (e.g., after a comment in an array: [/* comment */ /regex/gi])
            Debug.Assert(first != RdnConstants.Slash || !_inObject);

            TokenStartIndex = _consumed;

            // Map arrow handling (for comment recovery path)
            if (first == RdnConstants.Equals && IsCurrentDepthMap() && IsMapExpectingArrow())
            {
                if (!ConsumeMapArrow())
                {
                    goto RollBack;
                }
                ClearMapArrowExpect();

                if (_consumed >= (uint)_buffer.Length)
                {
                    if (IsLastSpan)
                    {
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyOrValueNotFound);
                    }
                    goto RollBack;
                }
                first = _buffer[_consumed];
                if (first <= RdnConstants.Space)
                {
                    SkipWhiteSpace();
                    if (!HasMoreData(ExceptionResource.ExpectedStartOfPropertyOrValueNotFound))
                    {
                        goto RollBack;
                    }
                    first = _buffer[_consumed];
                }
                TokenStartIndex = _consumed;
                if (ConsumeValue(first))
                {
                    goto Done;
                }
                else
                {
                    goto RollBack;
                }
            }

            if (first == RdnConstants.ListSeparator)
            {
                // A comma without some RDN value preceding it is invalid
                if (_previousTokenType <= RdnTokenType.StartObject || _previousTokenType == RdnTokenType.StartArray || _previousTokenType == RdnTokenType.StartSet || _previousTokenType == RdnTokenType.StartMap || _trailingCommaBeforeComment)
                {
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyOrValueAfterComment, first);
                }

                _consumed++;
                _bytePositionInLine++;

                // After comma in a map, the next value is a key
                if (IsCurrentDepthMap())
                {
                    SetMapArrowExpect();
                }

                if (_consumed >= (uint)_buffer.Length)
                {
                    if (IsLastSpan)
                    {
                        _consumed--;
                        _bytePositionInLine--;
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyOrValueNotFound);
                    }
                    goto RollBack;
                }
                first = _buffer[_consumed];

                // This check is done as an optimization to avoid calling SkipWhiteSpace when not necessary.
                if (first <= RdnConstants.Space)
                {
                    SkipWhiteSpace();
                    // The next character must be a start of a property name or value.
                    if (!HasMoreData(ExceptionResource.ExpectedStartOfPropertyOrValueNotFound))
                    {
                        goto RollBack;
                    }
                    first = _buffer[_consumed];
                }

                TokenStartIndex = _consumed;

                if (first == RdnConstants.Slash)
                {
                    if (_inObject)
                    {
                        // In object context: / after comma must be a comment (property names are always quoted)
                        _trailingCommaBeforeComment = true;
                        if (ConsumeComment())
                        {
                            goto Done;
                        }
                        else
                        {
                            goto RollBack;
                        }
                    }
                    // In array/set/map: fall through to ConsumeValue which handles regex/comment disambiguation
                }

                if (_inObject)
                {
                    if (first != RdnConstants.Quote)
                    {
                        if (first == RdnConstants.CloseBrace)
                        {
                            if (_readerOptions.AllowTrailingCommas)
                            {
                                EndObject();
                                goto Done;
                            }
                            ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeObjectEnd);
                        }

                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyNotFound, first);
                    }
                    if (ConsumePropertyName())
                    {
                        goto Done;
                    }
                    else
                    {
                        goto RollBack;
                    }
                }
                else
                {
                    if (first == RdnConstants.CloseBracket)
                    {
                        if (_readerOptions.AllowTrailingCommas)
                        {
                            EndArray();
                            goto Done;
                        }
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeArrayEnd);
                    }
                    if (first == RdnConstants.CloseBrace && IsCurrentDepthSet())
                    {
                        if (_readerOptions.AllowTrailingCommas)
                        {
                            EndSet();
                            goto Done;
                        }
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeArrayEnd);
                    }
                    if (first == RdnConstants.CloseBrace && IsCurrentDepthMap())
                    {
                        if (_readerOptions.AllowTrailingCommas)
                        {
                            EndMap();
                            goto Done;
                        }
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeArrayEnd);
                    }
                    if (first == RdnConstants.CloseParen && IsCurrentDepthTuple())
                    {
                        if (_readerOptions.AllowTrailingCommas)
                        {
                            EndTuple();
                            goto Done;
                        }
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeArrayEnd);
                    }

                    if (ConsumeValue(first))
                    {
                        goto Done;
                    }
                    else
                    {
                        goto RollBack;
                    }
                }
            }
            else if (first == RdnConstants.CloseBrace)
            {
                if (_inObject)
                {
                    EndObject();
                }
                else if (IsCurrentDepthSet())
                {
                    EndSet();
                }
                else if (IsCurrentDepthMap())
                {
                    EndMap();
                }
                else
                {
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.MismatchedObjectArray, RdnConstants.CloseBrace);
                }
            }
            else if (first == RdnConstants.CloseBracket)
            {
                EndArray();
            }
            else if (first == RdnConstants.CloseParen)
            {
                EndTuple();
            }
            else if (_tokenType == RdnTokenType.None)
            {
                if (ReadFirstToken(first))
                {
                    goto Done;
                }
                else
                {
                    goto RollBack;
                }
            }
            else if (_tokenType == RdnTokenType.StartObject)
            {
                Debug.Assert(first != RdnConstants.CloseBrace);
                if (first != RdnConstants.Quote)
                {
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyNotFound, first);
                }

                int prevConsumed = _consumed;
                long prevPosition = _bytePositionInLine;
                long prevLineNumber = _lineNumber;
                if (!ConsumePropertyName())
                {
                    // roll back potential changes
                    _consumed = prevConsumed;
                    _tokenType = RdnTokenType.StartObject;
                    _bytePositionInLine = prevPosition;
                    _lineNumber = prevLineNumber;
                    goto RollBack;
                }
                goto Done;
            }
            else if (_tokenType == RdnTokenType.StartSet)
            {
                Debug.Assert(first != RdnConstants.CloseBrace);
                if (!ConsumeValue(first))
                {
                    goto RollBack;
                }
                goto Done;
            }
            else if (_tokenType == RdnTokenType.StartMap)
            {
                Debug.Assert(first != RdnConstants.CloseBrace);
                if (!ConsumeValue(first))
                {
                    goto RollBack;
                }
                goto Done;
            }
            else if (_tokenType == RdnTokenType.StartArray)
            {
                Debug.Assert(first != RdnConstants.CloseBracket);
                if (!ConsumeValue(first))
                {
                    goto RollBack;
                }
                goto Done;
            }
            else if (_tokenType == RdnTokenType.PropertyName)
            {
                if (!ConsumeValue(first))
                {
                    goto RollBack;
                }
                goto Done;
            }
            else
            {
                Debug.Assert(_tokenType is RdnTokenType.EndArray or RdnTokenType.EndObject or RdnTokenType.EndSet or RdnTokenType.EndMap);
                if (_inObject)
                {
                    Debug.Assert(first != RdnConstants.CloseBrace);
                    if (first != RdnConstants.Quote)
                    {
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyNotFound, first);
                    }

                    if (ConsumePropertyName())
                    {
                        goto Done;
                    }
                    else
                    {
                        goto RollBack;
                    }
                }
                else
                {
                    Debug.Assert(first != RdnConstants.CloseBracket);

                    if (ConsumeValue(first))
                    {
                        goto Done;
                    }
                    else
                    {
                        goto RollBack;
                    }
                }
            }

        Done:
            return ConsumeTokenResult.Success;

        RollBack:
            return ConsumeTokenResult.NotEnoughDataRollBackState;
        }

        private bool SkipAllComments(scoped ref byte marker)
        {
            while (marker == RdnConstants.Slash)
            {
                // Peek ahead: only treat as comment if next byte is / or *; otherwise it's a regex
                int nextIdx = _consumed + 1;
                if (nextIdx < _buffer.Length)
                {
                    byte next = _buffer[nextIdx];
                    if (next != RdnConstants.Slash && next != RdnConstants.Asterisk)
                        break;
                }
                else if (!_isFinalBlock)
                {
                    break; // Need more data
                }
                else
                {
                    break; // Single trailing slash at end of input — not a valid comment
                }

                if (SkipComment())
                {
                    if (!HasMoreData())
                    {
                        goto IncompleteNoRollback;
                    }

                    marker = _buffer[_consumed];

                    // This check is done as an optimization to avoid calling SkipWhiteSpace when not necessary.
                    if (marker <= RdnConstants.Space)
                    {
                        SkipWhiteSpace();
                        if (!HasMoreData())
                        {
                            goto IncompleteNoRollback;
                        }
                        marker = _buffer[_consumed];
                    }
                }
                else
                {
                    goto IncompleteNoRollback;
                }
            }
            return true;

        IncompleteNoRollback:
            return false;
        }

        private bool SkipAllComments(scoped ref byte marker, ExceptionResource resource)
        {
            while (marker == RdnConstants.Slash)
            {
                // Peek ahead: only treat as comment if next byte is / or *; otherwise it's a regex
                int nextIdx = _consumed + 1;
                if (nextIdx < _buffer.Length)
                {
                    byte next = _buffer[nextIdx];
                    if (next != RdnConstants.Slash && next != RdnConstants.Asterisk)
                        break;
                }
                else if (!_isFinalBlock)
                {
                    break; // Need more data
                }
                else
                {
                    break; // Single trailing slash at end of input — not a valid comment
                }

                if (SkipComment())
                {
                    // The next character must be a start of a property name or value.
                    if (!HasMoreData(resource))
                    {
                        goto IncompleteRollback;
                    }

                    marker = _buffer[_consumed];

                    // This check is done as an optimization to avoid calling SkipWhiteSpace when not necessary.
                    if (marker <= RdnConstants.Space)
                    {
                        SkipWhiteSpace();
                        // The next character must be a start of a property name or value.
                        if (!HasMoreData(resource))
                        {
                            goto IncompleteRollback;
                        }
                        marker = _buffer[_consumed];
                    }
                }
                else
                {
                    goto IncompleteRollback;
                }
            }
            return true;

        IncompleteRollback:
            return false;
        }

        private ConsumeTokenResult ConsumeNextTokenUntilAfterAllCommentsAreSkipped(byte marker)
        {
            if (!SkipAllComments(ref marker))
            {
                goto IncompleteNoRollback;
            }

            TokenStartIndex = _consumed;

            if (_tokenType == RdnTokenType.StartObject)
            {
                if (marker == RdnConstants.CloseBrace)
                {
                    EndObject();
                }
                else
                {
                    if (marker != RdnConstants.Quote)
                    {
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyNotFound, marker);
                    }

                    int prevConsumed = _consumed;
                    long prevPosition = _bytePositionInLine;
                    long prevLineNumber = _lineNumber;
                    if (!ConsumePropertyName())
                    {
                        // roll back potential changes
                        _consumed = prevConsumed;
                        _tokenType = RdnTokenType.StartObject;
                        _bytePositionInLine = prevPosition;
                        _lineNumber = prevLineNumber;
                        goto IncompleteNoRollback;
                    }
                    goto Done;
                }
            }
            else if (_tokenType == RdnTokenType.StartSet)
            {
                if (marker == RdnConstants.CloseBrace)
                {
                    EndSet();
                }
                else
                {
                    if (!ConsumeValue(marker))
                    {
                        goto IncompleteNoRollback;
                    }
                    goto Done;
                }
            }
            else if (_tokenType == RdnTokenType.StartMap)
            {
                if (marker == RdnConstants.CloseBrace)
                {
                    EndMap();
                }
                else
                {
                    if (!ConsumeValue(marker))
                    {
                        goto IncompleteNoRollback;
                    }
                    goto Done;
                }
            }
            else if (_tokenType == RdnTokenType.StartArray)
            {
                if (marker == RdnConstants.CloseBracket)
                {
                    EndArray();
                }
                else if (marker == RdnConstants.CloseParen && IsCurrentDepthTuple())
                {
                    EndTuple();
                }
                else
                {
                    if (!ConsumeValue(marker))
                    {
                        goto IncompleteNoRollback;
                    }
                    goto Done;
                }
            }
            else if (_tokenType == RdnTokenType.PropertyName)
            {
                if (!ConsumeValue(marker))
                {
                    goto IncompleteNoRollback;
                }
                goto Done;
            }
            else if (_bitStack.CurrentDepth == 0)
            {
                if (_readerOptions.AllowMultipleValues)
                {
                    return ReadFirstToken(marker) ? ConsumeTokenResult.Success : ConsumeTokenResult.NotEnoughDataRollBackState;
                }

                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedEndAfterSingleRdn, marker);
            }
            else if (marker == RdnConstants.Equals && IsCurrentDepthMap() && IsMapExpectingArrow())
            {
                if (!ConsumeMapArrow())
                {
                    return ConsumeTokenResult.NotEnoughDataRollBackState;
                }
                ClearMapArrowExpect();

                if (_consumed >= (uint)_buffer.Length)
                {
                    if (IsLastSpan)
                    {
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyOrValueNotFound);
                    }
                    return ConsumeTokenResult.NotEnoughDataRollBackState;
                }
                marker = _buffer[_consumed];
                if (marker <= RdnConstants.Space)
                {
                    SkipWhiteSpace();
                    if (!HasMoreData(ExceptionResource.ExpectedStartOfPropertyOrValueNotFound))
                    {
                        return ConsumeTokenResult.NotEnoughDataRollBackState;
                    }
                    marker = _buffer[_consumed];
                }

                if (!SkipAllComments(ref marker, ExceptionResource.ExpectedStartOfPropertyOrValueNotFound))
                {
                    goto IncompleteRollback;
                }

                TokenStartIndex = _consumed;
                return ConsumeValue(marker) ? ConsumeTokenResult.Success : ConsumeTokenResult.NotEnoughDataRollBackState;
            }
            else if (marker == RdnConstants.ListSeparator)
            {
                _consumed++;
                _bytePositionInLine++;

                if (IsCurrentDepthMap())
                {
                    SetMapArrowExpect();
                }

                if (_consumed >= (uint)_buffer.Length)
                {
                    if (IsLastSpan)
                    {
                        _consumed--;
                        _bytePositionInLine--;
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyOrValueNotFound);
                    }
                    return ConsumeTokenResult.NotEnoughDataRollBackState;
                }
                marker = _buffer[_consumed];

                // This check is done as an optimization to avoid calling SkipWhiteSpace when not necessary.
                if (marker <= RdnConstants.Space)
                {
                    SkipWhiteSpace();
                    // The next character must be a start of a property name or value.
                    if (!HasMoreData(ExceptionResource.ExpectedStartOfPropertyOrValueNotFound))
                    {
                        return ConsumeTokenResult.NotEnoughDataRollBackState;
                    }
                    marker = _buffer[_consumed];
                }

                if (!SkipAllComments(ref marker, ExceptionResource.ExpectedStartOfPropertyOrValueNotFound))
                {
                    goto IncompleteRollback;
                }

                TokenStartIndex = _consumed;

                if (_inObject)
                {
                    if (marker != RdnConstants.Quote)
                    {
                        if (marker == RdnConstants.CloseBrace)
                        {
                            if (_readerOptions.AllowTrailingCommas)
                            {
                                EndObject();
                                goto Done;
                            }
                            ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeObjectEnd);
                        }

                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyNotFound, marker);
                    }
                    return ConsumePropertyName() ? ConsumeTokenResult.Success : ConsumeTokenResult.NotEnoughDataRollBackState;
                }
                else
                {
                    if (marker == RdnConstants.CloseBracket)
                    {
                        if (_readerOptions.AllowTrailingCommas)
                        {
                            EndArray();
                            goto Done;
                        }
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeArrayEnd);
                    }
                    if (marker == RdnConstants.CloseBrace && IsCurrentDepthSet())
                    {
                        if (_readerOptions.AllowTrailingCommas)
                        {
                            EndSet();
                            goto Done;
                        }
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeArrayEnd);
                    }
                    if (marker == RdnConstants.CloseBrace && IsCurrentDepthMap())
                    {
                        if (_readerOptions.AllowTrailingCommas)
                        {
                            EndMap();
                            goto Done;
                        }
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeArrayEnd);
                    }
                    if (marker == RdnConstants.CloseParen && IsCurrentDepthTuple())
                    {
                        if (_readerOptions.AllowTrailingCommas)
                        {
                            EndTuple();
                            goto Done;
                        }
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeArrayEnd);
                    }

                    return ConsumeValue(marker) ? ConsumeTokenResult.Success : ConsumeTokenResult.NotEnoughDataRollBackState;
                }
            }
            else if (marker == RdnConstants.CloseBrace)
            {
                if (_inObject)
                {
                    EndObject();
                }
                else if (IsCurrentDepthSet())
                {
                    EndSet();
                }
                else if (IsCurrentDepthMap())
                {
                    EndMap();
                }
                else
                {
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.MismatchedObjectArray, RdnConstants.CloseBrace);
                }
            }
            else if (marker == RdnConstants.CloseBracket)
            {
                EndArray();
            }
            else if (marker == RdnConstants.CloseParen)
            {
                EndTuple();
            }
            else
            {
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.FoundInvalidCharacter, marker);
            }

        Done:
            return ConsumeTokenResult.Success;
        IncompleteNoRollback:
            return ConsumeTokenResult.IncompleteNoRollBackNecessary;
        IncompleteRollback:
            return ConsumeTokenResult.NotEnoughDataRollBackState;
        }

        private bool SkipComment()
        {
            // Create local copy to avoid bounds checks.
            ReadOnlySpan<byte> localBuffer = _buffer.Slice(_consumed + 1);

            if (localBuffer.Length > 0)
            {
                byte marker = localBuffer[0];
                if (marker == RdnConstants.Slash)
                {
                    return SkipSingleLineComment(localBuffer.Slice(1), out _);
                }
                else if (marker == RdnConstants.Asterisk)
                {
                    return SkipMultiLineComment(localBuffer.Slice(1), out _);
                }
                else
                {
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, RdnConstants.Slash);
                }
            }

            if (IsLastSpan)
            {
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, RdnConstants.Slash);
            }
            return false;
        }

        private bool SkipSingleLineComment(ReadOnlySpan<byte> localBuffer, out int idx)
        {
            idx = FindLineSeparator(localBuffer);
            int toConsume;
            if (idx != -1)
            {
                toConsume = idx;
                if (localBuffer[idx] == RdnConstants.LineFeed)
                {
                    goto EndOfComment;
                }

                // If we are here, we have definintely found a \r. So now to check if \n follows.
                Debug.Assert(localBuffer[idx] == RdnConstants.CarriageReturn);

                if (idx < localBuffer.Length - 1)
                {
                    if (localBuffer[idx + 1] == RdnConstants.LineFeed)
                    {
                        toConsume++;
                    }

                    goto EndOfComment;
                }

                if (IsLastSpan)
                {
                    goto EndOfComment;
                }
                else
                {
                    // there might be LF in the next segment
                    return false;
                }
            }

            if (IsLastSpan)
            {
                idx = localBuffer.Length;
                toConsume = idx;
                // Assume everything on this line is a comment and there is no more data.
                _bytePositionInLine += 2 + localBuffer.Length;
                goto Done;
            }
            else
            {
                return false;
            }

        EndOfComment:
            toConsume++;
            _bytePositionInLine = 0;
            _lineNumber++;

        Done:
            _consumed += 2 + toConsume;
            return true;
        }

        private int FindLineSeparator(ReadOnlySpan<byte> localBuffer)
        {
            int totalIdx = 0;
            while (true)
            {
                int idx = localBuffer.IndexOfAny(RdnConstants.LineFeed, RdnConstants.CarriageReturn, RdnConstants.StartingByteOfNonStandardSeparator);

                if (idx == -1)
                {
                    return -1;
                }

                totalIdx += idx;

                if (localBuffer[idx] != RdnConstants.StartingByteOfNonStandardSeparator)
                {
                    return totalIdx;
                }

                totalIdx++;
                localBuffer = localBuffer.Slice(idx + 1);

                ThrowOnDangerousLineSeparator(localBuffer);
            }
        }

        // assumes first byte (RdnConstants.StartingByteOfNonStandardSeparator) is already read
        private void ThrowOnDangerousLineSeparator(ReadOnlySpan<byte> localBuffer)
        {
            // \u2028 and \u2029 are considered respectively line and paragraph separators
            // UTF-8 representation for them is E2, 80, A8/A9
            // we have already read E2, we need to check for remaining 2 bytes

            if (localBuffer.Length < 2)
            {
                return;
            }

            byte next = localBuffer[1];
            if (localBuffer[0] == 0x80 && (next == 0xA8 || next == 0xA9))
            {
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.UnexpectedEndOfLineSeparator);
            }
        }

        private bool SkipMultiLineComment(ReadOnlySpan<byte> localBuffer, out int idx)
        {
            idx = 0;
            while (true)
            {
                int foundIdx = localBuffer.Slice(idx).IndexOf(RdnConstants.Slash);
                if (foundIdx == -1)
                {
                    if (IsLastSpan)
                    {
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.EndOfCommentNotFound);
                    }
                    return false;
                }
                if (foundIdx != 0 && localBuffer[foundIdx + idx - 1] == RdnConstants.Asterisk)
                {
                    // foundIdx points just after '*' in the end-of-comment delimiter. Hence increment idx by one
                    // position less to make it point right before beginning of end-of-comment delimiter i.e. */
                    idx += foundIdx - 1;
                    break;
                }
                idx += foundIdx + 1;
            }

            // Consume the /* and */ characters that are part of the multi-line comment.
            // idx points right before the final '*' (which is right before the last '/'). Hence increment _consumed
            // by 4 to exclude the start/end-of-comment delimiters.
            _consumed += 4 + idx;

            (int newLines, int newLineIndex) = RdnReaderHelper.CountNewLines(localBuffer.Slice(0, idx));
            _lineNumber += newLines;
            if (newLineIndex != -1)
            {
                // newLineIndex points at last newline character and byte positions in the new line start
                // after that. Hence add 1 to skip the newline character.
                _bytePositionInLine = idx - newLineIndex + 1;
            }
            else
            {
                _bytePositionInLine += 4 + idx;
            }
            return true;
        }

        private bool ConsumeComment()
        {
            // Create local copy to avoid bounds checks.
            ReadOnlySpan<byte> localBuffer = _buffer.Slice(_consumed + 1);

            if (localBuffer.Length > 0)
            {
                byte marker = localBuffer[0];
                if (marker == RdnConstants.Slash)
                {
                    return ConsumeSingleLineComment(localBuffer.Slice(1), _consumed);
                }
                else if (marker == RdnConstants.Asterisk)
                {
                    return ConsumeMultiLineComment(localBuffer.Slice(1), _consumed);
                }
                else
                {
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.InvalidCharacterAtStartOfComment, marker);
                }
            }

            if (IsLastSpan)
            {
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.UnexpectedEndOfDataWhileReadingComment);
            }
            return false;
        }

        private bool ConsumeSingleLineComment(ReadOnlySpan<byte> localBuffer, int previousConsumed)
        {
            if (!SkipSingleLineComment(localBuffer, out int idx))
            {
                return false;
            }

            // Exclude the // at start of the comment. idx points right before the line separator
            // at the end of the comment.
            ValueSpan = _buffer.Slice(previousConsumed + 2, idx);
            if (_tokenType != RdnTokenType.Comment)
            {
                _previousTokenType = _tokenType;
            }
            _tokenType = RdnTokenType.Comment;
            return true;
        }

        private bool ConsumeMultiLineComment(ReadOnlySpan<byte> localBuffer, int previousConsumed)
        {
            if (!SkipMultiLineComment(localBuffer, out int idx))
            {
                return false;
            }

            // Exclude the /* at start of the comment. idx already points right before the terminal '*/'
            // for the end of multiline comment.
            ValueSpan = _buffer.Slice(previousConsumed + 2, idx);
            if (_tokenType != RdnTokenType.Comment)
            {
                _previousTokenType = _tokenType;
            }
            _tokenType = RdnTokenType.Comment;
            return true;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => $"TokenType = {DebugTokenType}, TokenStartIndex = {TokenStartIndex}, Consumed = {BytesConsumed}";

        // Using TokenType.ToString() (or {TokenType}) fails to render in the debug window. The
        // message "The runtime refused to evaluate the expression at this time." is shown. This
        // is a workaround until we root cause and fix the issue.
        private string DebugTokenType
            => TokenType switch
            {
                RdnTokenType.Comment => nameof(RdnTokenType.Comment),
                RdnTokenType.EndArray => nameof(RdnTokenType.EndArray),
                RdnTokenType.EndObject => nameof(RdnTokenType.EndObject),
                RdnTokenType.False => nameof(RdnTokenType.False),
                RdnTokenType.None => nameof(RdnTokenType.None),
                RdnTokenType.Null => nameof(RdnTokenType.Null),
                RdnTokenType.Number => nameof(RdnTokenType.Number),
                RdnTokenType.PropertyName => nameof(RdnTokenType.PropertyName),
                RdnTokenType.StartArray => nameof(RdnTokenType.StartArray),
                RdnTokenType.StartObject => nameof(RdnTokenType.StartObject),
                RdnTokenType.String => nameof(RdnTokenType.String),
                RdnTokenType.True => nameof(RdnTokenType.True),
                _ => ((byte)TokenType).ToString()
            };

        private ReadOnlySpan<byte> GetUnescapedSpan()
        {
            ReadOnlySpan<byte> span = HasValueSequence ? ValueSequence.ToArray() : ValueSpan;
            if (ValueIsEscaped)
            {
                span = RdnReaderHelper.GetUnescaped(span);
            }

            return span;
        }
    }
}
