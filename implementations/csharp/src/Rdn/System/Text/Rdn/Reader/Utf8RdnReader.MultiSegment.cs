// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Rdn
{
    public ref partial struct Utf8RdnReader
    {
        /// <summary>
        /// Constructs a new <see cref="Utf8RdnReader"/> instance.
        /// </summary>
        /// <param name="rdnData">The ReadOnlySequence&lt;byte&gt; containing the UTF-8 encoded RDN text to process.</param>
        /// <param name="isFinalBlock">True when the input span contains the entire data to process.
        /// Set to false only if it is known that the input span contains partial data with more data to follow.</param>
        /// <param name="state">If this is the first call to the ctor, pass in a default state. Otherwise,
        /// capture the state from the previous instance of the <see cref="Utf8RdnReader"/> and pass that back.</param>
        /// <remarks>
        /// Since this type is a ref struct, it is a stack-only type and all the limitations of ref structs apply to it.
        /// This is the reason why the ctor accepts a <see cref="RdnReaderState"/>.
        /// </remarks>
        public Utf8RdnReader(ReadOnlySequence<byte> rdnData, bool isFinalBlock, RdnReaderState state)
        {
            _buffer = rdnData.First.Span;

            _isFinalBlock = isFinalBlock;
            _isInputSequence = true;

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

            ValueSpan = ReadOnlySpan<byte>.Empty;

            _sequence = rdnData;
            HasValueSequence = false;
            ValueSequence = ReadOnlySequence<byte>.Empty;

            if (rdnData.IsSingleSegment)
            {
                _nextPosition = default;
                _currentPosition = rdnData.Start;
                _isLastSegment = isFinalBlock;
                _isMultiSegment = false;
            }
            else
            {
                _currentPosition = rdnData.Start;
                _nextPosition = _currentPosition;

                bool firstSegmentIsEmpty = _buffer.Length == 0;
                if (firstSegmentIsEmpty)
                {
                    // Once we find a non-empty segment, we need to set current position to it.
                    // Therefore, track the next position in a copy before it gets advanced to the next segment.
                    SequencePosition previousNextPosition = _nextPosition;
                    while (rdnData.TryGet(ref _nextPosition, out ReadOnlyMemory<byte> memory, advance: true))
                    {
                        // _currentPosition should point to the segment right befor the segment that _nextPosition points to.
                        _currentPosition = previousNextPosition;
                        if (memory.Length != 0)
                        {
                            _buffer = memory.Span;
                            break;
                        }
                        previousNextPosition = _nextPosition;
                    }
                }

                // If firstSegmentIsEmpty is true,
                //    only check if we have reached the last segment but do not advance _nextPosition. The while loop above already advanced it.
                //    Otherwise, we would end up skipping a segment (i.e. advance = false).
                // If firstSegmentIsEmpty is false,
                //    make sure to advance _nextPosition so that it is no longer the same as _currentPosition (i.e. advance = true).
                _isLastSegment = !rdnData.TryGet(ref _nextPosition, out _, advance: !firstSegmentIsEmpty) && isFinalBlock; // Don't re-order to avoid short-circuiting

                Debug.Assert(!_nextPosition.Equals(_currentPosition));

                _isMultiSegment = true;
            }
        }

        /// <summary>
        /// Constructs a new <see cref="Utf8RdnReader"/> instance.
        /// </summary>
        /// <param name="rdnData">The ReadOnlySequence&lt;byte&gt; containing the UTF-8 encoded RDN text to process.</param>
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
        public Utf8RdnReader(ReadOnlySequence<byte> rdnData, RdnReaderOptions options = default)
            : this(rdnData, isFinalBlock: true, new RdnReaderState(options))
        {
        }

        private bool ReadMultiSegment()
        {
            bool retVal = false;
            HasValueSequence = false;
            ValueIsEscaped = false;
            ValueSpan = default;
            ValueSequence = default;

            if (!HasMoreDataMultiSegment())
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
                SkipWhiteSpaceMultiSegment();
                if (!HasMoreDataMultiSegment())
                {
                    goto Done;
                }
                first = _buffer[_consumed];
            }

            TokenStartIndex = BytesConsumed;

            if (_tokenType == RdnTokenType.None)
            {
                goto ReadFirstToken;
            }

            if (first == RdnConstants.Slash)
            {
                // In value position, route to ConsumeValueMultiSegment which handles regex/comment disambiguation
                if (_tokenType is RdnTokenType.StartArray or RdnTokenType.StartSet or RdnTokenType.StartMap or RdnTokenType.PropertyName)
                {
                    retVal = ConsumeValueMultiSegment(first);
                    goto Done;
                }
                // In structural position (StartObject, after values), route to comment handling
                retVal = ConsumeNextTokenOrRollbackMultiSegment(first);
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

                    long prevTotalConsumed = _totalConsumed;
                    int prevConsumed = _consumed;
                    long prevPosition = _bytePositionInLine;
                    long prevLineNumber = _lineNumber;
                    SequencePosition copy = _currentPosition;
                    retVal = ConsumePropertyNameMultiSegment();
                    if (!retVal)
                    {
                        // roll back potential changes
                        _consumed = prevConsumed;
                        _tokenType = RdnTokenType.StartObject;
                        _bytePositionInLine = prevPosition;
                        _lineNumber = prevLineNumber;
                        _totalConsumed = prevTotalConsumed;
                        _currentPosition = copy;
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
                    retVal = ConsumeValueMultiSegment(first);
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
                    retVal = ConsumeValueMultiSegment(first);
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
                    retVal = ConsumeValueMultiSegment(first);
                    goto Done;
                }
            }
            else if (_tokenType == RdnTokenType.PropertyName)
            {
                retVal = ConsumeValueMultiSegment(first);
                goto Done;
            }
            else
            {
                retVal = ConsumeNextTokenOrRollbackMultiSegment(first);
                goto Done;
            }

            retVal = true;

        Done:
            return retVal;

        ReadFirstToken:
            retVal = ReadFirstTokenMultiSegment(first);
            goto Done;
        }

        private bool ValidateStateAtEndOfData()
        {
            Debug.Assert(_isNotPrimitive && IsLastSpan);

            if (_bitStack.CurrentDepth != 0)
            {
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ZeroDepthAtEnd);
            }

            if (_readerOptions.CommentHandling == RdnCommentHandling.Allow && _tokenType == RdnTokenType.Comment)
            {
                return false;
            }

            if (_tokenType != RdnTokenType.EndArray && _tokenType != RdnTokenType.EndObject && _tokenType != RdnTokenType.EndSet && _tokenType != RdnTokenType.EndMap)
            {
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.InvalidEndOfRdnNonPrimitive);
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasMoreDataMultiSegment()
        {
            if (_consumed >= (uint)_buffer.Length)
            {
                if (_isNotPrimitive && IsLastSpan)
                {
                    if (!ValidateStateAtEndOfData())
                    {
                        return false;
                    }
                }

                if (!GetNextSpan())
                {
                    if (_isNotPrimitive && IsLastSpan)
                    {
                        ValidateStateAtEndOfData();
                    }
                    return false;
                }
            }
            return true;
        }

        // Unlike the parameter-less overload of HasMoreData, if there is no more data when this method is called, we know the RDN input is invalid.
        // This is because, this method is only called after a ',' (i.e. we expect a value/property name) or after
        // a property name, which means it must be followed by a value.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasMoreDataMultiSegment(ExceptionResource resource)
        {
            if (_consumed >= (uint)_buffer.Length)
            {
                if (IsLastSpan)
                {
                    ThrowHelper.ThrowRdnReaderException(ref this, resource);
                }
                if (!GetNextSpan())
                {
                    if (IsLastSpan)
                    {
                        ThrowHelper.ThrowRdnReaderException(ref this, resource);
                    }
                    return false;
                }
            }
            return true;
        }

        private bool GetNextSpan()
        {
            ReadOnlyMemory<byte> memory;
            while (true)
            {
                Debug.Assert(!_isMultiSegment || _currentPosition.GetObject() != null);
                SequencePosition copy = _currentPosition;
                _currentPosition = _nextPosition;
                bool noMoreData = !_sequence.TryGet(ref _nextPosition, out memory, advance: true);
                if (noMoreData)
                {
                    _currentPosition = copy;
                    _isLastSegment = true;
                    return false;
                }
                if (memory.Length != 0)
                {
                    break;
                }
                // _currentPosition needs to point to last non-empty segment
                // Since memory.Length == 0, we need to revert back to previous.
                _currentPosition = copy;
                Debug.Assert(!_isMultiSegment || _currentPosition.GetObject() != null);
            }

            if (_isFinalBlock)
            {
                _isLastSegment = !_sequence.TryGet(ref _nextPosition, out _, advance: false);
            }

            _buffer = memory.Span;
            _totalConsumed += _consumed;
            _consumed = 0;

            return true;
        }

        private bool ReadFirstTokenMultiSegment(byte first)
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
                if (RdnHelpers.IsDigit(first) || first == '-')
                {
                    if (first == '-')
                    {
                        int nextIdx = _consumed + 1;
                        if (nextIdx < _buffer.Length && _buffer[nextIdx] == (byte)'I')
                        {
                            if (!ConsumeLiteralMultiSegment(RdnConstants.NegativeInfinityValue, RdnTokenType.Number))
                            {
                                return false;
                            }
                            goto DoneReadingFirstTokenMultiSegment;
                        }
                        if (nextIdx >= _buffer.Length && !IsLastSpan)
                        {
                            return false; // need more data
                        }
                    }
                    if (!TryGetNumberMultiSegment(_buffer.Slice(_consumed), out int numberOfBytes, out bool isBigInteger2))
                    {
                        return false;
                    }
                    _tokenType = isBigInteger2 ? RdnTokenType.RdnBigInteger : RdnTokenType.Number;
                    _consumed += numberOfBytes;
                }
                else if (!ConsumeValueMultiSegment(first))
                {
                    return false;
                }

            DoneReadingFirstTokenMultiSegment:
                _isNotPrimitive = _tokenType is RdnTokenType.StartObject or RdnTokenType.StartArray or RdnTokenType.StartSet or RdnTokenType.StartMap;
                // Intentionally fall out of the if-block to return true
            }
            return true;
        }

        private void SkipWhiteSpaceMultiSegment()
        {
            while (true)
            {
                SkipWhiteSpace();

                if (_consumed < _buffer.Length)
                {
                    break;
                }

                if (!GetNextSpan())
                {
                    break;
                }
            }
        }

        /// <summary>
        /// This method contains the logic for processing the next value token and determining
        /// what type of data it is.
        /// </summary>
        private bool ConsumeValueMultiSegment(byte marker)
        {
            while (true)
            {
                Debug.Assert((_trailingCommaBeforeComment && _readerOptions.CommentHandling == RdnCommentHandling.Allow) || !_trailingCommaBeforeComment);
                Debug.Assert((_trailingCommaBeforeComment && marker != RdnConstants.Slash) || !_trailingCommaBeforeComment);
                _trailingCommaBeforeComment = false;

                if (marker == RdnConstants.Quote)
                {
                    return ConsumeStringMultiSegment();
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
                            return ConsumeLiteralMultiSegment(RdnConstants.NegativeInfinityValue, RdnTokenType.Number);
                        }
                        if (nextIdx >= _buffer.Length && !IsLastSpan)
                        {
                            return false; // need more data
                        }
                    }
                    return ConsumeNumberMultiSegment();
                }
                else if (marker == (byte)'N')
                {
                    return ConsumeLiteralMultiSegment(RdnConstants.NaNValue, RdnTokenType.Number);
                }
                else if (marker == (byte)'I')
                {
                    return ConsumeLiteralMultiSegment(RdnConstants.PositiveInfinityValue, RdnTokenType.Number);
                }
                else if (marker == 'f')
                {
                    return ConsumeLiteralMultiSegment(RdnConstants.FalseValue, RdnTokenType.False);
                }
                else if (marker == 't')
                {
                    return ConsumeLiteralMultiSegment(RdnConstants.TrueValue, RdnTokenType.True);
                }
                else if (marker == 'n')
                {
                    return ConsumeLiteralMultiSegment(RdnConstants.NullValue, RdnTokenType.Null);
                }
                else if (marker == RdnConstants.AtSign)
                {
                    return ConsumeRdnLiteralMultiSegment();
                }
                else if (marker == RdnConstants.LetterS)
                {
                    return ConsumeExplicitSet();
                }
                else if (marker == RdnConstants.LetterM)
                {
                    return ConsumeExplicitMap();
                }
                else if (marker == RdnConstants.Slash)
                {
                    // Peek ahead: if next char is / or *, it may be a comment
                    int nextIdx = _consumed + 1;
                    bool hasNext = nextIdx < _buffer.Length;
                    if (hasNext)
                    {
                        byte next = _buffer[nextIdx];
                        if ((next == RdnConstants.Slash || next == RdnConstants.Asterisk) && _readerOptions.CommentHandling != RdnCommentHandling.Disallow)
                        {
                            if (_readerOptions.CommentHandling == RdnCommentHandling.Allow)
                            {
                                SequencePosition copy = _currentPosition;
                                if (!SkipOrConsumeCommentMultiSegmentWithRollback())
                                {
                                    _currentPosition = copy;
                                    return false;
                                }
                                return true;
                            }
                            else
                            {
                                Debug.Assert(_readerOptions.CommentHandling == RdnCommentHandling.Skip);
                                SequencePosition copy = _currentPosition;
                                if (SkipCommentMultiSegment(out _))
                                {
                                    if (_consumed >= (uint)_buffer.Length)
                                    {
                                        if (_isNotPrimitive && IsLastSpan && _tokenType != RdnTokenType.EndArray && _tokenType != RdnTokenType.EndObject && _tokenType != RdnTokenType.EndSet && _tokenType != RdnTokenType.EndMap)
                                        {
                                            ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.InvalidEndOfRdnNonPrimitive);
                                        }
                                        if (!GetNextSpan())
                                        {
                                            if (_isNotPrimitive && IsLastSpan && _tokenType != RdnTokenType.EndArray && _tokenType != RdnTokenType.EndObject && _tokenType != RdnTokenType.EndSet && _tokenType != RdnTokenType.EndMap)
                                            {
                                                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.InvalidEndOfRdnNonPrimitive);
                                            }
                                            _currentPosition = copy;
                                            return false;
                                        }
                                    }

                                    marker = _buffer[_consumed];

                                    if (marker <= RdnConstants.Space)
                                    {
                                        SkipWhiteSpaceMultiSegment();
                                        if (!HasMoreDataMultiSegment())
                                        {
                                            _currentPosition = copy;
                                            return false;
                                        }
                                        marker = _buffer[_consumed];
                                    }

                                    TokenStartIndex = BytesConsumed;
                                    continue;
                                }
                                _currentPosition = copy;
                                return false;
                            }
                        }
                    }
                    else if (!_isFinalBlock)
                    {
                        return false; // Need more data to distinguish regex from comment
                    }
                    // Not a comment → must be regex
                    return ConsumeRegexMultiSegment();
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
                                SequencePosition copy = _currentPosition;
                                if (!SkipOrConsumeCommentMultiSegmentWithRollback())
                                {
                                    _currentPosition = copy;
                                    return false;
                                }
                                return true;
                            }
                            break;
                        default:
                            Debug.Assert(_readerOptions.CommentHandling == RdnCommentHandling.Skip);
                            if (marker == RdnConstants.Slash)
                            {
                                SequencePosition copy = _currentPosition;
                                if (SkipCommentMultiSegment(out _))
                                {
                                    if (_consumed >= (uint)_buffer.Length)
                                    {
                                        if (_isNotPrimitive && IsLastSpan && _tokenType != RdnTokenType.EndArray && _tokenType != RdnTokenType.EndObject && _tokenType != RdnTokenType.EndSet && _tokenType != RdnTokenType.EndMap)
                                        {
                                            ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.InvalidEndOfRdnNonPrimitive);
                                        }
                                        if (!GetNextSpan())
                                        {
                                            if (_isNotPrimitive && IsLastSpan && _tokenType != RdnTokenType.EndArray && _tokenType != RdnTokenType.EndObject && _tokenType != RdnTokenType.EndSet && _tokenType != RdnTokenType.EndMap)
                                            {
                                                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.InvalidEndOfRdnNonPrimitive);
                                            }
                                            _currentPosition = copy;
                                            return false;
                                        }
                                    }

                                    marker = _buffer[_consumed];

                                    // This check is done as an optimization to avoid calling SkipWhiteSpace when not necessary.
                                    if (marker <= RdnConstants.Space)
                                    {
                                        SkipWhiteSpaceMultiSegment();
                                        if (!HasMoreDataMultiSegment())
                                        {
                                            _currentPosition = copy;
                                            return false;
                                        }
                                        marker = _buffer[_consumed];
                                    }

                                    TokenStartIndex = BytesConsumed;

                                    // Skip comments and consume the actual RDN value.
                                    continue;
                                }
                                _currentPosition = copy;
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
        private bool ConsumeLiteralMultiSegment(ReadOnlySpan<byte> literal, RdnTokenType tokenType)
        {
            ReadOnlySpan<byte> span = _buffer.Slice(_consumed);
            Debug.Assert(span.Length > 0);
            Debug.Assert(span[0] == 'n' || span[0] == 't' || span[0] == 'f' || span[0] == 'N' || span[0] == 'I' || span[0] == '-');

            int consumed = literal.Length;

            if (!span.StartsWith(literal))
            {
                int prevConsumed = _consumed;
                if (CheckLiteralMultiSegment(span, literal, out consumed))
                {
                    goto Done;
                }
                _consumed = prevConsumed;
                return false;
            }

            ValueSpan = span.Slice(0, literal.Length);
            HasValueSequence = false;
        Done:
            _tokenType = tokenType;
            _consumed += consumed;
            _bytePositionInLine += consumed;
            return true;
        }

        private bool CheckLiteralMultiSegment(ReadOnlySpan<byte> span, ReadOnlySpan<byte> literal, out int consumed)
        {
            Debug.Assert(span.Length > 0 && span[0] == literal[0] && literal.Length <= RdnConstants.MaximumLiteralLength);

            Span<byte> readSoFar = stackalloc byte[RdnConstants.MaximumLiteralLength];
            int written = 0;

            long prevTotalConsumed = _totalConsumed;
            SequencePosition copy = _currentPosition;
            if (span.Length >= literal.Length || IsLastSpan)
            {
                _bytePositionInLine += FindMismatch(span, literal);

                int amountToWrite = AmountToWrite(span, _bytePositionInLine, readSoFar, written);
                span.Slice(0, amountToWrite).CopyTo(readSoFar);
                written += amountToWrite;
                goto Throw;
            }
            else
            {
                if (!literal.StartsWith(span))
                {
                    _bytePositionInLine += FindMismatch(span, literal);
                    int amountToWrite = AmountToWrite(span, _bytePositionInLine, readSoFar, written);
                    span.Slice(0, amountToWrite).CopyTo(readSoFar);
                    written += amountToWrite;
                    goto Throw;
                }

                ReadOnlySpan<byte> leftToMatch = literal.Slice(span.Length);

                SequencePosition startPosition = _currentPosition;
                int startConsumed = _consumed;
                int alreadyMatched = literal.Length - leftToMatch.Length;
                while (true)
                {
                    _totalConsumed += alreadyMatched;
                    _bytePositionInLine += alreadyMatched;
                    if (!GetNextSpan())
                    {
                        _totalConsumed = prevTotalConsumed;
                        consumed = default;
                        _currentPosition = copy;
                        if (IsLastSpan)
                        {
                            goto Throw;
                        }
                        return false;
                    }

                    int amountToWrite = Math.Min(span.Length, readSoFar.Length - written);
                    span.Slice(0, amountToWrite).CopyTo(readSoFar.Slice(written));
                    written += amountToWrite;

                    span = _buffer;

                    if (span.StartsWith(leftToMatch))
                    {
                        HasValueSequence = true;
                        SequencePosition start = new SequencePosition(startPosition.GetObject(), startPosition.GetInteger() + startConsumed);
                        SequencePosition end = new SequencePosition(_currentPosition.GetObject(), _currentPosition.GetInteger() + leftToMatch.Length);
                        ValueSequence = _sequence.Slice(start, end);
                        consumed = leftToMatch.Length;
                        return true;
                    }

                    if (!leftToMatch.StartsWith(span))
                    {
                        _bytePositionInLine += FindMismatch(span, leftToMatch);

                        amountToWrite = AmountToWrite(span, _bytePositionInLine, readSoFar, written);
                        span.Slice(0, amountToWrite).CopyTo(readSoFar.Slice(written));
                        written += amountToWrite;

                        goto Throw;
                    }

                    leftToMatch = leftToMatch.Slice(span.Length);
                    alreadyMatched = span.Length;
                }
            }

            static int AmountToWrite(ReadOnlySpan<byte> span, long bytePositionInLine, ReadOnlySpan<byte> readSoFar, int written)
            {
                return Math.Min(
                    readSoFar.Length - written,
                    Math.Min(span.Length, (int)bytePositionInLine + 1));
            }
        Throw:
            _totalConsumed = prevTotalConsumed;
            consumed = default;
            _currentPosition = copy;
            throw GetInvalidLiteralMultiSegment(readSoFar.Slice(0, written).ToArray());
        }

        private static int FindMismatch(ReadOnlySpan<byte> span, ReadOnlySpan<byte> literal)
        {
            Debug.Assert(span.Length > 0);

            int indexOfFirstMismatch;

#if NET
            indexOfFirstMismatch = span.CommonPrefixLength(literal);
#else
            int minLength = Math.Min(span.Length, literal.Length);
            for (indexOfFirstMismatch = 0; indexOfFirstMismatch < minLength; indexOfFirstMismatch++)
            {
                if (span[indexOfFirstMismatch] != literal[indexOfFirstMismatch])
                {
                    break;
                }
            }
#endif

            Debug.Assert(indexOfFirstMismatch >= 0 && indexOfFirstMismatch < literal.Length);

            return indexOfFirstMismatch;
        }

        private RdnException GetInvalidLiteralMultiSegment(ReadOnlySpan<byte> span)
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
            return ThrowHelper.GetRdnReaderException(ref this, resource, nextByte: default, bytes: span);
        }

        private bool ConsumeNumberMultiSegment()
        {
            if (!TryGetNumberMultiSegment(_buffer.Slice(_consumed), out int consumed, out bool isBigInteger))
            {
                return false;
            }

            _tokenType = isBigInteger ? RdnTokenType.RdnBigInteger : RdnTokenType.Number;
            _consumed += consumed;

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

        private bool ConsumePropertyNameMultiSegment()
        {
            _trailingCommaBeforeComment = false;

            if (!ConsumeStringMultiSegment())
            {
                return false;
            }

            if (!HasMoreDataMultiSegment(ExceptionResource.ExpectedValueAfterPropertyNameNotFound))
            {
                return false;
            }

            byte first = _buffer[_consumed];

            // This check is done as an optimization to avoid calling SkipWhiteSpace when not necessary.
            // We do not validate if 'first' is an invalid RDN byte here (such as control characters).
            // Those cases are captured below where we only accept ':'.
            if (first <= RdnConstants.Space)
            {
                SkipWhiteSpaceMultiSegment();
                if (!HasMoreDataMultiSegment(ExceptionResource.ExpectedValueAfterPropertyNameNotFound))
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

        private bool ConsumeStringMultiSegment()
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
                    HasValueSequence = false;
                    ValueIsEscaped = false;
                    _tokenType = RdnTokenType.String;
                    _consumed += idx + 2;
                    return true;
                }
                else
                {
                    return ConsumeStringAndValidateMultiSegment(localBuffer, idx);
                }
            }
            else
            {
                if (IsLastSpan)
                {
                    _bytePositionInLine += localBuffer.Length + 1;  // Account for the start quote
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.EndOfStringNotFound);
                }
                return ConsumeStringNextSegment();
            }
        }

        private bool ConsumeStringNextSegment()
        {
            PartialStateForRollback rollBackState = CaptureState();

            SequencePosition end;
            HasValueSequence = true;
            int leftOver = _buffer.Length - _consumed;

            while (true)
            {
                if (!GetNextSpan())
                {
                    if (IsLastSpan)
                    {
                        _bytePositionInLine += leftOver;
                        RollBackState(rollBackState, isError: true);
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.EndOfStringNotFound);
                    }
                    RollBackState(rollBackState);
                    return false;
                }

                //Create local copy to avoid bounds checks.
                ReadOnlySpan<byte> localBuffer = _buffer;
                int idx = localBuffer.IndexOfQuoteOrAnyControlOrBackSlash();

                if (idx >= 0)
                {
                    byte foundByte = localBuffer[idx];
                    if (foundByte == RdnConstants.Quote)
                    {
                        end = new SequencePosition(_currentPosition.GetObject(), _currentPosition.GetInteger() + idx);
                        _bytePositionInLine += leftOver + idx + 1;  // Add 1 for the end quote of the string.
                        _totalConsumed += leftOver;
                        _consumed = idx + 1;    // Add 1 for the end quote of the string.
                        ValueIsEscaped = false;
                        break;
                    }
                    else
                    {
                        _bytePositionInLine += leftOver + idx;
                        ValueIsEscaped = true;

                        bool nextCharEscaped = false;
                        while (true)
                        {
                        StartOfLoop:
                            for (; idx < localBuffer.Length; idx++)
                            {
                                byte currentByte = localBuffer[idx];
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
                                        RollBackState(rollBackState, isError: true);
                                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.InvalidCharacterAfterEscapeWithinString, currentByte);
                                    }

                                    if (currentByte == 'u')
                                    {
                                        // Expecting 4 hex digits to follow the escaped 'u'
                                        _bytePositionInLine++;  // move past the 'u'

                                        int numberOfHexDigits = 0;
                                        int j = idx + 1;
                                        while (true)
                                        {
                                            for (; j < localBuffer.Length; j++)
                                            {
                                                byte nextByte = localBuffer[j];
                                                if (!RdnReaderHelper.IsHexDigit(nextByte))
                                                {
                                                    RollBackState(rollBackState, isError: true);
                                                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.InvalidHexCharacterWithinString, nextByte);
                                                }
                                                numberOfHexDigits++;
                                                _bytePositionInLine++;
                                                if (numberOfHexDigits >= 4)
                                                {
                                                    nextCharEscaped = false;
                                                    idx = j + 1; // Skip the 4 hex digits, the for loop accounts for idx incrementing past the 'u'
                                                    goto StartOfLoop;
                                                }
                                            }

                                            if (!GetNextSpan())
                                            {
                                                if (IsLastSpan)
                                                {
                                                    RollBackState(rollBackState, isError: true);
                                                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.EndOfStringNotFound);
                                                }

                                                // We found less than 4 hex digits.
                                                RollBackState(rollBackState);
                                                return false;
                                            }

                                            _totalConsumed += localBuffer.Length;

                                            localBuffer = _buffer;
                                            j = 0;
                                        }
                                    }
                                    nextCharEscaped = false;
                                }
                                else if (currentByte < RdnConstants.Space)
                                {
                                    RollBackState(rollBackState, isError: true);
                                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.InvalidCharacterWithinString, currentByte);
                                }

                                _bytePositionInLine++;
                            }

                            if (!GetNextSpan())
                            {
                                if (IsLastSpan)
                                {
                                    RollBackState(rollBackState, isError: true);
                                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.EndOfStringNotFound);
                                }
                                RollBackState(rollBackState);
                                return false;
                            }

                            _totalConsumed += localBuffer.Length;
                            localBuffer = _buffer;
                            idx = 0;
                        }

                    Done:
                        _bytePositionInLine++;  // Add 1 for the end quote of the string.
                        _consumed = idx + 1;    // Add 1 for the end quote of the string.
                        _totalConsumed += leftOver;
                        end = new SequencePosition(_currentPosition.GetObject(), _currentPosition.GetInteger() + idx);
                        break;
                    }
                }

                _totalConsumed += localBuffer.Length;
                _bytePositionInLine += localBuffer.Length;
            }

            SequencePosition start = rollBackState.GetStartPosition(offset: 1); // Offset for the starting quote
            ValueSequence = _sequence.Slice(start, end);
            _tokenType = RdnTokenType.String;
            return true;
        }

        // Found a backslash or control characters which are considered invalid within a string.
        // Search through the rest of the string one byte at a time.
        // https://tools.ietf.org/html/rfc8259#section-7
        private bool ConsumeStringAndValidateMultiSegment(ReadOnlySpan<byte> data, int idx)
        {
            Debug.Assert(idx >= 0 && idx < data.Length);
            Debug.Assert(data[idx] != RdnConstants.Quote);
            Debug.Assert(data[idx] == RdnConstants.BackSlash || data[idx] < RdnConstants.Space);

            PartialStateForRollback rollBackState = CaptureState();

            SequencePosition end;
            HasValueSequence = false;
            int leftOverFromConsumed = _buffer.Length - _consumed;

            _bytePositionInLine += idx + 1; // Add 1 for the first quote

            bool nextCharEscaped = false;
            while (true)
            {
            StartOfLoop:
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
                            RollBackState(rollBackState, isError: true);
                            ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.InvalidCharacterAfterEscapeWithinString, currentByte);
                        }

                        if (currentByte == 'u')
                        {
                            // Expecting 4 hex digits to follow the escaped 'u'
                            _bytePositionInLine++;  // move past the 'u'

                            int numberOfHexDigits = 0;
                            int j = idx + 1;
                            while (true)
                            {
                                for (; j < data.Length; j++)
                                {
                                    byte nextByte = data[j];
                                    if (!RdnReaderHelper.IsHexDigit(nextByte))
                                    {
                                        RollBackState(rollBackState, isError: true);
                                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.InvalidHexCharacterWithinString, nextByte);
                                    }
                                    numberOfHexDigits++;
                                    _bytePositionInLine++;
                                    if (numberOfHexDigits >= 4)
                                    {
                                        nextCharEscaped = false;
                                        idx = j + 1; // Skip the 4 hex digits, the for loop accounts for idx incrementing past the 'u'
                                        goto StartOfLoop;
                                    }
                                }

                                if (!GetNextSpan())
                                {
                                    if (IsLastSpan)
                                    {
                                        RollBackState(rollBackState, isError: true);
                                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.EndOfStringNotFound);
                                    }

                                    // We found less than 4 hex digits.
                                    RollBackState(rollBackState);
                                    return false;
                                }

                                // Do not add the left over for the first segment to total consumed
                                if (HasValueSequence)
                                {
                                    _totalConsumed += data.Length;
                                }

                                data = _buffer;
                                j = 0;
                                HasValueSequence = true;
                            }
                        }
                        nextCharEscaped = false;
                    }
                    else if (currentByte < RdnConstants.Space)
                    {
                        RollBackState(rollBackState, isError: true);
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.InvalidCharacterWithinString, currentByte);
                    }

                    _bytePositionInLine++;
                }

                if (!GetNextSpan())
                {
                    if (IsLastSpan)
                    {
                        RollBackState(rollBackState, isError: true);
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.EndOfStringNotFound);
                    }
                    RollBackState(rollBackState);
                    return false;
                }

                // Do not add the left over for the first segment to total consumed
                if (HasValueSequence)
                {
                    _totalConsumed += data.Length;
                }

                data = _buffer;
                idx = 0;
                HasValueSequence = true;
            }

        Done:
            if (HasValueSequence)
            {
                _bytePositionInLine++;  // Add 1 for the end quote of the string.
                _consumed = idx + 1;    // Add 1 for the end quote of the string.
                _totalConsumed += leftOverFromConsumed;
                end = new SequencePosition(_currentPosition.GetObject(), _currentPosition.GetInteger() + idx);
                SequencePosition start = rollBackState.GetStartPosition(offset: 1); // Offset for the starting quote
                ValueSequence = _sequence.Slice(start, end);
            }
            else
            {
                _bytePositionInLine++;  // Add 1 for the end quote
                _consumed += idx + 2;
                ValueSpan = data.Slice(0, idx);
            }

            ValueIsEscaped = true;
            _tokenType = RdnTokenType.String;
            return true;
        }

        private void RollBackState(scoped in PartialStateForRollback state, bool isError = false)
        {
            _totalConsumed = state._prevTotalConsumed;

            // Don't roll back byte position in line for invalid RDN since that is provided
            // to the user within the exception.
            if (!isError)
            {
                _bytePositionInLine = state._prevBytePositionInLine;
            }

            _consumed = state._prevConsumed;
            _currentPosition = state._prevCurrentPosition;
        }

        // https://tools.ietf.org/html/rfc7159#section-6
        private bool TryGetNumberMultiSegment(ReadOnlySpan<byte> data, out int consumed, out bool isBigInteger)
        {
            // TODO: https://github.com/dotnet/runtime/issues/27837
            Debug.Assert(data.Length > 0);

            PartialStateForRollback rollBackState = CaptureState();

            consumed = 0;
            isBigInteger = false;
            int i = 0;

            ConsumeNumberResult signResult = ConsumeNegativeSignMultiSegment(ref data, ref i, rollBackState);
            if (signResult == ConsumeNumberResult.NeedMoreData)
            {
                RollBackState(rollBackState);
                return false;
            }

            Debug.Assert(signResult == ConsumeNumberResult.OperationIncomplete);

            byte nextByte = data[i];
            Debug.Assert(nextByte >= '0' && nextByte <= '9');

            if (nextByte == '0')
            {
                ConsumeNumberResult result = ConsumeZeroMultiSegment(ref data, ref i, rollBackState);
                if (result == ConsumeNumberResult.NeedMoreData)
                {
                    RollBackState(rollBackState);
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
                    if (HasValueSequence)
                    {
                        SequencePosition start = rollBackState.GetStartPosition();
                        SequencePosition end = new SequencePosition(_currentPosition.GetObject(), _currentPosition.GetInteger() + i);
                        ValueSequence = _sequence.Slice(start, end);
                        consumed = i + 1;
                    }
                    else
                    {
                        ValueSpan = data.Slice(0, i);
                        consumed = i + 1;
                    }
                    _bytePositionInLine++;
                    return true;
                }
            }
            else
            {
                ConsumeNumberResult result = ConsumeIntegerDigitsMultiSegment(ref data, ref i);
                if (result == ConsumeNumberResult.NeedMoreData)
                {
                    RollBackState(rollBackState);
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
                    if (HasValueSequence)
                    {
                        SequencePosition start = rollBackState.GetStartPosition();
                        SequencePosition end = new SequencePosition(_currentPosition.GetObject(), _currentPosition.GetInteger() + i);
                        ValueSequence = _sequence.Slice(start, end);
                        consumed = i + 1;
                    }
                    else
                    {
                        ValueSpan = data.Slice(0, i);
                        consumed = i + 1;
                    }
                    _bytePositionInLine++;
                    return true;
                }

                if (nextByte != '.' && nextByte != 'E' && nextByte != 'e')
                {
                    RollBackState(rollBackState, isError: true);
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedEndOfDigitNotFound, nextByte);
                }
            }

            Debug.Assert(nextByte == '.' || nextByte == 'E' || nextByte == 'e');

            if (nextByte == '.')
            {
                i++;
                _bytePositionInLine++;
                ConsumeNumberResult result = ConsumeDecimalDigitsMultiSegment(ref data, ref i, rollBackState);
                if (result == ConsumeNumberResult.NeedMoreData)
                {
                    RollBackState(rollBackState);
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
                    RollBackState(rollBackState, isError: true);
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedNextDigitEValueNotFound, nextByte);
                }
            }

            Debug.Assert(nextByte == 'E' || nextByte == 'e');
            i++;
            _bytePositionInLine++;

            signResult = ConsumeSignMultiSegment(ref data, ref i, rollBackState);
            if (signResult == ConsumeNumberResult.NeedMoreData)
            {
                RollBackState(rollBackState);
                return false;
            }

            Debug.Assert(signResult == ConsumeNumberResult.OperationIncomplete);

            i++;
            _bytePositionInLine++;
            ConsumeNumberResult resultExponent = ConsumeIntegerDigitsMultiSegment(ref data, ref i);
            if (resultExponent == ConsumeNumberResult.NeedMoreData)
            {
                RollBackState(rollBackState);
                return false;
            }
            if (resultExponent == ConsumeNumberResult.Success)
            {
                goto Done;
            }

            Debug.Assert(resultExponent == ConsumeNumberResult.OperationIncomplete);

            RollBackState(rollBackState, isError: true);
            ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedEndOfDigitNotFound, data[i]);

        Done:
            if (HasValueSequence)
            {
                SequencePosition start = rollBackState.GetStartPosition();
                SequencePosition end = new SequencePosition(_currentPosition.GetObject(), _currentPosition.GetInteger() + i);
                ValueSequence = _sequence.Slice(start, end);
                consumed = i;
            }
            else
            {
                ValueSpan = data.Slice(0, i);
                consumed = i;
            }
            return true;
        }

        private ConsumeNumberResult ConsumeNegativeSignMultiSegment(ref ReadOnlySpan<byte> data, scoped ref int i, scoped in PartialStateForRollback rollBackState)
        {
            Debug.Assert(i == 0);
            byte nextByte = data[i];

            if (nextByte == '-')
            {
                i++;
                _bytePositionInLine++;
                if (i >= data.Length)
                {
                    if (IsLastSpan)
                    {
                        RollBackState(rollBackState, isError: true);
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.RequiredDigitNotFoundEndOfData);
                    }
                    if (!GetNextSpan())
                    {
                        if (IsLastSpan)
                        {
                            RollBackState(rollBackState, isError: true);
                            ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.RequiredDigitNotFoundEndOfData);
                        }
                        return ConsumeNumberResult.NeedMoreData;
                    }
                    Debug.Assert(i == 1);
                    _totalConsumed += i;
                    HasValueSequence = true;
                    i = 0;
                    data = _buffer;
                }

                nextByte = data[i];
                if (!RdnHelpers.IsDigit(nextByte))
                {
                    RollBackState(rollBackState, isError: true);
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.RequiredDigitNotFoundAfterSign, nextByte);
                }
            }
            return ConsumeNumberResult.OperationIncomplete;
        }

        private ConsumeNumberResult ConsumeZeroMultiSegment(ref ReadOnlySpan<byte> data, scoped ref int i, scoped in PartialStateForRollback rollBackState)
        {
            Debug.Assert(data[i] == (byte)'0');
            Debug.Assert(i == 0 || i == 1);
            i++;
            _bytePositionInLine++;
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

                if (!GetNextSpan())
                {
                    if (IsLastSpan)
                    {
                        return ConsumeNumberResult.Success;
                    }
                    return ConsumeNumberResult.NeedMoreData;
                }

                _totalConsumed += i;
                HasValueSequence = true;
                i = 0;
                data = _buffer;
                nextByte = data[i];
                if (RdnConstants.Delimiters.Contains(nextByte))
                {
                    return ConsumeNumberResult.Success;
                }
            }
            nextByte = data[i];
            if (nextByte != '.' && nextByte != 'E' && nextByte != 'e' && nextByte != 'n')
            {
                RollBackState(rollBackState, isError: true);
                ThrowHelper.ThrowRdnReaderException(ref this,
                    RdnHelpers.IsInRangeInclusive(nextByte, '0', '9') ? ExceptionResource.InvalidLeadingZeroInNumber : ExceptionResource.ExpectedEndOfDigitNotFound,
                    nextByte);
            }

            return ConsumeNumberResult.OperationIncomplete;
        }

        private ConsumeNumberResult ConsumeIntegerDigitsMultiSegment(ref ReadOnlySpan<byte> data, scoped ref int i)
        {
            byte nextByte = default;
            int counter = 0;
            for (; i < data.Length; i++)
            {
                nextByte = data[i];
                if (!RdnHelpers.IsDigit(nextByte))
                {
                    break;
                }
                counter++;
            }
            if (i >= data.Length)
            {
                if (IsLastSpan)
                {
                    // A payload containing a single value of integers (e.g. "12") is valid
                    // If we are dealing with multi-value RDN,
                    // ConsumeNumber will validate that we have a delimiter following the integer.
                    _bytePositionInLine += counter;
                    return ConsumeNumberResult.Success;
                }

                while (true)
                {
                    if (!GetNextSpan())
                    {
                        if (IsLastSpan)
                        {
                            _bytePositionInLine += counter;
                            return ConsumeNumberResult.Success;
                        }
                        return ConsumeNumberResult.NeedMoreData;
                    }

                    _totalConsumed += i;
                    _bytePositionInLine += counter;
                    counter = 0;
                    HasValueSequence = true;
                    i = 0;
                    data = _buffer;
                    for (; i < data.Length; i++)
                    {
                        nextByte = data[i];
                        if (!RdnHelpers.IsDigit(nextByte))
                        {
                            break;
                        }
                    }
                    _bytePositionInLine += i;
                    if (i >= data.Length)
                    {
                        if (IsLastSpan)
                        {
                            return ConsumeNumberResult.Success;
                        }
                    }
                    else
                    {
                        break;
                    }
                }

            }
            else
            {
                _bytePositionInLine += counter;
            }

            if (RdnConstants.Delimiters.Contains(nextByte))
            {
                return ConsumeNumberResult.Success;
            }

            return ConsumeNumberResult.OperationIncomplete;
        }

        private ConsumeNumberResult ConsumeDecimalDigitsMultiSegment(ref ReadOnlySpan<byte> data, scoped ref int i, scoped in PartialStateForRollback rollBackState)
        {
            if (i >= data.Length)
            {
                if (IsLastSpan)
                {
                    RollBackState(rollBackState, isError: true);
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.RequiredDigitNotFoundEndOfData);
                }
                if (!GetNextSpan())
                {
                    if (IsLastSpan)
                    {
                        RollBackState(rollBackState, isError: true);
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.RequiredDigitNotFoundEndOfData);
                    }
                    return ConsumeNumberResult.NeedMoreData;
                }
                _totalConsumed += i;
                HasValueSequence = true;
                i = 0;
                data = _buffer;
            }
            byte nextByte = data[i];
            if (!RdnHelpers.IsDigit(nextByte))
            {
                RollBackState(rollBackState, isError: true);
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.RequiredDigitNotFoundAfterDecimal, nextByte);
            }
            i++;
            _bytePositionInLine++;
            return ConsumeIntegerDigitsMultiSegment(ref data, ref i);
        }

        private ConsumeNumberResult ConsumeSignMultiSegment(ref ReadOnlySpan<byte> data, scoped ref int i, scoped in PartialStateForRollback rollBackState)
        {
            if (i >= data.Length)
            {
                if (IsLastSpan)
                {
                    RollBackState(rollBackState, isError: true);
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.RequiredDigitNotFoundEndOfData);
                }

                if (!GetNextSpan())
                {
                    if (IsLastSpan)
                    {
                        RollBackState(rollBackState, isError: true);
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.RequiredDigitNotFoundEndOfData);
                    }
                    return ConsumeNumberResult.NeedMoreData;
                }
                _totalConsumed += i;
                HasValueSequence = true;
                i = 0;
                data = _buffer;
            }

            byte nextByte = data[i];
            if (nextByte == '+' || nextByte == '-')
            {
                i++;
                _bytePositionInLine++;
                if (i >= data.Length)
                {
                    if (IsLastSpan)
                    {
                        RollBackState(rollBackState, isError: true);
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.RequiredDigitNotFoundEndOfData);
                    }

                    if (!GetNextSpan())
                    {
                        if (IsLastSpan)
                        {
                            RollBackState(rollBackState, isError: true);
                            ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.RequiredDigitNotFoundEndOfData);
                        }
                        return ConsumeNumberResult.NeedMoreData;
                    }
                    _totalConsumed += i;
                    HasValueSequence = true;
                    i = 0;
                    data = _buffer;
                }
                nextByte = data[i];
            }

            if (!RdnHelpers.IsDigit(nextByte))
            {
                RollBackState(rollBackState, isError: true);
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.RequiredDigitNotFoundAfterSign, nextByte);
            }

            return ConsumeNumberResult.OperationIncomplete;
        }

        private bool ConsumeNextTokenOrRollbackMultiSegment(byte marker)
        {
            long prevTotalConsumed = _totalConsumed;
            int prevConsumed = _consumed;
            long prevPosition = _bytePositionInLine;
            long prevLineNumber = _lineNumber;
            RdnTokenType prevTokenType = _tokenType;
            SequencePosition prevSequencePosition = _currentPosition;
            bool prevTrailingCommaBeforeComment = _trailingCommaBeforeComment;
            ConsumeTokenResult result = ConsumeNextTokenMultiSegment(marker);
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
                _totalConsumed = prevTotalConsumed;
                _currentPosition = prevSequencePosition;
                _trailingCommaBeforeComment = prevTrailingCommaBeforeComment;
            }
            return false;
        }

        /// <summary>
        /// This method consumes the next token regardless of whether we are inside an object or an array.
        /// For an object, it reads the next property name token. For an array, it just reads the next value.
        /// </summary>
        private ConsumeTokenResult ConsumeNextTokenMultiSegment(byte marker)
        {
            if (_readerOptions.CommentHandling != RdnCommentHandling.Disallow)
            {
                if (_readerOptions.CommentHandling == RdnCommentHandling.Allow)
                {
                    if (marker == RdnConstants.Slash)
                    {
                        return SkipOrConsumeCommentMultiSegmentWithRollback() ? ConsumeTokenResult.Success : ConsumeTokenResult.NotEnoughDataRollBackState;
                    }
                    if (_tokenType == RdnTokenType.Comment)
                    {
                        return ConsumeNextTokenFromLastNonCommentTokenMultiSegment();
                    }
                }
                else
                {
                    Debug.Assert(_readerOptions.CommentHandling == RdnCommentHandling.Skip);
                    return ConsumeNextTokenUntilAfterAllCommentsAreSkippedMultiSegment(marker);
                }
            }

            if (_bitStack.CurrentDepth == 0)
            {
                if (_readerOptions.AllowMultipleValues)
                {
                    return ReadFirstTokenMultiSegment(marker) ? ConsumeTokenResult.Success : ConsumeTokenResult.NotEnoughDataRollBackState;
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
                    if (!GetNextSpan())
                    {
                        if (IsLastSpan)
                        {
                            ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyOrValueNotFound);
                        }
                        return ConsumeTokenResult.NotEnoughDataRollBackState;
                    }
                }
                byte first = _buffer[_consumed];
                if (first <= RdnConstants.Space)
                {
                    SkipWhiteSpaceMultiSegment();
                    if (!HasMoreDataMultiSegment(ExceptionResource.ExpectedStartOfPropertyOrValueNotFound))
                    {
                        return ConsumeTokenResult.NotEnoughDataRollBackState;
                    }
                    first = _buffer[_consumed];
                }
                TokenStartIndex = BytesConsumed;
                return ConsumeValueMultiSegment(first) ? ConsumeTokenResult.Success : ConsumeTokenResult.NotEnoughDataRollBackState;
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
                    if (!GetNextSpan())
                    {
                        if (IsLastSpan)
                        {
                            _consumed--;
                            _bytePositionInLine--;
                            ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyOrValueNotFound);
                        }
                        return ConsumeTokenResult.NotEnoughDataRollBackState;
                    }
                }
                byte first = _buffer[_consumed];

                // This check is done as an optimization to avoid calling SkipWhiteSpace when not necessary.
                if (first <= RdnConstants.Space)
                {
                    SkipWhiteSpaceMultiSegment();
                    // The next character must be a start of a property name or value.
                    if (!HasMoreDataMultiSegment(ExceptionResource.ExpectedStartOfPropertyOrValueNotFound))
                    {
                        return ConsumeTokenResult.NotEnoughDataRollBackState;
                    }
                    first = _buffer[_consumed];
                }

                TokenStartIndex = BytesConsumed;

                if (_readerOptions.CommentHandling == RdnCommentHandling.Allow && first == RdnConstants.Slash)
                {
                    if (_inObject)
                    {
                        // In object context: / after comma must be a comment (property names are always quoted)
                        _trailingCommaBeforeComment = true;
                        return SkipOrConsumeCommentMultiSegmentWithRollback() ? ConsumeTokenResult.Success : ConsumeTokenResult.NotEnoughDataRollBackState;
                    }
                    // In array/set/map: fall through to ConsumeValueMultiSegment which handles regex/comment disambiguation
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
                    return ConsumePropertyNameMultiSegment() ? ConsumeTokenResult.Success : ConsumeTokenResult.NotEnoughDataRollBackState;
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
                    return ConsumeValueMultiSegment(first) ? ConsumeTokenResult.Success : ConsumeTokenResult.NotEnoughDataRollBackState;
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

        private ConsumeTokenResult ConsumeNextTokenFromLastNonCommentTokenMultiSegment()
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

            if (!HasMoreDataMultiSegment())
            {
                goto RollBack;
            }

            byte first = _buffer[_consumed];

            // This check is done as an optimization to avoid calling SkipWhiteSpace when not necessary.
            if (first <= RdnConstants.Space)
            {
                SkipWhiteSpaceMultiSegment();
                if (!HasMoreDataMultiSegment())
                {
                    goto RollBack;
                }
                first = _buffer[_consumed];
            }

            if (_bitStack.CurrentDepth == 0 && _tokenType != RdnTokenType.None)
            {
                if (_readerOptions.AllowMultipleValues)
                {
                    return ReadFirstTokenMultiSegment(first) ? ConsumeTokenResult.Success : ConsumeTokenResult.NotEnoughDataRollBackState;
                }

                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedEndAfterSingleRdn, first);
            }

            // Note: first CAN be Slash here for regex values (e.g., after a comment in an array: [/* comment */ /regex/gi])
            Debug.Assert(first != RdnConstants.Slash || !_inObject);

            TokenStartIndex = BytesConsumed;

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
                    if (!GetNextSpan())
                    {
                        if (IsLastSpan)
                        {
                            ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyOrValueNotFound);
                        }
                        goto RollBack;
                    }
                }
                first = _buffer[_consumed];
                if (first <= RdnConstants.Space)
                {
                    SkipWhiteSpaceMultiSegment();
                    if (!HasMoreDataMultiSegment(ExceptionResource.ExpectedStartOfPropertyOrValueNotFound))
                    {
                        goto RollBack;
                    }
                    first = _buffer[_consumed];
                }
                TokenStartIndex = BytesConsumed;
                if (ConsumeValueMultiSegment(first))
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
                    if (!GetNextSpan())
                    {
                        if (IsLastSpan)
                        {
                            _consumed--;
                            _bytePositionInLine--;
                            ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyOrValueNotFound);
                        }
                        goto RollBack;
                    }
                }
                first = _buffer[_consumed];

                // This check is done as an optimization to avoid calling SkipWhiteSpace when not necessary.
                if (first <= RdnConstants.Space)
                {
                    SkipWhiteSpaceMultiSegment();
                    // The next character must be a start of a property name or value.
                    if (!HasMoreDataMultiSegment(ExceptionResource.ExpectedStartOfPropertyOrValueNotFound))
                    {
                        goto RollBack;
                    }
                    first = _buffer[_consumed];
                }

                TokenStartIndex = BytesConsumed;

                if (first == RdnConstants.Slash)
                {
                    if (_inObject)
                    {
                        // In object context: / after comma must be a comment (property names are always quoted)
                        _trailingCommaBeforeComment = true;
                        if (SkipOrConsumeCommentMultiSegmentWithRollback())
                        {
                            goto Done;
                        }
                        else
                        {
                            goto RollBack;
                        }
                    }
                    // In array/set/map: fall through to ConsumeValueMultiSegment which handles regex/comment disambiguation
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
                    if (ConsumePropertyNameMultiSegment())
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

                    if (ConsumeValueMultiSegment(first))
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
                if (ReadFirstTokenMultiSegment(first))
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

                long prevTotalConsumed = _totalConsumed;
                int prevConsumed = _consumed;
                long prevPosition = _bytePositionInLine;
                long prevLineNumber = _lineNumber;
                if (!ConsumePropertyNameMultiSegment())
                {
                    // roll back potential changes
                    _consumed = prevConsumed;
                    _tokenType = RdnTokenType.StartObject;
                    _bytePositionInLine = prevPosition;
                    _lineNumber = prevLineNumber;
                    _totalConsumed = prevTotalConsumed;
                    goto RollBack;
                }
                goto Done;
            }
            else if (_tokenType == RdnTokenType.StartSet)
            {
                Debug.Assert(first != RdnConstants.CloseBrace);
                if (!ConsumeValueMultiSegment(first))
                {
                    goto RollBack;
                }
                goto Done;
            }
            else if (_tokenType == RdnTokenType.StartMap)
            {
                Debug.Assert(first != RdnConstants.CloseBrace);
                if (!ConsumeValueMultiSegment(first))
                {
                    goto RollBack;
                }
                goto Done;
            }
            else if (_tokenType == RdnTokenType.StartArray)
            {
                Debug.Assert(first != RdnConstants.CloseBracket);
                if (!ConsumeValueMultiSegment(first))
                {
                    goto RollBack;
                }
                goto Done;
            }
            else if (_tokenType == RdnTokenType.PropertyName)
            {
                if (!ConsumeValueMultiSegment(first))
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

                    if (ConsumePropertyNameMultiSegment())
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

                    if (ConsumeValueMultiSegment(first))
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

        private bool SkipAllCommentsMultiSegment(scoped ref byte marker)
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
                else if (!IsLastSpan)
                {
                    break; // Need more data
                }
                else
                {
                    break; // Single trailing slash at end of input
                }

                if (SkipOrConsumeCommentMultiSegmentWithRollback())
                {
                    if (!HasMoreDataMultiSegment())
                    {
                        goto IncompleteNoRollback;
                    }

                    marker = _buffer[_consumed];

                    // This check is done as an optimization to avoid calling SkipWhiteSpace when not necessary.
                    if (marker <= RdnConstants.Space)
                    {
                        SkipWhiteSpaceMultiSegment();
                        if (!HasMoreDataMultiSegment())
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

        private bool SkipAllCommentsMultiSegment(scoped ref byte marker, ExceptionResource resource)
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
                else if (!IsLastSpan)
                {
                    break; // Need more data
                }
                else
                {
                    break; // Single trailing slash at end of input
                }

                if (SkipOrConsumeCommentMultiSegmentWithRollback())
                {
                    // The next character must be a start of a property name or value.
                    if (!HasMoreDataMultiSegment(resource))
                    {
                        goto IncompleteRollback;
                    }

                    marker = _buffer[_consumed];

                    // This check is done as an optimization to avoid calling SkipWhiteSpace when not necessary.
                    if (marker <= RdnConstants.Space)
                    {
                        SkipWhiteSpaceMultiSegment();
                        // The next character must be a start of a property name or value.
                        if (!HasMoreDataMultiSegment(resource))
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

        private ConsumeTokenResult ConsumeNextTokenUntilAfterAllCommentsAreSkippedMultiSegment(byte marker)
        {
            if (!SkipAllCommentsMultiSegment(ref marker))
            {
                goto IncompleteNoRollback;
            }

            TokenStartIndex = BytesConsumed;

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

                    long prevTotalConsumed = _totalConsumed;
                    int prevConsumed = _consumed;
                    long prevPosition = _bytePositionInLine;
                    long prevLineNumber = _lineNumber;
                    SequencePosition copy = _currentPosition;
                    if (!ConsumePropertyNameMultiSegment())
                    {
                        // roll back potential changes
                        _consumed = prevConsumed;
                        _tokenType = RdnTokenType.StartObject;
                        _bytePositionInLine = prevPosition;
                        _lineNumber = prevLineNumber;
                        _totalConsumed = prevTotalConsumed;
                        _currentPosition = copy;
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
                    if (!ConsumeValueMultiSegment(marker))
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
                    if (!ConsumeValueMultiSegment(marker))
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
                    if (!ConsumeValueMultiSegment(marker))
                    {
                        goto IncompleteNoRollback;
                    }
                    goto Done;
                }
            }
            else if (_tokenType == RdnTokenType.PropertyName)
            {
                if (!ConsumeValueMultiSegment(marker))
                {
                    goto IncompleteNoRollback;
                }
                goto Done;
            }
            else if (_bitStack.CurrentDepth == 0)
            {
                if (_readerOptions.AllowMultipleValues)
                {
                    return ReadFirstTokenMultiSegment(marker) ? ConsumeTokenResult.Success : ConsumeTokenResult.NotEnoughDataRollBackState;
                }

                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedEndAfterSingleRdn, marker);
            }
            else if (marker == RdnConstants.ListSeparator)
            {
                _consumed++;
                _bytePositionInLine++;

                if (_consumed >= (uint)_buffer.Length)
                {
                    if (IsLastSpan)
                    {
                        _consumed--;
                        _bytePositionInLine--;
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyOrValueNotFound);
                    }
                    if (!GetNextSpan())
                    {
                        if (IsLastSpan)
                        {
                            _consumed--;
                            _bytePositionInLine--;
                            ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyOrValueNotFound);
                        }
                        return ConsumeTokenResult.NotEnoughDataRollBackState;
                    }
                }
                marker = _buffer[_consumed];

                // This check is done as an optimization to avoid calling SkipWhiteSpace when not necessary.
                if (marker <= RdnConstants.Space)
                {
                    SkipWhiteSpaceMultiSegment();
                    // The next character must be a start of a property name or value.
                    if (!HasMoreDataMultiSegment(ExceptionResource.ExpectedStartOfPropertyOrValueNotFound))
                    {
                        return ConsumeTokenResult.NotEnoughDataRollBackState;
                    }
                    marker = _buffer[_consumed];
                }

                if (!SkipAllCommentsMultiSegment(ref marker, ExceptionResource.ExpectedStartOfPropertyOrValueNotFound))
                {
                    goto IncompleteRollback;
                }

                TokenStartIndex = BytesConsumed;

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
                    return ConsumePropertyNameMultiSegment() ? ConsumeTokenResult.Success : ConsumeTokenResult.NotEnoughDataRollBackState;
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

                    return ConsumeValueMultiSegment(marker) ? ConsumeTokenResult.Success : ConsumeTokenResult.NotEnoughDataRollBackState;
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

        private bool SkipOrConsumeCommentMultiSegmentWithRollback()
        {
            long prevTotalConsumed = BytesConsumed;
            SequencePosition start = new SequencePosition(_currentPosition.GetObject(), _currentPosition.GetInteger() + _consumed);
            bool skipSucceeded = SkipCommentMultiSegment(out int tailBytesToIgnore);

            if (skipSucceeded)
            {
                Debug.Assert(
                    _readerOptions.CommentHandling == RdnCommentHandling.Allow ||
                    _readerOptions.CommentHandling == RdnCommentHandling.Skip);

                if (_readerOptions.CommentHandling == RdnCommentHandling.Allow)
                {
                    SequencePosition end = new SequencePosition(_currentPosition.GetObject(), _currentPosition.GetInteger() + _consumed);

                    ReadOnlySequence<byte> commentSequence = _sequence.Slice(start, end);
                    commentSequence = commentSequence.Slice(2, commentSequence.Length - 2 - tailBytesToIgnore);
                    HasValueSequence = !commentSequence.IsSingleSegment;

                    if (HasValueSequence)
                    {
                        ValueSequence = commentSequence;
                    }
                    else
                    {
                        ValueSpan = commentSequence.First.Span;
                    }

                    if (_tokenType != RdnTokenType.Comment)
                    {
                        _previousTokenType = _tokenType;
                    }

                    _tokenType = RdnTokenType.Comment;
                }
            }
            else
            {
                _totalConsumed = prevTotalConsumed;
                // Note: BytesConsumed = _totalConsumed + _consumed
                // Changing _consumed and _totalConsumed to original values might not work correctly
                // since _consumed is tracking position in the current sequence
                // and current sequence might not be the same as we could've called GetNextSpan.
                // Since we return false we do not expect these APIs to be called again
                // so the values are ok to be slightly incorrect as long as the sum remains the same
                // if we don't reset this value to zero the BytesConsumed might be reported incorrectly.
                _consumed = 0;
            }

            return skipSucceeded;
        }

        private bool SkipCommentMultiSegment(out int tailBytesToIgnore)
        {
            _consumed++;
            _bytePositionInLine++;
            // Create local copy to avoid bounds checks.
            ReadOnlySpan<byte> localBuffer = _buffer.Slice(_consumed);

            if (localBuffer.Length == 0)
            {
                if (IsLastSpan)
                {
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.UnexpectedEndOfDataWhileReadingComment);
                }

                if (!GetNextSpan())
                {
                    if (IsLastSpan)
                    {
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.UnexpectedEndOfDataWhileReadingComment);
                    }

                    tailBytesToIgnore = 0;
                    return false;
                }

                localBuffer = _buffer;
            }

            byte marker = localBuffer[0];
            if (marker != RdnConstants.Slash && marker != RdnConstants.Asterisk)
            {
                ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.InvalidCharacterAtStartOfComment, marker);
            }

            bool multiLine = marker == RdnConstants.Asterisk;

            _consumed++;
            _bytePositionInLine++;
            localBuffer = localBuffer.Slice(1);

            if (localBuffer.Length == 0)
            {
                if (IsLastSpan)
                {
                    tailBytesToIgnore = 0;

                    if (multiLine)
                    {
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.UnexpectedEndOfDataWhileReadingComment);
                    }

                    return true;
                }

                if (!GetNextSpan())
                {
                    tailBytesToIgnore = 0;

                    if (IsLastSpan)
                    {
                        if (multiLine)
                        {
                            ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.UnexpectedEndOfDataWhileReadingComment);
                        }

                        return true;
                    }

                    return false;
                }

                localBuffer = _buffer;
            }

            if (multiLine)
            {
                tailBytesToIgnore = 2;
                return SkipMultiLineCommentMultiSegment(localBuffer);
            }
            else
            {
                return SkipSingleLineCommentMultiSegment(localBuffer, out tailBytesToIgnore);
            }
        }

        private bool SkipSingleLineCommentMultiSegment(ReadOnlySpan<byte> localBuffer, out int tailBytesToSkip)
        {
            bool expectLF = false;
            int dangerousLineSeparatorBytesConsumed = 0;
            tailBytesToSkip = 0;

            while (true)
            {
                if (expectLF)
                {
                    if (localBuffer[0] == RdnConstants.LineFeed)
                    {
                        tailBytesToSkip++;
                        _consumed++;
                    }

                    break;
                }

                int idx = FindLineSeparatorMultiSegment(localBuffer, ref dangerousLineSeparatorBytesConsumed);
                Debug.Assert(dangerousLineSeparatorBytesConsumed >= 0 && dangerousLineSeparatorBytesConsumed <= 2);

                if (idx != -1)
                {
                    tailBytesToSkip++;
                    _consumed += idx + 1;
                    _bytePositionInLine += idx + 1;

                    if (localBuffer[idx] == RdnConstants.LineFeed)
                    {
                        break;
                    }

                    // If we are here, we have definintely found a \r. So now to check if \n follows.
                    Debug.Assert(localBuffer[idx] == RdnConstants.CarriageReturn);

                    if (idx < localBuffer.Length - 1)
                    {
                        if (localBuffer[idx + 1] == RdnConstants.LineFeed)
                        {
                            tailBytesToSkip++;
                            _consumed++;
                            _bytePositionInLine++;
                        }

                        break;
                    }

                    expectLF = true;
                }
                else
                {
                    _consumed += localBuffer.Length;
                    _bytePositionInLine += localBuffer.Length;
                }

                if (IsLastSpan)
                {
                    if (expectLF)
                    {
                        break;
                    }

                    return true;
                }

                if (!GetNextSpan())
                {
                    if (IsLastSpan)
                    {
                        if (expectLF)
                        {
                            break;
                        }

                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }

                localBuffer = _buffer;
            }

            _bytePositionInLine = 0;
            _lineNumber++;

            return true;
        }

        private int FindLineSeparatorMultiSegment(ReadOnlySpan<byte> localBuffer, scoped ref int dangerousLineSeparatorBytesConsumed)
        {
            Debug.Assert(dangerousLineSeparatorBytesConsumed >= 0 && dangerousLineSeparatorBytesConsumed <= 2);

            if (dangerousLineSeparatorBytesConsumed != 0)
            {
                ThrowOnDangerousLineSeparatorMultiSegment(localBuffer, ref dangerousLineSeparatorBytesConsumed);

                if (dangerousLineSeparatorBytesConsumed != 0)
                {
                    // this can only happen if localBuffer size is 1 and we have previously only consumed 1 byte
                    // or localBuffer is 0
                    Debug.Assert(dangerousLineSeparatorBytesConsumed >= 1 && dangerousLineSeparatorBytesConsumed <= 2 && localBuffer.Length <= 1);
                    return -1;
                }
            }

            int totalIdx = 0;
            while (true)
            {
                int idx = localBuffer.IndexOfAny(RdnConstants.LineFeed, RdnConstants.CarriageReturn, RdnConstants.StartingByteOfNonStandardSeparator);
                dangerousLineSeparatorBytesConsumed = 0;

                if (idx == -1)
                {
                    return -1;
                }

                if (localBuffer[idx] != RdnConstants.StartingByteOfNonStandardSeparator)
                {
                    return totalIdx + idx;
                }

                int p = idx + 1;
                localBuffer = localBuffer.Slice(p);
                totalIdx += p;

                dangerousLineSeparatorBytesConsumed++;
                ThrowOnDangerousLineSeparatorMultiSegment(localBuffer, ref dangerousLineSeparatorBytesConsumed);

                if (dangerousLineSeparatorBytesConsumed != 0)
                {
                    // this can only happen in the end of the local buffer
                    Debug.Assert(localBuffer.Length < 2);
                    return -1;
                }
            }
        }

        // assumes first byte (RdnConstants.UnexpectedEndOfLineSeparator) is already read
        private void ThrowOnDangerousLineSeparatorMultiSegment(ReadOnlySpan<byte> localBuffer, scoped ref int dangerousLineSeparatorBytesConsumed)
        {
            Debug.Assert(dangerousLineSeparatorBytesConsumed == 1 || dangerousLineSeparatorBytesConsumed == 2);

            // \u2028 and \u2029 are considered respectively line and paragraph separators
            // UTF-8 representation for them is E2, 80, A8/A9
            // we have already read E2 and maybe 80 we need to check for remaining 1 or 2 bytes

            if (localBuffer.IsEmpty)
            {
                return;
            }

            if (dangerousLineSeparatorBytesConsumed == 1)
            {
                if (localBuffer[0] == 0x80)
                {
                    localBuffer = localBuffer.Slice(1);
                    dangerousLineSeparatorBytesConsumed++;

                    if (localBuffer.IsEmpty)
                    {
                        return;
                    }
                }
                else
                {
                    // no match
                    dangerousLineSeparatorBytesConsumed = 0;
                    return;
                }
            }

            if (dangerousLineSeparatorBytesConsumed == 2)
            {
                byte lastByte = localBuffer[0];
                if (lastByte == 0xA8 || lastByte == 0xA9)
                {
                    ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.UnexpectedEndOfLineSeparator);
                }
                else
                {
                    // no match
                    dangerousLineSeparatorBytesConsumed = 0;
                    return;
                }
            }
        }

        private bool SkipMultiLineCommentMultiSegment(ReadOnlySpan<byte> localBuffer)
        {
            bool expectSlash = false;
            bool ignoreNextLfForLineTracking = false;

            while (true)
            {
                Debug.Assert(localBuffer.Length > 0);

                if (expectSlash)
                {
                    if (localBuffer[0] == RdnConstants.Slash)
                    {
                        _consumed++;
                        _bytePositionInLine++;
                        return true;
                    }

                    expectSlash = false;
                }

                if (ignoreNextLfForLineTracking)
                {
                    if (localBuffer[0] == RdnConstants.LineFeed)
                    {
                        _consumed++;
                        localBuffer = localBuffer.Slice(1);
                    }

                    ignoreNextLfForLineTracking = false;
                }

                int idx = localBuffer.IndexOfAny(RdnConstants.Asterisk, RdnConstants.LineFeed, RdnConstants.CarriageReturn);

                if (idx != -1)
                {
                    int nextIdx = idx + 1;
                    byte marker = localBuffer[idx];
                    localBuffer = localBuffer.Slice(nextIdx);

                    _consumed += nextIdx;

                    switch (marker)
                    {
                        case RdnConstants.Asterisk:
                            expectSlash = true;
                            _bytePositionInLine += nextIdx;
                            break;
                        case RdnConstants.LineFeed:
                            _bytePositionInLine = 0;
                            _lineNumber++;
                            break;
                        default:
                            Debug.Assert(marker == RdnConstants.CarriageReturn);
                            _bytePositionInLine = 0;
                            _lineNumber++;
                            ignoreNextLfForLineTracking = true;
                            break;
                    }
                }
                else
                {
                    _consumed += localBuffer.Length;
                    _bytePositionInLine += localBuffer.Length;
                    localBuffer = ReadOnlySpan<byte>.Empty;
                }

                if (localBuffer.IsEmpty)
                {
                    if (IsLastSpan)
                    {
                        ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.UnexpectedEndOfDataWhileReadingComment);
                    }

                    if (!GetNextSpan())
                    {
                        if (IsLastSpan)
                        {
                            ThrowHelper.ThrowRdnReaderException(ref this, ExceptionResource.UnexpectedEndOfDataWhileReadingComment);
                        }
                        else
                        {
                            return false;
                        }
                    }

                    localBuffer = _buffer;
                    Debug.Assert(!localBuffer.IsEmpty);
                }
            }
        }

        private PartialStateForRollback CaptureState()
        {
            return new PartialStateForRollback(_totalConsumed, _bytePositionInLine, _consumed, _currentPosition);
        }

        private readonly struct PartialStateForRollback
        {
            public readonly long _prevTotalConsumed;
            public readonly long _prevBytePositionInLine;
            public readonly int _prevConsumed;
            public readonly SequencePosition _prevCurrentPosition;

            public PartialStateForRollback(long totalConsumed, long bytePositionInLine, int consumed, SequencePosition currentPosition)
            {
                _prevTotalConsumed = totalConsumed;
                _prevBytePositionInLine = bytePositionInLine;
                _prevConsumed = consumed;
                _prevCurrentPosition = currentPosition;
            }

            public SequencePosition GetStartPosition(int offset = 0)
            {
                return new SequencePosition(_prevCurrentPosition.GetObject(), _prevCurrentPosition.GetInteger() + _prevConsumed + offset);
            }
        }
    }
}
