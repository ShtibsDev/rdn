// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Rdn
{
    /// <summary>
    ///   Represents the structure of a RDN value in a lightweight, read-only form.
    /// </summary>
    /// <remarks>
    ///   This class utilizes resources from pooled memory to minimize the garbage collector (GC)
    ///   impact in high-usage scenarios. Failure to properly Dispose this object will result in
    ///   the memory not being returned to the pool, which will cause an increase in GC impact across
    ///   various parts of the framework.
    /// </remarks>
    public sealed partial class RdnDocument : IDisposable
    {
        private ReadOnlyMemory<byte> _utf8Rdn;
        private MetadataDb _parsedData;

        private byte[]? _extraRentedArrayPoolBytes;
        private PooledByteBufferWriter? _extraPooledByteBufferWriter;

        internal bool IsDisposable { get; }

        /// <summary>
        ///   The <see cref="RdnElement"/> representing the value of the document.
        /// </summary>
        public RdnElement RootElement => new RdnElement(this, 0);

        private RdnDocument(
            ReadOnlyMemory<byte> utf8Rdn,
            MetadataDb parsedData,
            byte[]? extraRentedArrayPoolBytes = null,
            PooledByteBufferWriter? extraPooledByteBufferWriter = null,
            bool isDisposable = true)
        {
            Debug.Assert(!utf8Rdn.IsEmpty);

            // Both rented values better be null if we're not disposable.
            Debug.Assert(isDisposable ||
                (extraRentedArrayPoolBytes == null && extraPooledByteBufferWriter == null));

            // Both rented values can't be specified.
            Debug.Assert(extraRentedArrayPoolBytes == null || extraPooledByteBufferWriter == null);

            _utf8Rdn = utf8Rdn;
            _parsedData = parsedData;
            _extraRentedArrayPoolBytes = extraRentedArrayPoolBytes;
            _extraPooledByteBufferWriter = extraPooledByteBufferWriter;
            IsDisposable = isDisposable;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            int length = _utf8Rdn.Length;
            if (length == 0 || !IsDisposable)
            {
                return;
            }

            _parsedData.Dispose();
            _utf8Rdn = ReadOnlyMemory<byte>.Empty;

            if (_extraRentedArrayPoolBytes != null)
            {
                byte[]? extraRentedBytes = Interlocked.Exchange<byte[]?>(ref _extraRentedArrayPoolBytes, null);

                if (extraRentedBytes != null)
                {
                    // When "extra rented bytes exist" it contains the document,
                    // and thus needs to be cleared before being returned.
                    extraRentedBytes.AsSpan(0, length).Clear();
                    ArrayPool<byte>.Shared.Return(extraRentedBytes);
                }
            }
            else if (_extraPooledByteBufferWriter != null)
            {
                PooledByteBufferWriter? extraBufferWriter = Interlocked.Exchange<PooledByteBufferWriter?>(ref _extraPooledByteBufferWriter, null);
                extraBufferWriter?.Dispose();
            }
        }

        /// <summary>
        ///  Write the document into the provided writer as a RDN value.
        /// </summary>
        /// <param name="writer"></param>
        /// <exception cref="ArgumentNullException">
        ///   The <paramref name="writer"/> parameter is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        ///   This <see cref="RootElement"/>'s <see cref="RdnElement.ValueKind"/> would result in an invalid RDN.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="RdnDocument"/> has been disposed.
        /// </exception>
        public void WriteTo(Utf8RdnWriter writer)
        {
            ArgumentNullException.ThrowIfNull(writer);

            RootElement.WriteTo(writer);
        }

        internal RdnTokenType GetRdnTokenType(int index)
        {
            CheckNotDisposed();

            return _parsedData.GetRdnTokenType(index);
        }

        internal bool ValueIsEscaped(int index, bool isPropertyName)
        {
            CheckNotDisposed();

            int matchIndex = isPropertyName ? index - DbRow.Size : index;
            DbRow row = _parsedData.Get(matchIndex);
            Debug.Assert(!isPropertyName || row.TokenType is RdnTokenType.PropertyName);

            return row.HasComplexChildren;
        }

        internal int GetArrayLength(int index)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            if (row.TokenType != RdnTokenType.StartArray && row.TokenType != RdnTokenType.StartSet && row.TokenType != RdnTokenType.StartMap)
            {
                ThrowHelper.ThrowRdnElementWrongTypeException(RdnTokenType.StartArray, row.TokenType);
            }

            return row.SizeOrLength;
        }

        internal int GetPropertyCount(int index)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(RdnTokenType.StartObject, row.TokenType);

            return row.SizeOrLength;
        }

        internal RdnElement GetArrayIndexElement(int currentIndex, int arrayIndex)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(currentIndex);

            if (row.TokenType != RdnTokenType.StartArray && row.TokenType != RdnTokenType.StartSet && row.TokenType != RdnTokenType.StartMap)
            {
                ThrowHelper.ThrowRdnElementWrongTypeException(RdnTokenType.StartArray, row.TokenType);
            }

            int arrayLength = row.SizeOrLength;

            if ((uint)arrayIndex >= (uint)arrayLength)
            {
                throw new IndexOutOfRangeException();
            }

            if (!row.HasComplexChildren)
            {
                // Since we wouldn't be here without having completed the document parse, and we
                // already vetted the index against the length, this new index will always be
                // within the table.
                return new RdnElement(this, currentIndex + ((arrayIndex + 1) * DbRow.Size));
            }

            int elementCount = 0;
            int objectOffset = currentIndex + DbRow.Size;

            for (; objectOffset < _parsedData.Length; objectOffset += DbRow.Size)
            {
                if (arrayIndex == elementCount)
                {
                    return new RdnElement(this, objectOffset);
                }

                row = _parsedData.Get(objectOffset);

                if (!row.IsSimpleValue)
                {
                    objectOffset += DbRow.Size * row.NumberOfRows;
                }

                elementCount++;
            }

            Debug.Fail(
                $"Ran out of database searching for array index {arrayIndex} from {currentIndex} when length was {arrayLength}");
            throw new IndexOutOfRangeException();
        }

        internal int GetEndIndex(int index, bool includeEndElement)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            if (row.IsSimpleValue)
            {
                return index + DbRow.Size;
            }

            int endIndex = index + DbRow.Size * row.NumberOfRows;

            if (includeEndElement)
            {
                endIndex += DbRow.Size;
            }

            return endIndex;
        }

        internal ReadOnlyMemory<byte> GetRootRawValue()
        {
            return GetRawValue(0, includeQuotes: true);
        }

        internal ReadOnlyMemory<byte> GetRawValue(int index, bool includeQuotes)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            if (row.IsSimpleValue)
            {
                if (includeQuotes && row.TokenType == RdnTokenType.String)
                {
                    // Start one character earlier than the value (the open quote)
                    // End one character after the value (the close quote)
                    return _utf8Rdn.Slice(row.Location - 1, row.SizeOrLength + 2);
                }

                return _utf8Rdn.Slice(row.Location, row.SizeOrLength);
            }

            int endElementIdx = GetEndIndex(index, includeEndElement: false);
            int start = row.Location;
            row = _parsedData.Get(endElementIdx);
            return _utf8Rdn.Slice(start, row.Location - start + row.SizeOrLength);
        }

        private ReadOnlyMemory<byte> GetPropertyRawValue(int valueIndex)
        {
            CheckNotDisposed();

            // The property name is stored one row before the value
            DbRow row = _parsedData.Get(valueIndex - DbRow.Size);
            Debug.Assert(row.TokenType == RdnTokenType.PropertyName);

            // Subtract one for the open quote.
            int start = row.Location - 1;
            int end;

            row = _parsedData.Get(valueIndex);

            if (row.IsSimpleValue)
            {
                end = row.Location + row.SizeOrLength;

                // If the value was a string, pick up the terminating quote.
                if (row.TokenType == RdnTokenType.String)
                {
                    end++;
                }

                return _utf8Rdn.Slice(start, end - start);
            }

            int endElementIdx = GetEndIndex(valueIndex, includeEndElement: false);
            row = _parsedData.Get(endElementIdx);
            end = row.Location + row.SizeOrLength;
            return _utf8Rdn.Slice(start, end - start);
        }

        internal string? GetString(int index, RdnTokenType expectedType)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            RdnTokenType tokenType = row.TokenType;

            if (tokenType == RdnTokenType.Null)
            {
                return null;
            }

            CheckExpectedType(expectedType, tokenType);

            ReadOnlySpan<byte> data = _utf8Rdn.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            return row.HasComplexChildren
                ? RdnReaderHelper.GetUnescapedString(segment)
                : RdnReaderHelper.TranscodeHelper(segment);
        }

        internal bool TextEquals(int index, ReadOnlySpan<char> otherText, bool isPropertyName)
        {
            CheckNotDisposed();

            byte[]? otherUtf8TextArray = null;

            int length = checked(otherText.Length * RdnConstants.MaxExpansionFactorWhileTranscoding);
            Span<byte> otherUtf8Text = length <= RdnConstants.StackallocByteThreshold ?
                stackalloc byte[RdnConstants.StackallocByteThreshold] :
                (otherUtf8TextArray = ArrayPool<byte>.Shared.Rent(length));

            OperationStatus status = RdnWriterHelper.ToUtf8(otherText, otherUtf8Text, out int written);
            Debug.Assert(status != OperationStatus.DestinationTooSmall);
            bool result;
            if (status == OperationStatus.InvalidData)
            {
                result = false;
            }
            else
            {
                Debug.Assert(status == OperationStatus.Done);
                result = TextEquals(index, otherUtf8Text.Slice(0, written), isPropertyName, shouldUnescape: true);
            }

            if (otherUtf8TextArray != null)
            {
                otherUtf8Text.Slice(0, written).Clear();
                ArrayPool<byte>.Shared.Return(otherUtf8TextArray);
            }

            return result;
        }

        internal bool TextEquals(int index, ReadOnlySpan<byte> otherUtf8Text, bool isPropertyName, bool shouldUnescape)
        {
            CheckNotDisposed();

            int matchIndex = isPropertyName ? index - DbRow.Size : index;

            DbRow row = _parsedData.Get(matchIndex);

            CheckExpectedType(
                isPropertyName ? RdnTokenType.PropertyName : RdnTokenType.String,
                row.TokenType);

            ReadOnlySpan<byte> data = _utf8Rdn.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (otherUtf8Text.Length > segment.Length || (!shouldUnescape && otherUtf8Text.Length != segment.Length))
            {
                return false;
            }

            if (row.HasComplexChildren && shouldUnescape)
            {
                if (otherUtf8Text.Length < segment.Length / RdnConstants.MaxExpansionFactorWhileEscaping)
                {
                    return false;
                }

                int idx = segment.IndexOf(RdnConstants.BackSlash);
                Debug.Assert(idx != -1);

                if (!otherUtf8Text.StartsWith(segment.Slice(0, idx)))
                {
                    return false;
                }

                return RdnReaderHelper.UnescapeAndCompare(segment.Slice(idx), otherUtf8Text.Slice(idx));
            }

            return segment.SequenceEqual(otherUtf8Text);
        }

        internal string GetNameOfPropertyValue(int index)
        {
            // The property name is one row before the property value
            return GetString(index - DbRow.Size, RdnTokenType.PropertyName)!;
        }

        internal ReadOnlySpan<byte> GetPropertyNameRaw(int index)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index - DbRow.Size);
            Debug.Assert(row.TokenType is RdnTokenType.PropertyName);

            return _utf8Rdn.Span.Slice(row.Location, row.SizeOrLength);
        }

        internal bool TryGetValue(int index, [NotNullWhen(true)] out byte[]? value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(RdnTokenType.String, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Rdn.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            // Segment needs to be unescaped
            if (row.HasComplexChildren)
            {
                return RdnReaderHelper.TryGetUnescapedBase64Bytes(segment, out value);
            }

            Debug.Assert(!segment.Contains(RdnConstants.BackSlash));
            return RdnReaderHelper.TryDecodeBase64(segment, out value);
        }

        internal bool TryGetValue(int index, out sbyte value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(RdnTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Rdn.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (Utf8Parser.TryParse(segment, out sbyte tmp, out int consumed) &&
                consumed == segment.Length)
            {
                value = tmp;
                return true;
            }

            value = 0;
            return false;
        }

        internal bool TryGetValue(int index, out byte value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(RdnTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Rdn.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (Utf8Parser.TryParse(segment, out byte tmp, out int consumed) &&
                consumed == segment.Length)
            {
                value = tmp;
                return true;
            }

            value = 0;
            return false;
        }

        internal bool TryGetValue(int index, out short value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(RdnTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Rdn.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (Utf8Parser.TryParse(segment, out short tmp, out int consumed) &&
                consumed == segment.Length)
            {
                value = tmp;
                return true;
            }

            value = 0;
            return false;
        }

        internal bool TryGetValue(int index, out ushort value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(RdnTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Rdn.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (Utf8Parser.TryParse(segment, out ushort tmp, out int consumed) &&
                consumed == segment.Length)
            {
                value = tmp;
                return true;
            }

            value = 0;
            return false;
        }

        internal bool TryGetValue(int index, out int value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(RdnTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Rdn.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (Utf8Parser.TryParse(segment, out int tmp, out int consumed) &&
                consumed == segment.Length)
            {
                value = tmp;
                return true;
            }

            value = 0;
            return false;
        }

        internal bool TryGetValue(int index, out uint value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(RdnTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Rdn.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (Utf8Parser.TryParse(segment, out uint tmp, out int consumed) &&
                consumed == segment.Length)
            {
                value = tmp;
                return true;
            }

            value = 0;
            return false;
        }

        internal bool TryGetValue(int index, out long value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(RdnTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Rdn.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (Utf8Parser.TryParse(segment, out long tmp, out int consumed) &&
                consumed == segment.Length)
            {
                value = tmp;
                return true;
            }

            value = 0;
            return false;
        }

        internal bool TryGetValue(int index, out ulong value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(RdnTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Rdn.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (Utf8Parser.TryParse(segment, out ulong tmp, out int consumed) &&
                consumed == segment.Length)
            {
                value = tmp;
                return true;
            }

            value = 0;
            return false;
        }

        internal bool TryGetValue(int index, out double value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(RdnTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Rdn.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (Utf8Parser.TryParse(segment, out double tmp, out int bytesConsumed) &&
                segment.Length == bytesConsumed)
            {
                value = tmp;
                return true;
            }

            // Fall back to NaN/Infinity/−Infinity
            if (RdnReaderHelper.TryGetFloatingPointConstant(segment, out value))
            {
                return true;
            }

            value = 0;
            return false;
        }

        internal bool TryGetValue(int index, out float value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(RdnTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Rdn.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (Utf8Parser.TryParse(segment, out float tmp, out int bytesConsumed) &&
                segment.Length == bytesConsumed)
            {
                value = tmp;
                return true;
            }

            // Fall back to NaN/Infinity/−Infinity
            if (RdnReaderHelper.TryGetFloatingPointConstant(segment, out value))
            {
                return true;
            }

            value = 0;
            return false;
        }

        internal bool TryGetValue(int index, out decimal value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(RdnTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Rdn.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (Utf8Parser.TryParse(segment, out decimal tmp, out int bytesConsumed) &&
                segment.Length == bytesConsumed)
            {
                value = tmp;
                return true;
            }

            value = 0;
            return false;
        }

        internal bool TryGetValue(int index, out DateTime value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            if (row.TokenType == RdnTokenType.RdnDateTime)
            {
                ReadOnlySpan<byte> data = _utf8Rdn.Span;
                ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);
                return TryParseRdnDateTime(segment, out value);
            }

            CheckExpectedType(RdnTokenType.String, row.TokenType);

            ReadOnlySpan<byte> data2 = _utf8Rdn.Span;
            ReadOnlySpan<byte> segment2 = data2.Slice(row.Location, row.SizeOrLength);

            return RdnReaderHelper.TryGetValue(segment2, row.HasComplexChildren, out value);
        }

        internal bool TryGetValue(int index, out DateTimeOffset value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            if (row.TokenType == RdnTokenType.RdnDateTime)
            {
                ReadOnlySpan<byte> data = _utf8Rdn.Span;
                ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);
                if (TryParseRdnDateTime(segment, out DateTime dt))
                {
                    value = new DateTimeOffset(dt, TimeSpan.Zero);
                    return true;
                }
                value = default;
                return false;
            }

            CheckExpectedType(RdnTokenType.String, row.TokenType);

            ReadOnlySpan<byte> data2 = _utf8Rdn.Span;
            ReadOnlySpan<byte> segment2 = data2.Slice(row.Location, row.SizeOrLength);

            return RdnReaderHelper.TryGetValue(segment2, row.HasComplexChildren, out value);
        }

        private static bool TryParseRdnDateTime(ReadOnlySpan<byte> span, out DateTime value)
        {
            if (span.Length == 0)
            {
                value = default;
                return false;
            }

            // Unix timestamp: all digits, no hyphens or colons
            if (span.Length <= 13 && RdnHelpers.IsDigit(span[0]) && !span.Contains(RdnConstants.Hyphen) && !span.Contains(RdnConstants.Colon))
            {
                if (Utf8Parser.TryParse(span, out long timestamp, out int consumed) && consumed == span.Length)
                {
                    if (span.Length > 10)
                        value = DateTime.SpecifyKind(DateTime.UnixEpoch.AddMilliseconds(timestamp), DateTimeKind.Utc);
                    else
                        value = DateTime.SpecifyKind(DateTime.UnixEpoch.AddSeconds(timestamp), DateTimeKind.Utc);
                    return true;
                }
                value = default;
                return false;
            }

            // ISO date/datetime
            if (RdnHelpers.TryParseAsISO(span, out DateTime tmp))
            {
                value = tmp;
                return true;
            }

            value = default;
            return false;
        }

        internal bool TryGetRdnTimeOnly(int index, out TimeOnly value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            if (row.TokenType != RdnTokenType.RdnTimeOnly)
            {
                value = default;
                return false;
            }

            ReadOnlySpan<byte> data = _utf8Rdn.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (Utf8Parser.TryParse(segment, out TimeSpan ts, out int consumed, 'c') && consumed == segment.Length)
            {
                if (ts >= TimeSpan.Zero && ts <= TimeOnly.MaxValue.ToTimeSpan())
                {
                    value = TimeOnly.FromTimeSpan(ts);
                    return true;
                }
            }

            value = default;
            return false;
        }

        internal bool TryGetRdnDuration(int index, out RdnDuration value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            if (row.TokenType != RdnTokenType.RdnDuration)
            {
                value = default;
                return false;
            }

            ReadOnlySpan<byte> data = _utf8Rdn.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            value = new RdnDuration(System.Text.Encoding.UTF8.GetString(segment));
            return true;
        }

        internal bool TryGetRdnBinary(int index, [NotNullWhen(true)] out byte[]? value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            if (row.TokenType != RdnTokenType.RdnBinary)
            {
                value = null;
                return false;
            }

            ReadOnlySpan<byte> data = _utf8Rdn.Span;
            ReadOnlySpan<byte> content = data.Slice(row.Location, row.SizeOrLength);
            bool isHex = row.HasComplexChildren;

            if (content.Length == 0)
            {
                value = Array.Empty<byte>();
                return true;
            }

            if (!isHex)
            {
                // Base64 decode
                int maxDecodedLength = Base64.GetMaxDecodedFromUtf8Length(content.Length);
                byte[] decoded = new byte[maxDecodedLength];
                OperationStatus status = Base64.DecodeFromUtf8(content, decoded, out _, out int written);
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
                int byteCount = content.Length / 2;
                byte[] decoded = new byte[byteCount];
                for (int i = 0; i < byteCount; i++)
                {
                    int hi = HexConverter.FromChar(content[i * 2]);
                    int lo = HexConverter.FromChar(content[i * 2 + 1]);
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

        internal bool TryGetRdnRegExp(int index, out string source, out string flags)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            if (row.TokenType != RdnTokenType.RdnRegExp)
            {
                source = string.Empty;
                flags = string.Empty;
                return false;
            }

            ReadOnlySpan<byte> data = _utf8Rdn.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            // segment is "pattern/flags" — find the last /
            int lastSlash = segment.LastIndexOf(RdnConstants.Slash);
            if (lastSlash < 0)
            {
                source = string.Empty;
                flags = string.Empty;
                return false;
            }

            ReadOnlySpan<byte> sourceSpan = segment.Slice(0, lastSlash);
            ReadOnlySpan<byte> flagsSpan = segment.Slice(lastSlash + 1);

            if (row.HasComplexChildren) // HasComplexChildren means ValueIsEscaped for simple values
            {
                source = RdnReaderHelper.GetUnescapedString(sourceSpan);
            }
            else
            {
                source = System.Text.Encoding.UTF8.GetString(sourceSpan);
            }

            flags = System.Text.Encoding.UTF8.GetString(flagsSpan);
            return true;
        }

        internal bool TryGetValue(int index, out Guid value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(RdnTokenType.String, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Rdn.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            return RdnReaderHelper.TryGetValue(segment, row.HasComplexChildren, out value);
        }

        internal string GetRawValueAsString(int index)
        {
            ReadOnlyMemory<byte> segment = GetRawValue(index, includeQuotes: true);
            return RdnReaderHelper.TranscodeHelper(segment.Span);
        }

        internal string GetPropertyRawValueAsString(int valueIndex)
        {
            ReadOnlyMemory<byte> segment = GetPropertyRawValue(valueIndex);
            return RdnReaderHelper.TranscodeHelper(segment.Span);
        }

        internal RdnElement CloneElement(int index)
        {
            int endIndex = GetEndIndex(index, true);
            MetadataDb newDb = _parsedData.CopySegment(index, endIndex);
            ReadOnlyMemory<byte> segmentCopy = GetRawValue(index, includeQuotes: true).ToArray();

            RdnDocument newDocument =
                new RdnDocument(
                    segmentCopy,
                    newDb,
                    extraRentedArrayPoolBytes: null,
                    extraPooledByteBufferWriter: null,
                    isDisposable: false);

            return newDocument.RootElement;
        }

        internal void WriteElementTo(
            int index,
            Utf8RdnWriter writer)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            switch (row.TokenType)
            {
                case RdnTokenType.StartObject:
                    writer.WriteStartObject();
                    WriteComplexElement(index, writer);
                    return;
                case RdnTokenType.StartArray:
                    writer.WriteStartArray();
                    WriteComplexElement(index, writer);
                    return;
                case RdnTokenType.StartSet:
                    writer.WriteStartSet(forceTypeName: row.SizeOrLength == 0);
                    WriteComplexElement(index, writer);
                    return;
                case RdnTokenType.StartMap:
                    writer.WriteStartMap(forceTypeName: row.SizeOrLength == 0);
                    WriteComplexElement(index, writer);
                    return;
                case RdnTokenType.String:
                    WriteString(row, writer);
                    return;
                case RdnTokenType.Number:
                    writer.WriteNumberValue(_utf8Rdn.Slice(row.Location, row.SizeOrLength).Span);
                    return;
                case RdnTokenType.True:
                    writer.WriteBooleanValue(value: true);
                    return;
                case RdnTokenType.False:
                    writer.WriteBooleanValue(value: false);
                    return;
                case RdnTokenType.Null:
                    writer.WriteNullValue();
                    return;
                case RdnTokenType.RdnDateTime:
                case RdnTokenType.RdnTimeOnly:
                case RdnTokenType.RdnDuration:
                    WriteRdnLiteral(row, writer);
                    return;
                case RdnTokenType.RdnRegExp:
                    WriteRdnRegExp(row, writer);
                    return;
                case RdnTokenType.RdnBinary:
                    WriteRdnBinary(row, writer);
                    return;
            }

            Debug.Fail($"Unexpected encounter with RdnTokenType {row.TokenType}");
        }

        private void WriteRdnLiteral(DbRow row, Utf8RdnWriter writer)
        {
            ReadOnlySpan<byte> body = _utf8Rdn.Slice(row.Location, row.SizeOrLength).Span;
            // Write as raw: @ + body
            Span<byte> buffer = stackalloc byte[1 + body.Length];
            buffer[0] = RdnConstants.AtSign;
            body.CopyTo(buffer.Slice(1));
            writer.WriteRawValue(buffer, skipInputValidation: true);
        }

        private void WriteRdnRegExp(DbRow row, Utf8RdnWriter writer)
        {
            ReadOnlySpan<byte> body = _utf8Rdn.Slice(row.Location, row.SizeOrLength).Span;
            // Write as raw: / + body (body already contains pattern/flags with closing /)
            Span<byte> buffer = stackalloc byte[1 + body.Length];
            buffer[0] = RdnConstants.Slash;
            body.CopyTo(buffer.Slice(1));
            writer.WriteRawValue(buffer, skipInputValidation: true);
        }

        private void WriteRdnBinary(DbRow row, Utf8RdnWriter writer)
        {
            ReadOnlySpan<byte> content = _utf8Rdn.Slice(row.Location, row.SizeOrLength).Span;
            bool isHex = row.HasComplexChildren;

            if (!isHex)
            {
                // Base64: write b"content" directly
                Span<byte> buffer = stackalloc byte[3 + content.Length]; // b + " + content + "
                buffer[0] = RdnConstants.LetterB;
                buffer[1] = RdnConstants.Quote;
                content.CopyTo(buffer.Slice(2));
                buffer[2 + content.Length] = RdnConstants.Quote;
                writer.WriteRawValue(buffer, skipInputValidation: true);
            }
            else
            {
                // Hex: decode hex to bytes, then write as canonical base64
                int byteCount = content.Length / 2;
                byte[] decoded = new byte[byteCount];
                for (int i = 0; i < byteCount; i++)
                {
                    int hi = HexConverter.FromChar(content[i * 2]);
                    int lo = HexConverter.FromChar(content[i * 2 + 1]);
                    decoded[i] = (byte)((hi << 4) | lo);
                }
                writer.WriteRdnBinaryValue(decoded);
            }
        }

        private void WriteComplexElement(int index, Utf8RdnWriter writer)
        {
            int endIndex = GetEndIndex(index, true);
            DbRow startRow = _parsedData.Get(index);

            // Track Map key/value state using bitmasks instead of a heap-allocated Stack.
            // Bit set at depth N in _mapMask = depth N is a Map container.
            // Bit set at depth N in _arrowMask = next value at depth N needs a => prefix.
            long mapMask = 0;
            long arrowMask = 0;
            int depth = 0;

            if (startRow.TokenType == RdnTokenType.StartMap)
            {
                mapMask = 1L; // depth 0 is a map
            }

            for (int i = index + DbRow.Size; i < endIndex; i += DbRow.Size)
            {
                DbRow row = _parsedData.Get(i);
                long depthBit = depth < 64 ? (1L << depth) : 0;
                bool isMap = (mapMask & depthBit) != 0;

                // Write map arrow before value tokens that are direct children of a Map
                bool wroteArrow = false;
                if (isMap && (arrowMask & depthBit) != 0 && row.TokenType != RdnTokenType.EndMap)
                {
                    writer.WriteMapArrow();
                    wroteArrow = true;
                }

                switch (row.TokenType)
                {
                    case RdnTokenType.String:
                        WriteString(row, writer);
                        if (isMap) { if (wroteArrow) arrowMask &= ~depthBit; else arrowMask |= depthBit; }
                        continue;
                    case RdnTokenType.Number:
                        writer.WriteNumberValue(_utf8Rdn.Slice(row.Location, row.SizeOrLength).Span);
                        if (isMap) { if (wroteArrow) arrowMask &= ~depthBit; else arrowMask |= depthBit; }
                        continue;
                    case RdnTokenType.True:
                        writer.WriteBooleanValue(value: true);
                        if (isMap) { if (wroteArrow) arrowMask &= ~depthBit; else arrowMask |= depthBit; }
                        continue;
                    case RdnTokenType.False:
                        writer.WriteBooleanValue(value: false);
                        if (isMap) { if (wroteArrow) arrowMask &= ~depthBit; else arrowMask |= depthBit; }
                        continue;
                    case RdnTokenType.Null:
                        writer.WriteNullValue();
                        if (isMap) { if (wroteArrow) arrowMask &= ~depthBit; else arrowMask |= depthBit; }
                        continue;
                    case RdnTokenType.RdnDateTime:
                    case RdnTokenType.RdnTimeOnly:
                    case RdnTokenType.RdnDuration:
                        WriteRdnLiteral(row, writer);
                        if (isMap) { if (wroteArrow) arrowMask &= ~depthBit; else arrowMask |= depthBit; }
                        continue;
                    case RdnTokenType.RdnRegExp:
                        WriteRdnRegExp(row, writer);
                        if (isMap) { if (wroteArrow) arrowMask &= ~depthBit; else arrowMask |= depthBit; }
                        continue;
                    case RdnTokenType.RdnBinary:
                        WriteRdnBinary(row, writer);
                        if (isMap) { if (wroteArrow) arrowMask &= ~depthBit; else arrowMask |= depthBit; }
                        continue;
                    case RdnTokenType.StartObject:
                        writer.WriteStartObject();
                        if (isMap) { if (wroteArrow) arrowMask &= ~depthBit; else arrowMask |= depthBit; }
                        depth++;
                        continue;
                    case RdnTokenType.EndObject:
                        writer.WriteEndObject();
                        depth--;
                        continue;
                    case RdnTokenType.StartArray:
                        writer.WriteStartArray();
                        if (isMap) { if (wroteArrow) arrowMask &= ~depthBit; else arrowMask |= depthBit; }
                        depth++;
                        continue;
                    case RdnTokenType.EndArray:
                        writer.WriteEndArray();
                        depth--;
                        continue;
                    case RdnTokenType.StartSet:
                        writer.WriteStartSet(forceTypeName: row.SizeOrLength == 0);
                        if (isMap) { if (wroteArrow) arrowMask &= ~depthBit; else arrowMask |= depthBit; }
                        depth++;
                        continue;
                    case RdnTokenType.EndSet:
                        writer.WriteEndSet();
                        depth--;
                        continue;
                    case RdnTokenType.StartMap:
                        writer.WriteStartMap(forceTypeName: row.SizeOrLength == 0);
                        if (isMap) { if (wroteArrow) arrowMask &= ~depthBit; else arrowMask |= depthBit; }
                        depth++;
                        if (depth < 64) mapMask |= (1L << depth);
                        continue;
                    case RdnTokenType.EndMap:
                        writer.WriteEndMap();
                        if (depth < 64) { mapMask &= ~(1L << depth); arrowMask &= ~(1L << depth); }
                        depth--;
                        continue;
                    case RdnTokenType.PropertyName:
                        WritePropertyName(row, writer);
                        continue;
                }

                Debug.Fail($"Unexpected encounter with RdnTokenType {row.TokenType}");
            }
        }

        private ReadOnlySpan<byte> UnescapeString(in DbRow row, out ArraySegment<byte> rented)
        {
            Debug.Assert(row.TokenType == RdnTokenType.String || row.TokenType == RdnTokenType.PropertyName);
            int loc = row.Location;
            int length = row.SizeOrLength;
            ReadOnlySpan<byte> text = _utf8Rdn.Slice(loc, length).Span;

            if (!row.HasComplexChildren)
            {
                rented = default;
                return text;
            }

            byte[] rent = ArrayPool<byte>.Shared.Rent(length);
            RdnReaderHelper.Unescape(text, rent, out int written);
            rented = new ArraySegment<byte>(rent, 0, written);
            return rented.AsSpan();
        }

        private static void ClearAndReturn(ArraySegment<byte> rented)
        {
            if (rented.Array != null)
            {
                rented.AsSpan().Clear();
                ArrayPool<byte>.Shared.Return(rented.Array);
            }
        }

        internal void WritePropertyName(int index, Utf8RdnWriter writer)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index - DbRow.Size);
            Debug.Assert(row.TokenType == RdnTokenType.PropertyName);
            WritePropertyName(row, writer);
        }

        private void WritePropertyName(in DbRow row, Utf8RdnWriter writer)
        {
            ArraySegment<byte> rented = default;

            try
            {
                writer.WritePropertyName(UnescapeString(row, out rented));
            }
            finally
            {
                ClearAndReturn(rented);
            }
        }

        private void WriteString(in DbRow row, Utf8RdnWriter writer)
        {
            ArraySegment<byte> rented = default;

            try
            {
                writer.WriteStringValue(UnescapeString(row, out rented));
            }
            finally
            {
                ClearAndReturn(rented);
            }
        }

        private static void Parse(
            ReadOnlySpan<byte> utf8RdnSpan,
            RdnReaderOptions readerOptions,
            ref MetadataDb database,
            ref StackRowStack stack)
        {
            bool inArray = false;
            int arrayItemsOrPropertyCount = 0;
            int numberOfRowsForMembers = 0;
            int numberOfRowsForValues = 0;

            Utf8RdnReader reader = new Utf8RdnReader(
                utf8RdnSpan,
                isFinalBlock: true,
                new RdnReaderState(options: readerOptions));

            while (reader.Read())
            {
                RdnTokenType tokenType = reader.TokenType;

                // Since the input payload is contained within a Span,
                // token start index can never be larger than int.MaxValue (i.e. utf8RdnSpan.Length).
                Debug.Assert(reader.TokenStartIndex <= int.MaxValue);
                int tokenStart = (int)reader.TokenStartIndex;

                if (tokenType == RdnTokenType.StartObject)
                {
                    if (inArray)
                    {
                        arrayItemsOrPropertyCount++;
                    }

                    numberOfRowsForValues++;
                    database.Append(tokenType, tokenStart, DbRow.UnknownSize);
                    var row = new StackRow(arrayItemsOrPropertyCount, numberOfRowsForMembers + 1);
                    stack.Push(row);
                    arrayItemsOrPropertyCount = 0;
                    numberOfRowsForMembers = 0;
                }
                else if (tokenType == RdnTokenType.EndObject)
                {
                    int rowIndex = database.FindIndexOfFirstUnsetSizeOrLength(RdnTokenType.StartObject);

                    numberOfRowsForValues++;
                    numberOfRowsForMembers++;
                    database.SetLength(rowIndex, arrayItemsOrPropertyCount);

                    int newRowIndex = database.Length;
                    database.Append(tokenType, tokenStart, reader.ValueSpan.Length);
                    database.SetNumberOfRows(rowIndex, numberOfRowsForMembers);
                    database.SetNumberOfRows(newRowIndex, numberOfRowsForMembers);

                    StackRow row = stack.Pop();
                    arrayItemsOrPropertyCount = row.SizeOrLength;
                    numberOfRowsForMembers += row.NumberOfRows;
                }
                else if (tokenType == RdnTokenType.StartArray)
                {
                    if (inArray)
                    {
                        arrayItemsOrPropertyCount++;
                    }

                    numberOfRowsForMembers++;
                    database.Append(tokenType, tokenStart, DbRow.UnknownSize);
                    var row = new StackRow(arrayItemsOrPropertyCount, numberOfRowsForValues + 1);
                    stack.Push(row);
                    arrayItemsOrPropertyCount = 0;
                    numberOfRowsForValues = 0;
                }
                else if (tokenType == RdnTokenType.EndArray)
                {
                    int rowIndex = database.FindIndexOfFirstUnsetSizeOrLength(RdnTokenType.StartArray);

                    numberOfRowsForValues++;
                    numberOfRowsForMembers++;
                    database.SetLength(rowIndex, arrayItemsOrPropertyCount);
                    database.SetNumberOfRows(rowIndex, numberOfRowsForValues);

                    // If the array item count is (e.g.) 12 and the number of rows is (e.g.) 13
                    // then the extra row is just this EndArray item, so the array was made up
                    // of simple values.
                    //
                    // If the off-by-one relationship does not hold, then one of the values was
                    // more than one row, making it a complex object.
                    //
                    // This check is similar to tracking the start array and painting it when
                    // StartObject or StartArray is encountered, but avoids the mixed state
                    // where "UnknownSize" implies "has complex children".
                    if (arrayItemsOrPropertyCount + 1 != numberOfRowsForValues)
                    {
                        database.SetHasComplexChildren(rowIndex);
                    }

                    int newRowIndex = database.Length;
                    database.Append(tokenType, tokenStart, reader.ValueSpan.Length);
                    database.SetNumberOfRows(newRowIndex, numberOfRowsForValues);

                    StackRow row = stack.Pop();
                    arrayItemsOrPropertyCount = row.SizeOrLength;
                    numberOfRowsForValues += row.NumberOfRows;
                }
                else if (tokenType == RdnTokenType.StartSet)
                {
                    if (inArray)
                    {
                        arrayItemsOrPropertyCount++;
                    }

                    numberOfRowsForMembers++;
                    database.Append(tokenType, tokenStart, DbRow.UnknownSize);
                    var row = new StackRow(arrayItemsOrPropertyCount, numberOfRowsForValues + 1);
                    stack.Push(row);
                    arrayItemsOrPropertyCount = 0;
                    numberOfRowsForValues = 0;
                }
                else if (tokenType == RdnTokenType.EndSet)
                {
                    int rowIndex = database.FindIndexOfFirstUnsetSizeOrLength(RdnTokenType.StartSet);

                    numberOfRowsForValues++;
                    numberOfRowsForMembers++;
                    database.SetLength(rowIndex, arrayItemsOrPropertyCount);
                    database.SetNumberOfRows(rowIndex, numberOfRowsForValues);

                    if (arrayItemsOrPropertyCount + 1 != numberOfRowsForValues)
                    {
                        database.SetHasComplexChildren(rowIndex);
                    }

                    int newRowIndex = database.Length;
                    database.Append(tokenType, tokenStart, reader.ValueSpan.Length);
                    database.SetNumberOfRows(newRowIndex, numberOfRowsForValues);

                    StackRow row = stack.Pop();
                    arrayItemsOrPropertyCount = row.SizeOrLength;
                    numberOfRowsForValues += row.NumberOfRows;
                }
                else if (tokenType == RdnTokenType.StartMap)
                {
                    if (inArray)
                    {
                        arrayItemsOrPropertyCount++;
                    }

                    numberOfRowsForMembers++;
                    database.Append(tokenType, tokenStart, DbRow.UnknownSize);
                    var row = new StackRow(arrayItemsOrPropertyCount, numberOfRowsForValues + 1);
                    stack.Push(row);
                    arrayItemsOrPropertyCount = 0;
                    numberOfRowsForValues = 0;
                }
                else if (tokenType == RdnTokenType.EndMap)
                {
                    int rowIndex = database.FindIndexOfFirstUnsetSizeOrLength(RdnTokenType.StartMap);

                    numberOfRowsForValues++;
                    numberOfRowsForMembers++;
                    database.SetLength(rowIndex, arrayItemsOrPropertyCount);
                    database.SetNumberOfRows(rowIndex, numberOfRowsForValues);

                    if (arrayItemsOrPropertyCount + 1 != numberOfRowsForValues)
                    {
                        database.SetHasComplexChildren(rowIndex);
                    }

                    int newRowIndex = database.Length;
                    database.Append(tokenType, tokenStart, reader.ValueSpan.Length);
                    database.SetNumberOfRows(newRowIndex, numberOfRowsForValues);

                    StackRow row = stack.Pop();
                    arrayItemsOrPropertyCount = row.SizeOrLength;
                    numberOfRowsForValues += row.NumberOfRows;
                }
                else if (tokenType == RdnTokenType.PropertyName)
                {
                    numberOfRowsForValues++;
                    numberOfRowsForMembers++;
                    arrayItemsOrPropertyCount++;

                    // Adding 1 to skip the start quote will never overflow
                    Debug.Assert(tokenStart < int.MaxValue);

                    database.Append(tokenType, tokenStart + 1, reader.ValueSpan.Length);

                    if (reader.ValueIsEscaped)
                    {
                        database.SetHasComplexChildren(database.Length - DbRow.Size);
                    }

                    Debug.Assert(!inArray);
                }
                else
                {
                    Debug.Assert((tokenType >= RdnTokenType.String && tokenType <= RdnTokenType.Null) || tokenType == RdnTokenType.RdnDateTime || tokenType == RdnTokenType.RdnTimeOnly || tokenType == RdnTokenType.RdnDuration || tokenType == RdnTokenType.RdnRegExp || tokenType == RdnTokenType.RdnBinary);
                    numberOfRowsForValues++;
                    numberOfRowsForMembers++;

                    if (inArray)
                    {
                        arrayItemsOrPropertyCount++;
                    }

                    if (tokenType == RdnTokenType.String)
                    {
                        // Adding 1 to skip the start quote will never overflow
                        Debug.Assert(tokenStart < int.MaxValue);

                        database.Append(tokenType, tokenStart + 1, reader.ValueSpan.Length);

                        if (reader.ValueIsEscaped)
                        {
                            database.SetHasComplexChildren(database.Length - DbRow.Size);
                        }
                    }
                    else if (tokenType == RdnTokenType.RdnDateTime || tokenType == RdnTokenType.RdnTimeOnly || tokenType == RdnTokenType.RdnDuration)
                    {
                        // Adding 1 to skip the @ prefix will never overflow
                        Debug.Assert(tokenStart < int.MaxValue);

                        database.Append(tokenType, tokenStart + 1, reader.ValueSpan.Length);
                    }
                    else if (tokenType == RdnTokenType.RdnRegExp)
                    {
                        // Store the full /pattern/flags span as the value
                        // ValueSpan from reader contains pattern/flags (without outer slashes)
                        database.Append(tokenType, tokenStart + 1, reader.ValueSpan.Length);

                        if (reader.ValueIsEscaped)
                        {
                            database.SetHasComplexChildren(database.Length - DbRow.Size);
                        }
                    }
                    else if (tokenType == RdnTokenType.RdnBinary)
                    {
                        // Adding 2 to skip the b" or x" prefix will never overflow
                        Debug.Assert(tokenStart < int.MaxValue - 1);

                        database.Append(tokenType, tokenStart + 2, reader.ValueSpan.Length);

                        // Use HasComplexChildren to store encoding type: true = hex, false = base64
                        // reader.ValueIsEscaped is repurposed: true = hex, false = base64
                        if (reader.ValueIsEscaped)
                        {
                            database.SetHasComplexChildren(database.Length - DbRow.Size);
                        }
                    }
                    else
                    {
                        database.Append(tokenType, tokenStart, reader.ValueSpan.Length);
                    }
                }

                inArray = reader.IsInArray;
            }

            Debug.Assert(reader.BytesConsumed == utf8RdnSpan.Length);
            database.CompleteAllocations();
        }

        private static void ValidateNoDuplicateProperties(RdnDocument document)
        {
            if (document.RootElement.ValueKind is RdnValueKind.Array or RdnValueKind.Object or RdnValueKind.Set or RdnValueKind.Map)
            {
                ValidateDuplicatePropertiesCore(document);
            }
        }

        private static void ValidateDuplicatePropertiesCore(RdnDocument document)
        {
            Debug.Assert(document.RootElement.ValueKind is RdnValueKind.Array or RdnValueKind.Object or RdnValueKind.Set or RdnValueKind.Map);

            using PropertyNameSet propertyNameSet = new PropertyNameSet();

            Stack<int> traversalPath = new Stack<int>();
            int? databaseIndexOflastProcessedChild = null;

            traversalPath.Push(document.RootElement.MetadataDbIndex);

            do
            {
                RdnElement curr = new RdnElement(document, traversalPath.Peek());

                switch (curr.ValueKind)
                {
                    case RdnValueKind.Object:
                    {
                        RdnElement.ObjectEnumerator enumerator = new(curr, databaseIndexOflastProcessedChild ?? -1);

                        while (enumerator.MoveNext())
                        {
                            if (enumerator.Current.Value.ValueKind is RdnValueKind.Object or RdnValueKind.Array or RdnValueKind.Set)
                            {
                                traversalPath.Push(enumerator.Current.Value.MetadataDbIndex);
                                databaseIndexOflastProcessedChild = null;
                                goto continueOuter;
                            }
                        }

                        // No more children, so process the current element.
                        enumerator.Reset();
                        propertyNameSet.SetCapacity(curr.GetPropertyCount());

                        foreach (RdnProperty property in enumerator)
                        {
                            propertyNameSet.AddPropertyName(property, document);
                        }

                        propertyNameSet.Reset();
                        databaseIndexOflastProcessedChild = traversalPath.Pop();
                        break;
                    }
                    case RdnValueKind.Array:
                    case RdnValueKind.Set:
                    case RdnValueKind.Map:
                    {
                        RdnElement.ArrayEnumerator enumerator = new(curr, databaseIndexOflastProcessedChild ?? -1);

                        while (enumerator.MoveNext())
                        {
                            if (enumerator.Current.ValueKind is RdnValueKind.Object or RdnValueKind.Array or RdnValueKind.Set or RdnValueKind.Map)
                            {
                                traversalPath.Push(enumerator.Current.MetadataDbIndex);
                                databaseIndexOflastProcessedChild = null;
                                goto continueOuter;
                            }
                        }

                        databaseIndexOflastProcessedChild = traversalPath.Pop();
                        break;
                    }
                    default:
                        Debug.Fail($"Expected only complex children but got {curr.ValueKind}");
                        ThrowHelper.ThrowRdnException();
                        break;
                }

            continueOuter:
                ;
            } while (traversalPath.Count is not 0);
        }

        private void CheckNotDisposed()
        {
            if (_utf8Rdn.IsEmpty)
            {
                ThrowHelper.ThrowObjectDisposedException_RdnDocument();
            }
        }

        private static void CheckExpectedType(RdnTokenType expected, RdnTokenType actual)
        {
            if (expected != actual)
            {
                ThrowHelper.ThrowRdnElementWrongTypeException(expected, actual);
            }
        }

        private static void CheckSupportedOptions(
            RdnReaderOptions readerOptions,
            string paramName)
        {
            // Since these are coming from a valid instance of Utf8RdnReader, the RdnReaderOptions must already be valid
            Debug.Assert(readerOptions.CommentHandling >= 0 && readerOptions.CommentHandling <= RdnCommentHandling.Allow);

            if (readerOptions.CommentHandling == RdnCommentHandling.Allow)
            {
                throw new ArgumentException(SR.RdnDocumentDoesNotSupportComments, paramName);
            }
        }
    }
}
