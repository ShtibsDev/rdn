// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Rdn
{
    public ref partial struct Utf8RdnReader
    {
        /// <summary>
        /// Parses the current RDN token value from the source, unescaped, and transcoded as a <see cref="string"/>.
        /// </summary>
        /// <remarks>
        /// Returns <see langword="null" /> when <see cref="TokenType"/> is <see cref="RdnTokenType.Null"/>.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to get the value of the RDN token that is not a string
        /// (i.e. other than <see cref="RdnTokenType.String"/>, <see cref="RdnTokenType.PropertyName"/> or
        /// <see cref="RdnTokenType.Null"/>).
        /// <seealso cref="TokenType" />
        /// It will also throw when the RDN string contains invalid UTF-8 bytes, or invalid UTF-16 surrogates.
        /// </exception>
        public string? GetString()
        {
            if (TokenType == RdnTokenType.Null)
            {
                return null;
            }

            if (TokenType != RdnTokenType.String && TokenType != RdnTokenType.PropertyName)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedString(TokenType);
            }

            ReadOnlySpan<byte> span = HasValueSequence ? ValueSequence.ToArray() : ValueSpan;

            if (ValueIsEscaped)
            {
                return RdnReaderHelper.GetUnescapedString(span);
            }

            Debug.Assert(!span.Contains(RdnConstants.BackSlash));
            return RdnReaderHelper.TranscodeHelper(span);
        }

        /// <summary>
        /// Copies the current RDN token value from the source, unescaped as a UTF-8 string to the destination buffer.
        /// </summary>
        /// <param name="utf8Destination">A buffer to write the unescaped UTF-8 bytes into.</param>
        /// <returns>The number of bytes written to <paramref name="utf8Destination"/>.</returns>
        /// <remarks>
        /// Unlike <see cref="GetString"/>, this method does not support <see cref="RdnTokenType.Null"/>.
        ///
        /// This method will throw <see cref="ArgumentException"/> if the destination buffer is too small to hold the unescaped value.
        /// An appropriately sized buffer can be determined by consulting the length of either <see cref="ValueSpan"/> or <see cref="ValueSequence"/>,
        /// since the unescaped result is always less than or equal to the length of the encoded strings.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to get the value of the RDN token that is not a string
        /// (i.e. other than <see cref="RdnTokenType.String"/> or <see cref="RdnTokenType.PropertyName"/>.
        /// <seealso cref="TokenType" />
        /// It will also throw when the RDN string contains invalid UTF-8 bytes, or invalid UTF-16 surrogates.
        /// </exception>
        /// <exception cref="ArgumentException">The destination buffer is too small to hold the unescaped value.</exception>
        public readonly int CopyString(Span<byte> utf8Destination)
        {
            if (_tokenType is not (RdnTokenType.String or RdnTokenType.PropertyName))
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedString(_tokenType);
            }

            return CopyValue(utf8Destination);
        }

        internal readonly int CopyValue(Span<byte> utf8Destination)
        {
            Debug.Assert(_tokenType is RdnTokenType.String or RdnTokenType.PropertyName or RdnTokenType.Number);
            Debug.Assert(_tokenType != RdnTokenType.Number || !ValueIsEscaped, "Numbers can't contain escape characters.");

            int bytesWritten;

            if (ValueIsEscaped)
            {
                if (!TryCopyEscapedString(utf8Destination, out bytesWritten))
                {
                    utf8Destination.Slice(0, bytesWritten).Clear();
                    ThrowHelper.ThrowArgumentException_DestinationTooShort();
                }
            }
            else
            {
                if (HasValueSequence)
                {
                    ReadOnlySequence<byte> valueSequence = ValueSequence;
                    valueSequence.CopyTo(utf8Destination);
                    bytesWritten = (int)valueSequence.Length;
                }
                else
                {
                    ReadOnlySpan<byte> valueSpan = ValueSpan;
                    valueSpan.CopyTo(utf8Destination);
                    bytesWritten = valueSpan.Length;
                }
            }

            RdnReaderHelper.ValidateUtf8(utf8Destination.Slice(0, bytesWritten));
            return bytesWritten;
        }

        /// <summary>
        /// Copies the current RDN token value from the source, unescaped, and transcoded as a UTF-16 char buffer.
        /// </summary>
        /// <param name="destination">A buffer to write the transcoded UTF-16 characters into.</param>
        /// <returns>The number of characters written to <paramref name="destination"/>.</returns>
        /// <remarks>
        /// Unlike <see cref="GetString"/>, this method does not support <see cref="RdnTokenType.Null"/>.
        ///
        /// This method will throw <see cref="ArgumentException"/> if the destination buffer is too small to hold the unescaped value.
        /// An appropriately sized buffer can be determined by consulting the length of either <see cref="ValueSpan"/> or <see cref="ValueSequence"/>,
        /// since the unescaped result is always less than or equal to the length of the encoded strings.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to get the value of the RDN token that is not a string
        /// (i.e. other than <see cref="RdnTokenType.String"/> or <see cref="RdnTokenType.PropertyName"/>.
        /// <seealso cref="TokenType" />
        /// It will also throw when the RDN string contains invalid UTF-8 bytes, or invalid UTF-16 surrogates.
        /// </exception>
        /// <exception cref="ArgumentException">The destination buffer is too small to hold the unescaped value.</exception>
        public readonly int CopyString(Span<char> destination)
        {
            if (_tokenType is not (RdnTokenType.String or RdnTokenType.PropertyName))
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedString(_tokenType);
            }

            return CopyValue(destination);
        }

        internal readonly int CopyValue(Span<char> destination)
        {
            Debug.Assert(_tokenType is RdnTokenType.String or RdnTokenType.PropertyName or RdnTokenType.Number);
            Debug.Assert(_tokenType != RdnTokenType.Number || !ValueIsEscaped, "Numbers can't contain escape characters.");

            scoped ReadOnlySpan<byte> unescapedSource;
            byte[]? rentedBuffer = null;
            int valueLength;

            if (ValueIsEscaped)
            {
                valueLength = ValueLength;

                Span<byte> unescapedBuffer = valueLength <= RdnConstants.StackallocByteThreshold ?
                    stackalloc byte[RdnConstants.StackallocByteThreshold] :
                    (rentedBuffer = ArrayPool<byte>.Shared.Rent(valueLength));

                bool success = TryCopyEscapedString(unescapedBuffer, out int bytesWritten);
                Debug.Assert(success);
                unescapedSource = unescapedBuffer.Slice(0, bytesWritten);
            }
            else
            {
                if (HasValueSequence)
                {
                    ReadOnlySequence<byte> valueSequence = ValueSequence;
                    valueLength = checked((int)valueSequence.Length);

                    Span<byte> intermediate = valueLength <= RdnConstants.StackallocByteThreshold ?
                        stackalloc byte[RdnConstants.StackallocByteThreshold] :
                        (rentedBuffer = ArrayPool<byte>.Shared.Rent(valueLength));

                    valueSequence.CopyTo(intermediate);
                    unescapedSource = intermediate.Slice(0, valueLength);
                }
                else
                {
                    unescapedSource = ValueSpan;
                }
            }

            int charsWritten = RdnReaderHelper.TranscodeHelper(unescapedSource, destination);

            if (rentedBuffer != null)
            {
                new Span<byte>(rentedBuffer, 0, unescapedSource.Length).Clear();
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }

            return charsWritten;
        }

        private readonly bool TryCopyEscapedString(Span<byte> destination, out int bytesWritten)
        {
            Debug.Assert(_tokenType is RdnTokenType.String or RdnTokenType.PropertyName);
            Debug.Assert(ValueIsEscaped);

            byte[]? rentedBuffer = null;
            scoped ReadOnlySpan<byte> source;

            if (HasValueSequence)
            {
                ReadOnlySequence<byte> valueSequence = ValueSequence;
                int sequenceLength = checked((int)valueSequence.Length);

                Span<byte> intermediate = sequenceLength <= RdnConstants.StackallocByteThreshold ?
                    stackalloc byte[RdnConstants.StackallocByteThreshold] :
                    (rentedBuffer = ArrayPool<byte>.Shared.Rent(sequenceLength));

                valueSequence.CopyTo(intermediate);
                source = intermediate.Slice(0, sequenceLength);
            }
            else
            {
                source = ValueSpan;
            }

            bool success = RdnReaderHelper.TryUnescape(source, destination, out bytesWritten);

            if (rentedBuffer != null)
            {
                new Span<byte>(rentedBuffer, 0, source.Length).Clear();
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }

            Debug.Assert(bytesWritten < source.Length, "source buffer must contain at least one escape sequence");
            return success;
        }

        /// <summary>
        /// Parses the current RDN token value from the source as a comment, transcoded as a <see cref="string"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to get the value of the RDN token that is not a comment.
        /// <seealso cref="TokenType" />
        /// </exception>
        public string GetComment()
        {
            if (TokenType != RdnTokenType.Comment)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedComment(TokenType);
            }
            ReadOnlySpan<byte> span = HasValueSequence ? ValueSequence.ToArray() : ValueSpan;
            return RdnReaderHelper.TranscodeHelper(span);
        }

        /// <summary>
        /// Parses the current RDN token value from the source as a <see cref="bool"/>.
        /// Returns <see langword="true"/> if the TokenType is RdnTokenType.True and <see langword="false"/> if the TokenType is RdnTokenType.False.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to get the value of a RDN token that is not a boolean (i.e. <see cref="RdnTokenType.True"/> or <see cref="RdnTokenType.False"/>).
        /// <seealso cref="TokenType" />
        /// </exception>
        public bool GetBoolean()
        {
            RdnTokenType type = TokenType;
            if (type == RdnTokenType.True)
            {
                Debug.Assert((HasValueSequence ? ValueSequence.ToArray() : ValueSpan).Length == 4);
                return true;
            }
            else if (type != RdnTokenType.False)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedBoolean(TokenType);
                Debug.Fail("Throw helper should have thrown an exception.");
            }

            Debug.Assert((HasValueSequence ? ValueSequence.ToArray() : ValueSpan).Length == 5);
            return false;
        }

        /// <summary>
        /// Parses the current RDN token value from the source and decodes the Base64 encoded RDN string as bytes.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to get the value of a RDN token that is not a <see cref="RdnTokenType.String"/>.
        /// <seealso cref="TokenType" />
        /// </exception>
        /// <exception cref="FormatException">
        /// The RDN string contains data outside of the expected Base64 range, or if it contains invalid/more than two padding characters,
        /// or is incomplete (i.e. the RDN string length is not a multiple of 4).
        /// </exception>
        public byte[] GetBytesFromBase64()
        {
            if (!TryGetBytesFromBase64(out byte[]? value))
            {
                ThrowHelper.ThrowFormatException(DataType.Base64String);
            }
            return value;
        }

        /// <summary>
        /// Parses the current RDN token value from the source as a <see cref="byte"/>.
        /// Returns the value if the entire UTF-8 encoded token value can be successfully parsed to a <see cref="byte"/>
        /// value.
        /// Throws exceptions otherwise.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to get the value of a RDN token that is not a <see cref="RdnTokenType.Number"/>.
        /// <seealso cref="TokenType" />
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown if the RDN token value is either of incorrect numeric format (for example if it contains a decimal or
        /// is written in scientific notation) or, it represents a number less than <see cref="byte.MinValue"/> or greater
        /// than <see cref="byte.MaxValue"/>.
        /// </exception>
        public byte GetByte()
        {
            if (!TryGetByte(out byte value))
            {
                ThrowHelper.ThrowFormatException(NumericType.Byte);
            }
            return value;
        }

        internal byte GetByteWithQuotes()
        {
            ReadOnlySpan<byte> span = GetUnescapedSpan();
            if (!TryGetByteCore(out byte value, span))
            {
                ThrowHelper.ThrowFormatException(NumericType.Byte);
            }
            return value;
        }

        /// <summary>
        /// Parses the current RDN token value from the source as an <see cref="sbyte"/>.
        /// Returns the value if the entire UTF-8 encoded token value can be successfully parsed to an <see cref="sbyte"/>
        /// value.
        /// Throws exceptions otherwise.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to get the value of a RDN token that is not a <see cref="RdnTokenType.Number"/>.
        /// <seealso cref="TokenType" />
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown if the RDN token value is either of incorrect numeric format (for example if it contains a decimal or
        /// is written in scientific notation) or, it represents a number less than <see cref="sbyte.MinValue"/> or greater
        /// than <see cref="sbyte.MaxValue"/>.
        /// </exception>
        [System.CLSCompliantAttribute(false)]
        public sbyte GetSByte()
        {
            if (!TryGetSByte(out sbyte value))
            {
                ThrowHelper.ThrowFormatException(NumericType.SByte);
            }
            return value;
        }

        internal sbyte GetSByteWithQuotes()
        {
            ReadOnlySpan<byte> span = GetUnescapedSpan();
            if (!TryGetSByteCore(out sbyte value, span))
            {
                ThrowHelper.ThrowFormatException(NumericType.SByte);
            }
            return value;
        }

        /// <summary>
        /// Parses the current RDN token value from the source as a <see cref="short"/>.
        /// Returns the value if the entire UTF-8 encoded token value can be successfully parsed to a <see cref="short"/>
        /// value.
        /// Throws exceptions otherwise.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to get the value of a RDN token that is not a <see cref="RdnTokenType.Number"/>.
        /// <seealso cref="TokenType" />
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown if the RDN token value is either of incorrect numeric format (for example if it contains a decimal or
        /// is written in scientific notation) or, it represents a number less than <see cref="short.MinValue"/> or greater
        /// than <see cref="short.MaxValue"/>.
        /// </exception>
        public short GetInt16()
        {
            if (!TryGetInt16(out short value))
            {
                ThrowHelper.ThrowFormatException(NumericType.Int16);
            }
            return value;
        }

        internal short GetInt16WithQuotes()
        {
            ReadOnlySpan<byte> span = GetUnescapedSpan();
            if (!TryGetInt16Core(out short value, span))
            {
                ThrowHelper.ThrowFormatException(NumericType.Int16);
            }
            return value;
        }

        /// <summary>
        /// Parses the current RDN token value from the source as an <see cref="int"/>.
        /// Returns the value if the entire UTF-8 encoded token value can be successfully parsed to an <see cref="int"/>
        /// value.
        /// Throws exceptions otherwise.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to get the value of a RDN token that is not a <see cref="RdnTokenType.Number"/>.
        /// <seealso cref="TokenType" />
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown if the RDN token value is either of incorrect numeric format (for example if it contains a decimal or
        /// is written in scientific notation) or, it represents a number less than <see cref="int.MinValue"/> or greater
        /// than <see cref="int.MaxValue"/>.
        /// </exception>
        public int GetInt32()
        {
            if (!TryGetInt32(out int value))
            {
                ThrowHelper.ThrowFormatException(NumericType.Int32);
            }
            return value;
        }

        internal int GetInt32WithQuotes()
        {
            ReadOnlySpan<byte> span = GetUnescapedSpan();
            if (!TryGetInt32Core(out int value, span))
            {
                ThrowHelper.ThrowFormatException(NumericType.Int32);
            }
            return value;
        }

        /// <summary>
        /// Parses the current RDN token value from the source as a <see cref="long"/>.
        /// Returns the value if the entire UTF-8 encoded token value can be successfully parsed to a <see cref="long"/>
        /// value.
        /// Throws exceptions otherwise.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to get the value of a RDN token that is not a <see cref="RdnTokenType.Number"/>.
        /// <seealso cref="TokenType" />
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown if the RDN token value is either of incorrect numeric format (for example if it contains a decimal or
        /// is written in scientific notation) or, it represents a number less than <see cref="long.MinValue"/> or greater
        /// than <see cref="long.MaxValue"/>.
        /// </exception>
        public long GetInt64()
        {
            if (!TryGetInt64(out long value))
            {
                ThrowHelper.ThrowFormatException(NumericType.Int64);
            }
            return value;
        }

        internal long GetInt64WithQuotes()
        {
            ReadOnlySpan<byte> span = GetUnescapedSpan();
            if (!TryGetInt64Core(out long value, span))
            {
                ThrowHelper.ThrowFormatException(NumericType.Int64);
            }
            return value;
        }

        /// <summary>
        /// Parses the current RDN token value from the source as a <see cref="ushort"/>.
        /// Returns the value if the entire UTF-8 encoded token value can be successfully parsed to a <see cref="ushort"/>
        /// value.
        /// Throws exceptions otherwise.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to get the value of a RDN token that is not a <see cref="RdnTokenType.Number"/>.
        /// <seealso cref="TokenType" />
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown if the RDN token value is either of incorrect numeric format (for example if it contains a decimal or
        /// is written in scientific notation) or, it represents a number less than <see cref="ushort.MinValue"/> or greater
        /// than <see cref="ushort.MaxValue"/>.
        /// </exception>
        [System.CLSCompliantAttribute(false)]
        public ushort GetUInt16()
        {
            if (!TryGetUInt16(out ushort value))
            {
                ThrowHelper.ThrowFormatException(NumericType.UInt16);
            }
            return value;
        }

        internal ushort GetUInt16WithQuotes()
        {
            ReadOnlySpan<byte> span = GetUnescapedSpan();
            if (!TryGetUInt16Core(out ushort value, span))
            {
                ThrowHelper.ThrowFormatException(NumericType.UInt16);
            }
            return value;
        }

        /// <summary>
        /// Parses the current RDN token value from the source as a <see cref="uint"/>.
        /// Returns the value if the entire UTF-8 encoded token value can be successfully parsed to a <see cref="uint"/>
        /// value.
        /// Throws exceptions otherwise.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to get the value of a RDN token that is not a <see cref="RdnTokenType.Number"/>.
        /// <seealso cref="TokenType" />
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown if the RDN token value is either of incorrect numeric format (for example if it contains a decimal or
        /// is written in scientific notation) or, it represents a number less than <see cref="uint.MinValue"/> or greater
        /// than <see cref="uint.MaxValue"/>.
        /// </exception>
        [System.CLSCompliantAttribute(false)]
        public uint GetUInt32()
        {
            if (!TryGetUInt32(out uint value))
            {
                ThrowHelper.ThrowFormatException(NumericType.UInt32);
            }
            return value;
        }

        internal uint GetUInt32WithQuotes()
        {
            ReadOnlySpan<byte> span = GetUnescapedSpan();
            if (!TryGetUInt32Core(out uint value, span))
            {
                ThrowHelper.ThrowFormatException(NumericType.UInt32);
            }
            return value;
        }

        /// <summary>
        /// Parses the current RDN token value from the source as a <see cref="ulong"/>.
        /// Returns the value if the entire UTF-8 encoded token value can be successfully parsed to a <see cref="ulong"/>
        /// value.
        /// Throws exceptions otherwise.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to get the value of a RDN token that is not a <see cref="RdnTokenType.Number"/>.
        /// <seealso cref="TokenType" />
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown if the RDN token value is either of incorrect numeric format (for example if it contains a decimal or
        /// is written in scientific notation) or, it represents a number less than <see cref="ulong.MinValue"/> or greater
        /// than <see cref="ulong.MaxValue"/>.
        /// </exception>
        [System.CLSCompliantAttribute(false)]
        public ulong GetUInt64()
        {
            if (!TryGetUInt64(out ulong value))
            {
                ThrowHelper.ThrowFormatException(NumericType.UInt64);
            }
            return value;
        }

        internal ulong GetUInt64WithQuotes()
        {
            ReadOnlySpan<byte> span = GetUnescapedSpan();
            if (!TryGetUInt64Core(out ulong value, span))
            {
                ThrowHelper.ThrowFormatException(NumericType.UInt64);
            }
            return value;
        }

        /// <summary>
        /// Parses the current RDN token value from the source as a <see cref="float"/>.
        /// Returns the value if the entire UTF-8 encoded token value can be successfully parsed to a <see cref="float"/>
        /// value.
        /// Throws exceptions otherwise.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to get the value of a RDN token that is not a <see cref="RdnTokenType.Number"/>.
        /// <seealso cref="TokenType" />
        /// </exception>
        /// <exception cref="FormatException">
        /// On any framework that is not .NET Core 3.0 or higher, thrown if the RDN token value represents a number less than <see cref="float.MinValue"/> or greater
        /// than <see cref="float.MaxValue"/>.
        /// </exception>
        public float GetSingle()
        {
            if (!TryGetSingle(out float value))
            {
                ThrowHelper.ThrowFormatException(NumericType.Single);
            }
            return value;
        }

        internal float GetSingleWithQuotes()
        {
            ReadOnlySpan<byte> span = GetUnescapedSpan();

            if (RdnReaderHelper.TryGetFloatingPointConstant(span, out float value))
            {
                return value;
            }

            // NETCOREAPP implementation of the TryParse method above permits case-insensitive variants of the
            // float constants "NaN", "Infinity", "-Infinity". This differs from the NETFRAMEWORK implementation.
            // The following logic reconciles the two implementations to enforce consistent behavior.
            if (!(Utf8Parser.TryParse(span, out value, out int bytesConsumed)
                  && span.Length == bytesConsumed
                  && RdnHelpers.IsFinite(value)))
            {
                ThrowHelper.ThrowFormatException(NumericType.Single);
            }

            return value;
        }

        internal float GetSingleFloatingPointConstant()
        {
            ReadOnlySpan<byte> span = GetUnescapedSpan();

            if (!RdnReaderHelper.TryGetFloatingPointConstant(span, out float value))
            {
                ThrowHelper.ThrowFormatException(NumericType.Single);
            }

            return value;
        }

        /// <summary>
        /// Parses the current RDN token value from the source as a <see cref="double"/>.
        /// Returns the value if the entire UTF-8 encoded token value can be successfully parsed to a <see cref="double"/>
        /// value.
        /// Throws exceptions otherwise.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to get the value of a RDN token that is not a <see cref="RdnTokenType.Number"/>.
        /// <seealso cref="TokenType" />
        /// </exception>
        /// <exception cref="FormatException">
        /// On any framework that is not .NET Core 3.0 or higher, thrown if the RDN token value represents a number less than <see cref="double.MinValue"/> or greater
        /// than <see cref="double.MaxValue"/>.
        /// </exception>
        public double GetDouble()
        {
            if (!TryGetDouble(out double value))
            {
                ThrowHelper.ThrowFormatException(NumericType.Double);
            }
            return value;
        }

        internal double GetDoubleWithQuotes()
        {
            ReadOnlySpan<byte> span = GetUnescapedSpan();

            if (RdnReaderHelper.TryGetFloatingPointConstant(span, out double value))
            {
                return value;
            }

            // NETCOREAPP implementation of the TryParse method above permits case-insensitive variants of the
            // float constants "NaN", "Infinity", "-Infinity". This differs from the NETFRAMEWORK implementation.
            // The following logic reconciles the two implementations to enforce consistent behavior.
            if (!(Utf8Parser.TryParse(span, out value, out int bytesConsumed)
                  && span.Length == bytesConsumed
                  && RdnHelpers.IsFinite(value)))
            {
                ThrowHelper.ThrowFormatException(NumericType.Double);
            }

            return value;
        }

        internal double GetDoubleFloatingPointConstant()
        {
            ReadOnlySpan<byte> span = GetUnescapedSpan();

            if (!RdnReaderHelper.TryGetFloatingPointConstant(span, out double value))
            {
                ThrowHelper.ThrowFormatException(NumericType.Double);
            }

            return value;
        }

        /// <summary>
        /// Parses the current RDN token value from the source as a <see cref="decimal"/>.
        /// Returns the value if the entire UTF-8 encoded token value can be successfully parsed to a <see cref="decimal"/>
        /// value.
        /// Throws exceptions otherwise.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to get the value of a RDN token that is not a <see cref="RdnTokenType.Number"/>.
        /// <seealso cref="TokenType" />
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown if the RDN token value represents a number less than <see cref="decimal.MinValue"/> or greater
        /// than <see cref="decimal.MaxValue"/>.
        /// </exception>
        public decimal GetDecimal()
        {
            if (!TryGetDecimal(out decimal value))
            {
                ThrowHelper.ThrowFormatException(NumericType.Decimal);
            }
            return value;
        }

        internal decimal GetDecimalWithQuotes()
        {
            ReadOnlySpan<byte> span = GetUnescapedSpan();
            if (!TryGetDecimalCore(out decimal value, span))
            {
                ThrowHelper.ThrowFormatException(NumericType.Decimal);
            }
            return value;
        }

        /// <summary>
        /// Parses the current RDN token value from the source as a <see cref="DateTime"/>.
        /// Returns the value if the entire UTF-8 encoded token value can be successfully parsed to a <see cref="DateTime"/>
        /// value.
        /// Throws exceptions otherwise.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to get the value of a RDN token that is not a <see cref="RdnTokenType.String"/>.
        /// <seealso cref="TokenType" />
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown if the RDN token value is of an unsupported format. Only a subset of ISO 8601 formats are supported.
        /// </exception>
        public DateTime GetDateTime()
        {
            if (!TryGetDateTime(out DateTime value))
            {
                ThrowHelper.ThrowFormatException(DataType.DateTime);
            }

            return value;
        }

        internal DateTime GetDateTimeNoValidation()
        {
            if (!TryGetDateTimeCore(out DateTime value))
            {
                ThrowHelper.ThrowFormatException(DataType.DateTime);
            }

            return value;
        }

        /// <summary>
        /// Parses the current RDN token value from the source as a <see cref="DateTimeOffset"/>.
        /// Returns the value if the entire UTF-8 encoded token value can be successfully parsed to a <see cref="DateTimeOffset"/>
        /// value.
        /// Throws exceptions otherwise.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to get the value of a RDN token that is not a <see cref="RdnTokenType.String"/>.
        /// <seealso cref="TokenType" />
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown if the RDN token value is of an unsupported format. Only a subset of ISO 8601 formats are supported.
        /// </exception>
        public DateTimeOffset GetDateTimeOffset()
        {
            if (!TryGetDateTimeOffset(out DateTimeOffset value))
            {
                ThrowHelper.ThrowFormatException(DataType.DateTimeOffset);
            }

            return value;
        }

        internal DateTimeOffset GetDateTimeOffsetNoValidation()
        {
            if (!TryGetDateTimeOffsetCore(out DateTimeOffset value))
            {
                ThrowHelper.ThrowFormatException(DataType.DateTimeOffset);
            }

            return value;
        }

        /// <summary>
        /// Parses the current RDN token value from the source as a <see cref="Guid"/>.
        /// Returns the value if the entire UTF-8 encoded token value can be successfully parsed to a <see cref="Guid"/>
        /// value.
        /// Throws exceptions otherwise.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to get the value of a RDN token that is not a <see cref="RdnTokenType.String"/>.
        /// <seealso cref="TokenType" />
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown if the RDN token value is of an unsupported format for a Guid.
        /// </exception>
        public Guid GetGuid()
        {
            if (!TryGetGuid(out Guid value))
            {
                ThrowHelper.ThrowFormatException(DataType.Guid);
            }

            return value;
        }

        internal Guid GetGuidNoValidation()
        {
            if (!TryGetGuidCore(out Guid value))
            {
                ThrowHelper.ThrowFormatException(DataType.Guid);
            }

            return value;
        }

        /// <summary>
        /// Parses the current RDN token value from the source and decodes the Base64 encoded RDN string as bytes.
        /// Returns <see langword="true"/> if the entire token value is encoded as valid Base64 text and can be successfully
        /// decoded to bytes.
        /// Returns <see langword="false"/> otherwise.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to get the value of a RDN token that is not a <see cref="RdnTokenType.String"/>.
        /// <seealso cref="TokenType" />
        /// </exception>
        public bool TryGetBytesFromBase64([NotNullWhen(true)] out byte[]? value)
        {
            if (TokenType != RdnTokenType.String)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedString(TokenType);
            }

            ReadOnlySpan<byte> span = HasValueSequence ? ValueSequence.ToArray() : ValueSpan;

            if (ValueIsEscaped)
            {
                return RdnReaderHelper.TryGetUnescapedBase64Bytes(span, out value);
            }

            Debug.Assert(!span.Contains(RdnConstants.BackSlash));
            return RdnReaderHelper.TryDecodeBase64(span, out value);
        }

        /// <summary>
        /// Parses the current RDN token value from the source as a <see cref="byte"/>.
        /// Returns <see langword="true"/> if the entire UTF-8 encoded token value can be successfully
        /// parsed to a <see cref="byte"/> value.
        /// Returns <see langword="false"/> otherwise.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to get the value of a RDN token that is not a <see cref="RdnTokenType.Number"/>.
        /// <seealso cref="TokenType" />
        /// </exception>
        public bool TryGetByte(out byte value)
        {
            if (TokenType != RdnTokenType.Number)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedNumber(TokenType);
            }

            ReadOnlySpan<byte> span = HasValueSequence ? ValueSequence.ToArray() : ValueSpan;
            return TryGetByteCore(out value, span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryGetByteCore(out byte value, ReadOnlySpan<byte> span)
        {
            if (Utf8Parser.TryParse(span, out byte tmp, out int bytesConsumed)
                && span.Length == bytesConsumed)
            {
                value = tmp;
                return true;
            }

            value = 0;
            return false;
        }

        /// <summary>
        /// Parses the current RDN token value from the source as an <see cref="sbyte"/>.
        /// Returns <see langword="true"/> if the entire UTF-8 encoded token value can be successfully
        /// parsed to an <see cref="sbyte"/> value.
        /// Returns <see langword="false"/> otherwise.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to get the value of a RDN token that is not a <see cref="RdnTokenType.Number"/>.
        /// <seealso cref="TokenType" />
        /// </exception>
        [System.CLSCompliantAttribute(false)]
        public bool TryGetSByte(out sbyte value)
        {
            if (TokenType != RdnTokenType.Number)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedNumber(TokenType);
            }

            ReadOnlySpan<byte> span = HasValueSequence ? ValueSequence.ToArray() : ValueSpan;
            return TryGetSByteCore(out value, span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryGetSByteCore(out sbyte value, ReadOnlySpan<byte> span)
        {
            if (Utf8Parser.TryParse(span, out sbyte tmp, out int bytesConsumed)
                && span.Length == bytesConsumed)
            {
                value = tmp;
                return true;
            }

            value = 0;
            return false;
        }

        /// <summary>
        /// Parses the current RDN token value from the source as a <see cref="short"/>.
        /// Returns <see langword="true"/> if the entire UTF-8 encoded token value can be successfully
        /// parsed to a <see cref="short"/> value.
        /// Returns <see langword="false"/> otherwise.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to get the value of a RDN token that is not a <see cref="RdnTokenType.Number"/>.
        /// <seealso cref="TokenType" />
        /// </exception>
        public bool TryGetInt16(out short value)
        {
            if (TokenType != RdnTokenType.Number)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedNumber(TokenType);
            }

            ReadOnlySpan<byte> span = HasValueSequence ? ValueSequence.ToArray() : ValueSpan;
            return TryGetInt16Core(out value, span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryGetInt16Core(out short value, ReadOnlySpan<byte> span)
        {
            if (Utf8Parser.TryParse(span, out short tmp, out int bytesConsumed)
                && span.Length == bytesConsumed)
            {
                value = tmp;
                return true;
            }

            value = 0;
            return false;
        }

        /// <summary>
        /// Parses the current RDN token value from the source as an <see cref="int"/>.
        /// Returns <see langword="true"/> if the entire UTF-8 encoded token value can be successfully
        /// parsed to an <see cref="int"/> value.
        /// Returns <see langword="false"/> otherwise.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to get the value of a RDN token that is not a <see cref="RdnTokenType.Number"/>.
        /// <seealso cref="TokenType" />
        /// </exception>
        public bool TryGetInt32(out int value)
        {
            if (TokenType != RdnTokenType.Number)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedNumber(TokenType);
            }

            ReadOnlySpan<byte> span = HasValueSequence ? ValueSequence.ToArray() : ValueSpan;
            return TryGetInt32Core(out value, span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryGetInt32Core(out int value, ReadOnlySpan<byte> span)
        {
            if (Utf8Parser.TryParse(span, out int tmp, out int bytesConsumed)
                && span.Length == bytesConsumed)
            {
                value = tmp;
                return true;
            }

            value = 0;
            return false;
        }

        /// <summary>
        /// Parses the current RDN token value from the source as a <see cref="long"/>.
        /// Returns <see langword="true"/> if the entire UTF-8 encoded token value can be successfully
        /// parsed to a <see cref="long"/> value.
        /// Returns <see langword="false"/> otherwise.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to get the value of a RDN token that is not a <see cref="RdnTokenType.Number"/>.
        /// <seealso cref="TokenType" />
        /// </exception>
        public bool TryGetInt64(out long value)
        {
            if (TokenType != RdnTokenType.Number)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedNumber(TokenType);
            }

            ReadOnlySpan<byte> span = HasValueSequence ? ValueSequence.ToArray() : ValueSpan;
            return TryGetInt64Core(out value, span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryGetInt64Core(out long value, ReadOnlySpan<byte> span)
        {
            if (Utf8Parser.TryParse(span, out long tmp, out int bytesConsumed)
                && span.Length == bytesConsumed)
            {
                value = tmp;
                return true;
            }

            value = 0;
            return false;
        }

        /// <summary>
        /// Parses the current RDN token value from the source as a <see cref="ushort"/>.
        /// Returns <see langword="true"/> if the entire UTF-8 encoded token value can be successfully
        /// parsed to a <see cref="ushort"/> value.
        /// Returns <see langword="false"/> otherwise.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to get the value of a RDN token that is not a <see cref="RdnTokenType.Number"/>.
        /// <seealso cref="TokenType" />
        /// </exception>
        [System.CLSCompliantAttribute(false)]
        public bool TryGetUInt16(out ushort value)
        {
            if (TokenType != RdnTokenType.Number)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedNumber(TokenType);
            }

            ReadOnlySpan<byte> span = HasValueSequence ? ValueSequence.ToArray() : ValueSpan;
            return TryGetUInt16Core(out value, span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryGetUInt16Core(out ushort value, ReadOnlySpan<byte> span)
        {
            if (Utf8Parser.TryParse(span, out ushort tmp, out int bytesConsumed)
                && span.Length == bytesConsumed)
            {
                value = tmp;
                return true;
            }

            value = 0;
            return false;
        }

        /// <summary>
        /// Parses the current RDN token value from the source as a <see cref="uint"/>.
        /// Returns <see langword="true"/> if the entire UTF-8 encoded token value can be successfully
        /// parsed to a <see cref="uint"/> value.
        /// Returns <see langword="false"/> otherwise.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to get the value of a RDN token that is not a <see cref="RdnTokenType.Number"/>.
        /// <seealso cref="TokenType" />
        /// </exception>
        [System.CLSCompliantAttribute(false)]
        public bool TryGetUInt32(out uint value)
        {
            if (TokenType != RdnTokenType.Number)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedNumber(TokenType);
            }

            ReadOnlySpan<byte> span = HasValueSequence ? ValueSequence.ToArray() : ValueSpan;
            return TryGetUInt32Core(out value, span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryGetUInt32Core(out uint value, ReadOnlySpan<byte> span)
        {
            if (Utf8Parser.TryParse(span, out uint tmp, out int bytesConsumed)
                && span.Length == bytesConsumed)
            {
                value = tmp;
                return true;
            }

            value = 0;
            return false;
        }

        /// <summary>
        /// Parses the current RDN token value from the source as a <see cref="ulong"/>.
        /// Returns <see langword="true"/> if the entire UTF-8 encoded token value can be successfully
        /// parsed to a <see cref="ulong"/> value.
        /// Returns <see langword="false"/> otherwise.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to get the value of a RDN token that is not a <see cref="RdnTokenType.Number"/>.
        /// <seealso cref="TokenType" />
        /// </exception>
        [System.CLSCompliantAttribute(false)]
        public bool TryGetUInt64(out ulong value)
        {
            if (TokenType != RdnTokenType.Number)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedNumber(TokenType);
            }

            ReadOnlySpan<byte> span = HasValueSequence ? ValueSequence.ToArray() : ValueSpan;
            return TryGetUInt64Core(out value, span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryGetUInt64Core(out ulong value, ReadOnlySpan<byte> span)
        {
            if (Utf8Parser.TryParse(span, out ulong tmp, out int bytesConsumed)
                && span.Length == bytesConsumed)
            {
                value = tmp;
                return true;
            }

            value = 0;
            return false;
        }

        /// <summary>
        /// Parses the current RDN token value from the source as a <see cref="float"/>.
        /// Returns <see langword="true"/> if the entire UTF-8 encoded token value can be successfully
        /// parsed to a <see cref="float"/> value.
        /// Returns <see langword="false"/> otherwise.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to get the value of a RDN token that is not a <see cref="RdnTokenType.Number"/>.
        /// <seealso cref="TokenType" />
        /// </exception>
        public bool TryGetSingle(out float value)
        {
            if (TokenType != RdnTokenType.Number)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedNumber(TokenType);
            }

            ReadOnlySpan<byte> span = HasValueSequence ? ValueSequence.ToArray() : ValueSpan;

            if (Utf8Parser.TryParse(span, out float tmp, out int bytesConsumed)
                && span.Length == bytesConsumed)
            {
                value = tmp;
                return true;
            }

            // Fall back to NaN/Infinity/−Infinity
            if (RdnReaderHelper.TryGetFloatingPointConstant(span, out value))
            {
                return true;
            }

            value = 0;
            return false;
        }

        /// <summary>
        /// Parses the current RDN token value from the source as a <see cref="double"/>.
        /// Returns <see langword="true"/> if the entire UTF-8 encoded token value can be successfully
        /// parsed to a <see cref="double"/> value.
        /// Returns <see langword="false"/> otherwise.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to get the value of a RDN token that is not a <see cref="RdnTokenType.Number"/>.
        /// <seealso cref="TokenType" />
        /// </exception>
        public bool TryGetDouble(out double value)
        {
            if (TokenType != RdnTokenType.Number)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedNumber(TokenType);
            }

            ReadOnlySpan<byte> span = HasValueSequence ? ValueSequence.ToArray() : ValueSpan;

            if (Utf8Parser.TryParse(span, out double tmp, out int bytesConsumed)
                && span.Length == bytesConsumed)
            {
                value = tmp;
                return true;
            }

            // Fall back to NaN/Infinity/−Infinity
            if (RdnReaderHelper.TryGetFloatingPointConstant(span, out value))
            {
                return true;
            }

            value = 0;
            return false;
        }

        /// <summary>
        /// Parses the current RDN token value from the source as a <see cref="decimal"/>.
        /// Returns <see langword="true"/> if the entire UTF-8 encoded token value can be successfully
        /// parsed to a <see cref="decimal"/> value.
        /// Returns <see langword="false"/> otherwise.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to get the value of a RDN token that is not a <see cref="RdnTokenType.Number"/>.
        /// <seealso cref="TokenType" />
        /// </exception>
        public bool TryGetDecimal(out decimal value)
        {
            if (TokenType != RdnTokenType.Number)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedNumber(TokenType);
            }

            ReadOnlySpan<byte> span = HasValueSequence ? ValueSequence.ToArray() : ValueSpan;
            return TryGetDecimalCore(out value, span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryGetDecimalCore(out decimal value, ReadOnlySpan<byte> span)
        {
            if (Utf8Parser.TryParse(span, out decimal tmp, out int bytesConsumed)
                && span.Length == bytesConsumed)
            {
                value = tmp;
                return true;
            }

            value = 0;
            return false;
        }

        /// <summary>
        /// Parses the current RDN token value from the source as a <see cref="DateTime"/>.
        /// Returns <see langword="true"/> if the entire UTF-8 encoded token value can be successfully
        /// parsed to a <see cref="DateTime"/> value.
        /// Returns <see langword="false"/> otherwise.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to get the value of a RDN token that is not a <see cref="RdnTokenType.String"/>.
        /// <seealso cref="TokenType" />
        /// </exception>
        public bool TryGetDateTime(out DateTime value)
        {
            if (TokenType != RdnTokenType.String && TokenType != RdnTokenType.RdnDateTime)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedString(TokenType);
            }

            if (TokenType == RdnTokenType.RdnDateTime)
            {
                return TryGetRdnDateTimeCore(out value);
            }

            return TryGetDateTimeCore(out value);
        }

        internal bool TryGetDateTimeCore(out DateTime value)
        {
            scoped ReadOnlySpan<byte> span;

            if (HasValueSequence)
            {
                long sequenceLength = ValueSequence.Length;
                if (!RdnHelpers.IsInRangeInclusive(sequenceLength, RdnConstants.MinimumDateTimeParseLength, RdnConstants.MaximumEscapedDateTimeOffsetParseLength))
                {
                    value = default;
                    return false;
                }

                Span<byte> stackSpan = stackalloc byte[RdnConstants.MaximumEscapedDateTimeOffsetParseLength];
                ValueSequence.CopyTo(stackSpan);
                span = stackSpan.Slice(0, (int)sequenceLength);
            }
            else
            {
                span = ValueSpan;
            }

            return RdnReaderHelper.TryGetValue(span, ValueIsEscaped, out value);
        }

        /// <summary>
        /// Parses the current RDN token value from the source as a <see cref="DateTimeOffset"/>.
        /// Returns <see langword="true"/> if the entire UTF-8 encoded token value can be successfully
        /// parsed to a <see cref="DateTimeOffset"/> value.
        /// Returns <see langword="false"/> otherwise.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to get the value of a RDN token that is not a <see cref="RdnTokenType.String"/>.
        /// <seealso cref="TokenType" />
        /// </exception>
        public bool TryGetDateTimeOffset(out DateTimeOffset value)
        {
            if (TokenType != RdnTokenType.String && TokenType != RdnTokenType.RdnDateTime)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedString(TokenType);
            }

            if (TokenType == RdnTokenType.RdnDateTime)
            {
                if (TryGetRdnDateTimeCore(out DateTime dt))
                {
                    value = new DateTimeOffset(dt, TimeSpan.Zero);
                    return true;
                }
                value = default;
                return false;
            }

            return TryGetDateTimeOffsetCore(out value);
        }

        internal bool TryGetDateTimeOffsetCore(out DateTimeOffset value)
        {
            scoped ReadOnlySpan<byte> span;

            if (HasValueSequence)
            {
                long sequenceLength = ValueSequence.Length;
                if (!RdnHelpers.IsInRangeInclusive(sequenceLength, RdnConstants.MinimumDateTimeParseLength, RdnConstants.MaximumEscapedDateTimeOffsetParseLength))
                {
                    value = default;
                    return false;
                }

                Span<byte> stackSpan = stackalloc byte[RdnConstants.MaximumEscapedDateTimeOffsetParseLength];
                ValueSequence.CopyTo(stackSpan);
                span = stackSpan.Slice(0, (int)sequenceLength);
            }
            else
            {
                span = ValueSpan;
            }

            return RdnReaderHelper.TryGetValue(span, ValueIsEscaped, out value);
        }

        /// <summary>
        /// Parses the current RDN token value from the source as a <see cref="Guid"/>.
        /// Returns <see langword="true"/> if the entire UTF-8 encoded token value can be successfully
        /// parsed to a <see cref="Guid"/> value. Only supports <see cref="Guid"/> values with hyphens
        /// and without any surrounding decorations.
        /// Returns <see langword="false"/> otherwise.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to get the value of a RDN token that is not a <see cref="RdnTokenType.String"/>.
        /// <seealso cref="TokenType" />
        /// </exception>
        public bool TryGetGuid(out Guid value)
        {
            if (TokenType != RdnTokenType.String)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedString(TokenType);
            }

            return TryGetGuidCore(out value);
        }

        internal bool TryGetGuidCore(out Guid value)
        {
            scoped ReadOnlySpan<byte> span;

            if (HasValueSequence)
            {
                long sequenceLength = ValueSequence.Length;
                if (sequenceLength > RdnConstants.MaximumEscapedGuidLength)
                {
                    value = default;
                    return false;
                }

                Span<byte> stackSpan = stackalloc byte[RdnConstants.MaximumEscapedGuidLength];
                ValueSequence.CopyTo(stackSpan);
                span = stackSpan.Slice(0, (int)sequenceLength);
            }
            else
            {
                span = ValueSpan;
            }

            return RdnReaderHelper.TryGetValue(span, ValueIsEscaped, out value);
        }

        // --- RDN Regex API ---

        /// <summary>
        /// Gets the source (pattern) of the current RDN RegExp token.
        /// </summary>
        public string GetRdnRegExpSource()
        {
            if (TokenType != RdnTokenType.RdnRegExp)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedString(TokenType);
            }

            ReadOnlySpan<byte> span = HasValueSequence ? ValueSequence.ToArray() : ValueSpan;
            // ValueSpan is "pattern/flags" — find the last /
            int lastSlash = span.LastIndexOf(RdnConstants.Slash);
            if (lastSlash < 0)
            {
                ThrowHelper.ThrowFormatException();
            }

            ReadOnlySpan<byte> source = span.Slice(0, lastSlash);

            if (ValueIsEscaped)
            {
                return RdnReaderHelper.GetUnescapedString(source);
            }

            return RdnReaderHelper.TranscodeHelper(source);
        }

        /// <summary>
        /// Gets the flags of the current RDN RegExp token.
        /// </summary>
        public string GetRdnRegExpFlags()
        {
            if (TokenType != RdnTokenType.RdnRegExp)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedString(TokenType);
            }

            ReadOnlySpan<byte> span = HasValueSequence ? ValueSequence.ToArray() : ValueSpan;
            int lastSlash = span.LastIndexOf(RdnConstants.Slash);
            if (lastSlash < 0)
            {
                ThrowHelper.ThrowFormatException();
            }

            ReadOnlySpan<byte> flags = span.Slice(lastSlash + 1);
            return RdnReaderHelper.TranscodeHelper(flags);
        }

        /// <summary>
        /// Tries to get the source and flags of the current RDN RegExp token.
        /// </summary>
        public bool TryGetRdnRegExp(out string source, out string flags)
        {
            if (TokenType != RdnTokenType.RdnRegExp)
            {
                source = string.Empty;
                flags = string.Empty;
                return false;
            }

            ReadOnlySpan<byte> span = HasValueSequence ? ValueSequence.ToArray() : ValueSpan;
            int lastSlash = span.LastIndexOf(RdnConstants.Slash);
            if (lastSlash < 0)
            {
                source = string.Empty;
                flags = string.Empty;
                return false;
            }

            ReadOnlySpan<byte> sourceSpan = span.Slice(0, lastSlash);
            ReadOnlySpan<byte> flagsSpan = span.Slice(lastSlash + 1);

            if (ValueIsEscaped)
            {
                source = RdnReaderHelper.GetUnescapedString(sourceSpan);
            }
            else
            {
                source = RdnReaderHelper.TranscodeHelper(sourceSpan);
            }

            flags = RdnReaderHelper.TranscodeHelper(flagsSpan);
            return true;
        }

        // --- RDN Date/Time API ---

        /// <summary>
        /// Gets the RDN DateTime value from the current token.
        /// </summary>
        public DateTime GetRdnDateTime()
        {
            if (!TryGetRdnDateTime(out DateTime value))
            {
                ThrowHelper.ThrowFormatException(DataType.DateTime);
            }
            return value;
        }

        /// <summary>
        /// Tries to get the RDN DateTime value from the current token.
        /// Accepts both RdnDateTime tokens and String tokens containing ISO dates.
        /// </summary>
        public bool TryGetRdnDateTime(out DateTime value)
        {
            if (TokenType == RdnTokenType.RdnDateTime)
            {
                return TryGetRdnDateTimeCore(out value);
            }
            if (TokenType == RdnTokenType.String)
            {
                return TryGetDateTimeCore(out value);
            }
            ThrowHelper.ThrowInvalidOperationException_ExpectedString(TokenType);
            value = default;
            return false;
        }

        /// <summary>
        /// Core parser for RDN DateTime from ValueSpan (body after @).
        /// Handles: full ISO, date-only, no-ms, and unix timestamp.
        /// </summary>
        internal bool TryGetRdnDateTimeCore(out DateTime value)
        {
            ReadOnlySpan<byte> span = HasValueSequence ? ValueSequence.ToArray() : ValueSpan;

            if (span.Length == 0)
            {
                value = default;
                return false;
            }

            // Unix timestamp: all digits
            if (span.Length <= 13 && RdnHelpers.IsDigit(span[0]) && !span.Contains(RdnConstants.Hyphen) && !span.Contains(RdnConstants.Colon))
            {
                return TryParseUnixTimestamp(span, out value);
            }

            // ISO date/datetime: parse using existing helpers
            if (RdnHelpers.TryParseAsISO(span, out DateTime tmp))
            {
                value = tmp;
                return true;
            }

            value = default;
            return false;
        }

        private static bool TryParseUnixTimestamp(ReadOnlySpan<byte> span, out DateTime value)
        {
            if (System.Buffers.Text.Utf8Parser.TryParse(span, out long timestamp, out int bytesConsumed) && bytesConsumed == span.Length)
            {
                // Timestamps > 10 digits are likely milliseconds
                if (span.Length > 10)
                {
                    value = DateTime.UnixEpoch.AddMilliseconds(timestamp);
                }
                else
                {
                    value = DateTime.UnixEpoch.AddSeconds(timestamp);
                }
                value = DateTime.SpecifyKind(value, DateTimeKind.Utc);
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>
        /// Gets the RDN TimeOnly value from the current token.
        /// </summary>
        public TimeOnly GetRdnTimeOnly()
        {
            if (!TryGetRdnTimeOnly(out TimeOnly value))
            {
                ThrowHelper.ThrowFormatException(DataType.TimeOnly);
            }
            return value;
        }

        /// <summary>
        /// Tries to get the RDN TimeOnly value from the current token.
        /// </summary>
        public bool TryGetRdnTimeOnly(out TimeOnly value)
        {
            if (TokenType != RdnTokenType.RdnTimeOnly && TokenType != RdnTokenType.String)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedString(TokenType);
            }

            ReadOnlySpan<byte> span = HasValueSequence ? ValueSequence.ToArray() : ValueSpan;

            if (span.Length < 5) // minimum: HH:MM
            {
                value = default;
                return false;
            }

            // Parse HH:MM:SS[.mmm]
            if (span.Length >= 2 && span.Length <= 16)
            {
                if (System.Buffers.Text.Utf8Parser.TryParse(span, out TimeSpan ts, out int consumed, 'c') && consumed == span.Length)
                {
                    if (ts >= TimeSpan.Zero && ts <= TimeOnly.MaxValue.ToTimeSpan())
                    {
                        value = TimeOnly.FromTimeSpan(ts);
                        return true;
                    }
                }
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Gets the RDN Duration value from the current token.
        /// </summary>
        public RdnDuration GetRdnDuration()
        {
            if (!TryGetRdnDuration(out RdnDuration value))
            {
                ThrowHelper.ThrowFormatException(DataType.TimeSpan);
            }
            return value;
        }

        /// <summary>
        /// Tries to get the RDN Duration value from the current token.
        /// </summary>
        public bool TryGetRdnDuration(out RdnDuration value)
        {
            if (TokenType != RdnTokenType.RdnDuration)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedString(TokenType);
            }

            ReadOnlySpan<byte> span = HasValueSequence ? ValueSequence.ToArray() : ValueSpan;

            if (span.Length < 2 || span[0] != (byte)'P')
            {
                value = default;
                return false;
            }

            value = new RdnDuration(RdnReaderHelper.TranscodeHelper(span));
            return true;
        }

        // --- RDN Binary API ---

        /// <summary>
        /// Gets the RDN binary value from the current token as a byte array.
        /// </summary>
        public byte[] GetRdnBinary()
        {
            if (!TryGetRdnBinary(out byte[]? value))
            {
                ThrowHelper.ThrowFormatException();
            }
            return value!;
        }

        /// <summary>
        /// Tries to get the RDN binary value from the current token.
        /// Decodes base64 (b"...") or hex (x"...") content into a byte array.
        /// </summary>
        public bool TryGetRdnBinary([NotNullWhen(true)] out byte[]? value)
        {
            if (TokenType != RdnTokenType.RdnBinary)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedString(TokenType);
            }

            ReadOnlySpan<byte> span = HasValueSequence ? ValueSequence.ToArray() : ValueSpan;

            if (span.Length == 0)
            {
                value = Array.Empty<byte>();
                return true;
            }

            // ValueIsEscaped: false = base64, true = hex
            if (!ValueIsEscaped)
            {
                // Base64 decode
                int maxDecodedLength = Base64.GetMaxDecodedFromUtf8Length(span.Length);
                byte[] decoded = new byte[maxDecodedLength];
                OperationStatus status = Base64.DecodeFromUtf8(span, decoded, out _, out int written);
                if (status != OperationStatus.Done)
                {
                    value = null;
                    return false;
                }
                value = decoded.AsSpan(0, written).ToArray();
                return true;
            }
            else
            {
                // Hex decode
                int byteCount = span.Length / 2;
                byte[] decoded = new byte[byteCount];
                for (int i = 0; i < byteCount; i++)
                {
                    int hi = HexConverter.FromChar(span[i * 2]);
                    int lo = HexConverter.FromChar(span[i * 2 + 1]);
                    if (hi == 0xFF || lo == 0xFF)
                    {
                        value = null;
                        return false;
                    }
                    decoded[i] = (byte)((hi << 4) | lo);
                }
                value = decoded;
                return true;
            }
        }
    }
}

