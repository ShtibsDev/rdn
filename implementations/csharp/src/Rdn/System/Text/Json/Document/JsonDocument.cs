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
    ///   Represents the structure of a JSON value in a lightweight, read-only form.
    /// </summary>
    /// <remarks>
    ///   This class utilizes resources from pooled memory to minimize the garbage collector (GC)
    ///   impact in high-usage scenarios. Failure to properly Dispose this object will result in
    ///   the memory not being returned to the pool, which will cause an increase in GC impact across
    ///   various parts of the framework.
    /// </remarks>
    public sealed partial class JsonDocument : IDisposable
    {
        private ReadOnlyMemory<byte> _utf8Json;
        private MetadataDb _parsedData;

        private byte[]? _extraRentedArrayPoolBytes;
        private PooledByteBufferWriter? _extraPooledByteBufferWriter;

        internal bool IsDisposable { get; }

        /// <summary>
        ///   The <see cref="JsonElement"/> representing the value of the document.
        /// </summary>
        public JsonElement RootElement => new JsonElement(this, 0);

        private JsonDocument(
            ReadOnlyMemory<byte> utf8Json,
            MetadataDb parsedData,
            byte[]? extraRentedArrayPoolBytes = null,
            PooledByteBufferWriter? extraPooledByteBufferWriter = null,
            bool isDisposable = true)
        {
            Debug.Assert(!utf8Json.IsEmpty);

            // Both rented values better be null if we're not disposable.
            Debug.Assert(isDisposable ||
                (extraRentedArrayPoolBytes == null && extraPooledByteBufferWriter == null));

            // Both rented values can't be specified.
            Debug.Assert(extraRentedArrayPoolBytes == null || extraPooledByteBufferWriter == null);

            _utf8Json = utf8Json;
            _parsedData = parsedData;
            _extraRentedArrayPoolBytes = extraRentedArrayPoolBytes;
            _extraPooledByteBufferWriter = extraPooledByteBufferWriter;
            IsDisposable = isDisposable;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            int length = _utf8Json.Length;
            if (length == 0 || !IsDisposable)
            {
                return;
            }

            _parsedData.Dispose();
            _utf8Json = ReadOnlyMemory<byte>.Empty;

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
        ///  Write the document into the provided writer as a JSON value.
        /// </summary>
        /// <param name="writer"></param>
        /// <exception cref="ArgumentNullException">
        ///   The <paramref name="writer"/> parameter is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        ///   This <see cref="RootElement"/>'s <see cref="JsonElement.ValueKind"/> would result in an invalid JSON.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public void WriteTo(Utf8JsonWriter writer)
        {
            ArgumentNullException.ThrowIfNull(writer);

            RootElement.WriteTo(writer);
        }

        internal JsonTokenType GetJsonTokenType(int index)
        {
            CheckNotDisposed();

            return _parsedData.GetJsonTokenType(index);
        }

        internal bool ValueIsEscaped(int index, bool isPropertyName)
        {
            CheckNotDisposed();

            int matchIndex = isPropertyName ? index - DbRow.Size : index;
            DbRow row = _parsedData.Get(matchIndex);
            Debug.Assert(!isPropertyName || row.TokenType is JsonTokenType.PropertyName);

            return row.HasComplexChildren;
        }

        internal int GetArrayLength(int index)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            if (row.TokenType != JsonTokenType.StartArray && row.TokenType != JsonTokenType.StartSet && row.TokenType != JsonTokenType.StartMap)
            {
                ThrowHelper.ThrowJsonElementWrongTypeException(JsonTokenType.StartArray, row.TokenType);
            }

            return row.SizeOrLength;
        }

        internal int GetPropertyCount(int index)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(JsonTokenType.StartObject, row.TokenType);

            return row.SizeOrLength;
        }

        internal JsonElement GetArrayIndexElement(int currentIndex, int arrayIndex)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(currentIndex);

            if (row.TokenType != JsonTokenType.StartArray && row.TokenType != JsonTokenType.StartSet && row.TokenType != JsonTokenType.StartMap)
            {
                ThrowHelper.ThrowJsonElementWrongTypeException(JsonTokenType.StartArray, row.TokenType);
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
                return new JsonElement(this, currentIndex + ((arrayIndex + 1) * DbRow.Size));
            }

            int elementCount = 0;
            int objectOffset = currentIndex + DbRow.Size;

            for (; objectOffset < _parsedData.Length; objectOffset += DbRow.Size)
            {
                if (arrayIndex == elementCount)
                {
                    return new JsonElement(this, objectOffset);
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
                if (includeQuotes && row.TokenType == JsonTokenType.String)
                {
                    // Start one character earlier than the value (the open quote)
                    // End one character after the value (the close quote)
                    return _utf8Json.Slice(row.Location - 1, row.SizeOrLength + 2);
                }

                return _utf8Json.Slice(row.Location, row.SizeOrLength);
            }

            int endElementIdx = GetEndIndex(index, includeEndElement: false);
            int start = row.Location;
            row = _parsedData.Get(endElementIdx);
            return _utf8Json.Slice(start, row.Location - start + row.SizeOrLength);
        }

        private ReadOnlyMemory<byte> GetPropertyRawValue(int valueIndex)
        {
            CheckNotDisposed();

            // The property name is stored one row before the value
            DbRow row = _parsedData.Get(valueIndex - DbRow.Size);
            Debug.Assert(row.TokenType == JsonTokenType.PropertyName);

            // Subtract one for the open quote.
            int start = row.Location - 1;
            int end;

            row = _parsedData.Get(valueIndex);

            if (row.IsSimpleValue)
            {
                end = row.Location + row.SizeOrLength;

                // If the value was a string, pick up the terminating quote.
                if (row.TokenType == JsonTokenType.String)
                {
                    end++;
                }

                return _utf8Json.Slice(start, end - start);
            }

            int endElementIdx = GetEndIndex(valueIndex, includeEndElement: false);
            row = _parsedData.Get(endElementIdx);
            end = row.Location + row.SizeOrLength;
            return _utf8Json.Slice(start, end - start);
        }

        internal string? GetString(int index, JsonTokenType expectedType)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            JsonTokenType tokenType = row.TokenType;

            if (tokenType == JsonTokenType.Null)
            {
                return null;
            }

            CheckExpectedType(expectedType, tokenType);

            ReadOnlySpan<byte> data = _utf8Json.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            return row.HasComplexChildren
                ? JsonReaderHelper.GetUnescapedString(segment)
                : JsonReaderHelper.TranscodeHelper(segment);
        }

        internal bool TextEquals(int index, ReadOnlySpan<char> otherText, bool isPropertyName)
        {
            CheckNotDisposed();

            byte[]? otherUtf8TextArray = null;

            int length = checked(otherText.Length * JsonConstants.MaxExpansionFactorWhileTranscoding);
            Span<byte> otherUtf8Text = length <= JsonConstants.StackallocByteThreshold ?
                stackalloc byte[JsonConstants.StackallocByteThreshold] :
                (otherUtf8TextArray = ArrayPool<byte>.Shared.Rent(length));

            OperationStatus status = JsonWriterHelper.ToUtf8(otherText, otherUtf8Text, out int written);
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
                isPropertyName ? JsonTokenType.PropertyName : JsonTokenType.String,
                row.TokenType);

            ReadOnlySpan<byte> data = _utf8Json.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (otherUtf8Text.Length > segment.Length || (!shouldUnescape && otherUtf8Text.Length != segment.Length))
            {
                return false;
            }

            if (row.HasComplexChildren && shouldUnescape)
            {
                if (otherUtf8Text.Length < segment.Length / JsonConstants.MaxExpansionFactorWhileEscaping)
                {
                    return false;
                }

                int idx = segment.IndexOf(JsonConstants.BackSlash);
                Debug.Assert(idx != -1);

                if (!otherUtf8Text.StartsWith(segment.Slice(0, idx)))
                {
                    return false;
                }

                return JsonReaderHelper.UnescapeAndCompare(segment.Slice(idx), otherUtf8Text.Slice(idx));
            }

            return segment.SequenceEqual(otherUtf8Text);
        }

        internal string GetNameOfPropertyValue(int index)
        {
            // The property name is one row before the property value
            return GetString(index - DbRow.Size, JsonTokenType.PropertyName)!;
        }

        internal ReadOnlySpan<byte> GetPropertyNameRaw(int index)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index - DbRow.Size);
            Debug.Assert(row.TokenType is JsonTokenType.PropertyName);

            return _utf8Json.Span.Slice(row.Location, row.SizeOrLength);
        }

        internal bool TryGetValue(int index, [NotNullWhen(true)] out byte[]? value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(JsonTokenType.String, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Json.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            // Segment needs to be unescaped
            if (row.HasComplexChildren)
            {
                return JsonReaderHelper.TryGetUnescapedBase64Bytes(segment, out value);
            }

            Debug.Assert(!segment.Contains(JsonConstants.BackSlash));
            return JsonReaderHelper.TryDecodeBase64(segment, out value);
        }

        internal bool TryGetValue(int index, out sbyte value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(JsonTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Json.Span;
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

            CheckExpectedType(JsonTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Json.Span;
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

            CheckExpectedType(JsonTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Json.Span;
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

            CheckExpectedType(JsonTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Json.Span;
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

            CheckExpectedType(JsonTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Json.Span;
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

            CheckExpectedType(JsonTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Json.Span;
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

            CheckExpectedType(JsonTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Json.Span;
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

            CheckExpectedType(JsonTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Json.Span;
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

            CheckExpectedType(JsonTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Json.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (Utf8Parser.TryParse(segment, out double tmp, out int bytesConsumed) &&
                segment.Length == bytesConsumed)
            {
                value = tmp;
                return true;
            }

            // Fall back to NaN/Infinity/−Infinity
            if (JsonReaderHelper.TryGetFloatingPointConstant(segment, out value))
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

            CheckExpectedType(JsonTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Json.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            if (Utf8Parser.TryParse(segment, out float tmp, out int bytesConsumed) &&
                segment.Length == bytesConsumed)
            {
                value = tmp;
                return true;
            }

            // Fall back to NaN/Infinity/−Infinity
            if (JsonReaderHelper.TryGetFloatingPointConstant(segment, out value))
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

            CheckExpectedType(JsonTokenType.Number, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Json.Span;
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

            if (row.TokenType == JsonTokenType.RdnDateTime)
            {
                ReadOnlySpan<byte> data = _utf8Json.Span;
                ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);
                return TryParseRdnDateTime(segment, out value);
            }

            CheckExpectedType(JsonTokenType.String, row.TokenType);

            ReadOnlySpan<byte> data2 = _utf8Json.Span;
            ReadOnlySpan<byte> segment2 = data2.Slice(row.Location, row.SizeOrLength);

            return JsonReaderHelper.TryGetValue(segment2, row.HasComplexChildren, out value);
        }

        internal bool TryGetValue(int index, out DateTimeOffset value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            if (row.TokenType == JsonTokenType.RdnDateTime)
            {
                ReadOnlySpan<byte> data = _utf8Json.Span;
                ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);
                if (TryParseRdnDateTime(segment, out DateTime dt))
                {
                    value = new DateTimeOffset(dt, TimeSpan.Zero);
                    return true;
                }
                value = default;
                return false;
            }

            CheckExpectedType(JsonTokenType.String, row.TokenType);

            ReadOnlySpan<byte> data2 = _utf8Json.Span;
            ReadOnlySpan<byte> segment2 = data2.Slice(row.Location, row.SizeOrLength);

            return JsonReaderHelper.TryGetValue(segment2, row.HasComplexChildren, out value);
        }

        private static bool TryParseRdnDateTime(ReadOnlySpan<byte> span, out DateTime value)
        {
            if (span.Length == 0)
            {
                value = default;
                return false;
            }

            // Unix timestamp: all digits, no hyphens or colons
            if (span.Length <= 13 && JsonHelpers.IsDigit(span[0]) && !span.Contains(JsonConstants.Hyphen) && !span.Contains(JsonConstants.Colon))
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
            if (JsonHelpers.TryParseAsISO(span, out DateTime tmp))
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

            if (row.TokenType != JsonTokenType.RdnTimeOnly)
            {
                value = default;
                return false;
            }

            ReadOnlySpan<byte> data = _utf8Json.Span;
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

            if (row.TokenType != JsonTokenType.RdnDuration)
            {
                value = default;
                return false;
            }

            ReadOnlySpan<byte> data = _utf8Json.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            value = new RdnDuration(System.Text.Encoding.UTF8.GetString(segment));
            return true;
        }

        internal bool TryGetValue(int index, out Guid value)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            CheckExpectedType(JsonTokenType.String, row.TokenType);

            ReadOnlySpan<byte> data = _utf8Json.Span;
            ReadOnlySpan<byte> segment = data.Slice(row.Location, row.SizeOrLength);

            return JsonReaderHelper.TryGetValue(segment, row.HasComplexChildren, out value);
        }

        internal string GetRawValueAsString(int index)
        {
            ReadOnlyMemory<byte> segment = GetRawValue(index, includeQuotes: true);
            return JsonReaderHelper.TranscodeHelper(segment.Span);
        }

        internal string GetPropertyRawValueAsString(int valueIndex)
        {
            ReadOnlyMemory<byte> segment = GetPropertyRawValue(valueIndex);
            return JsonReaderHelper.TranscodeHelper(segment.Span);
        }

        internal JsonElement CloneElement(int index)
        {
            int endIndex = GetEndIndex(index, true);
            MetadataDb newDb = _parsedData.CopySegment(index, endIndex);
            ReadOnlyMemory<byte> segmentCopy = GetRawValue(index, includeQuotes: true).ToArray();

            JsonDocument newDocument =
                new JsonDocument(
                    segmentCopy,
                    newDb,
                    extraRentedArrayPoolBytes: null,
                    extraPooledByteBufferWriter: null,
                    isDisposable: false);

            return newDocument.RootElement;
        }

        internal void WriteElementTo(
            int index,
            Utf8JsonWriter writer)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index);

            switch (row.TokenType)
            {
                case JsonTokenType.StartObject:
                    writer.WriteStartObject();
                    WriteComplexElement(index, writer);
                    return;
                case JsonTokenType.StartArray:
                    writer.WriteStartArray();
                    WriteComplexElement(index, writer);
                    return;
                case JsonTokenType.StartSet:
                    writer.WriteStartSet();
                    WriteComplexElement(index, writer);
                    return;
                case JsonTokenType.StartMap:
                    writer.WriteStartMap();
                    WriteComplexElement(index, writer);
                    return;
                case JsonTokenType.String:
                    WriteString(row, writer);
                    return;
                case JsonTokenType.Number:
                    writer.WriteNumberValue(_utf8Json.Slice(row.Location, row.SizeOrLength).Span);
                    return;
                case JsonTokenType.True:
                    writer.WriteBooleanValue(value: true);
                    return;
                case JsonTokenType.False:
                    writer.WriteBooleanValue(value: false);
                    return;
                case JsonTokenType.Null:
                    writer.WriteNullValue();
                    return;
                case JsonTokenType.RdnDateTime:
                case JsonTokenType.RdnTimeOnly:
                case JsonTokenType.RdnDuration:
                    WriteRdnLiteral(row, writer);
                    return;
            }

            Debug.Fail($"Unexpected encounter with JsonTokenType {row.TokenType}");
        }

        private void WriteRdnLiteral(DbRow row, Utf8JsonWriter writer)
        {
            ReadOnlySpan<byte> body = _utf8Json.Slice(row.Location, row.SizeOrLength).Span;
            // Write as raw: @ + body
            Span<byte> buffer = stackalloc byte[1 + body.Length];
            buffer[0] = JsonConstants.AtSign;
            body.CopyTo(buffer.Slice(1));
            writer.WriteRawValue(buffer, skipInputValidation: true);
        }

        private void WriteComplexElement(int index, Utf8JsonWriter writer)
        {
            int endIndex = GetEndIndex(index, true);
            DbRow startRow = _parsedData.Get(index);

            // Track Map key/value state using bitmasks instead of a heap-allocated Stack.
            // Bit set at depth N in _mapMask = depth N is a Map container.
            // Bit set at depth N in _arrowMask = next value at depth N needs a => prefix.
            long mapMask = 0;
            long arrowMask = 0;
            int depth = 0;

            if (startRow.TokenType == JsonTokenType.StartMap)
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
                if (isMap && (arrowMask & depthBit) != 0 && row.TokenType != JsonTokenType.EndMap)
                {
                    writer.WriteMapArrow();
                    wroteArrow = true;
                }

                switch (row.TokenType)
                {
                    case JsonTokenType.String:
                        WriteString(row, writer);
                        if (isMap) { if (wroteArrow) arrowMask &= ~depthBit; else arrowMask |= depthBit; }
                        continue;
                    case JsonTokenType.Number:
                        writer.WriteNumberValue(_utf8Json.Slice(row.Location, row.SizeOrLength).Span);
                        if (isMap) { if (wroteArrow) arrowMask &= ~depthBit; else arrowMask |= depthBit; }
                        continue;
                    case JsonTokenType.True:
                        writer.WriteBooleanValue(value: true);
                        if (isMap) { if (wroteArrow) arrowMask &= ~depthBit; else arrowMask |= depthBit; }
                        continue;
                    case JsonTokenType.False:
                        writer.WriteBooleanValue(value: false);
                        if (isMap) { if (wroteArrow) arrowMask &= ~depthBit; else arrowMask |= depthBit; }
                        continue;
                    case JsonTokenType.Null:
                        writer.WriteNullValue();
                        if (isMap) { if (wroteArrow) arrowMask &= ~depthBit; else arrowMask |= depthBit; }
                        continue;
                    case JsonTokenType.RdnDateTime:
                    case JsonTokenType.RdnTimeOnly:
                    case JsonTokenType.RdnDuration:
                        WriteRdnLiteral(row, writer);
                        if (isMap) { if (wroteArrow) arrowMask &= ~depthBit; else arrowMask |= depthBit; }
                        continue;
                    case JsonTokenType.StartObject:
                        writer.WriteStartObject();
                        if (isMap) { if (wroteArrow) arrowMask &= ~depthBit; else arrowMask |= depthBit; }
                        depth++;
                        continue;
                    case JsonTokenType.EndObject:
                        writer.WriteEndObject();
                        depth--;
                        continue;
                    case JsonTokenType.StartArray:
                        writer.WriteStartArray();
                        if (isMap) { if (wroteArrow) arrowMask &= ~depthBit; else arrowMask |= depthBit; }
                        depth++;
                        continue;
                    case JsonTokenType.EndArray:
                        writer.WriteEndArray();
                        depth--;
                        continue;
                    case JsonTokenType.StartSet:
                        writer.WriteStartSet();
                        if (isMap) { if (wroteArrow) arrowMask &= ~depthBit; else arrowMask |= depthBit; }
                        depth++;
                        continue;
                    case JsonTokenType.EndSet:
                        writer.WriteEndSet();
                        depth--;
                        continue;
                    case JsonTokenType.StartMap:
                        writer.WriteStartMap();
                        if (isMap) { if (wroteArrow) arrowMask &= ~depthBit; else arrowMask |= depthBit; }
                        depth++;
                        if (depth < 64) mapMask |= (1L << depth);
                        continue;
                    case JsonTokenType.EndMap:
                        writer.WriteEndMap();
                        if (depth < 64) { mapMask &= ~(1L << depth); arrowMask &= ~(1L << depth); }
                        depth--;
                        continue;
                    case JsonTokenType.PropertyName:
                        WritePropertyName(row, writer);
                        continue;
                }

                Debug.Fail($"Unexpected encounter with JsonTokenType {row.TokenType}");
            }
        }

        private ReadOnlySpan<byte> UnescapeString(in DbRow row, out ArraySegment<byte> rented)
        {
            Debug.Assert(row.TokenType == JsonTokenType.String || row.TokenType == JsonTokenType.PropertyName);
            int loc = row.Location;
            int length = row.SizeOrLength;
            ReadOnlySpan<byte> text = _utf8Json.Slice(loc, length).Span;

            if (!row.HasComplexChildren)
            {
                rented = default;
                return text;
            }

            byte[] rent = ArrayPool<byte>.Shared.Rent(length);
            JsonReaderHelper.Unescape(text, rent, out int written);
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

        internal void WritePropertyName(int index, Utf8JsonWriter writer)
        {
            CheckNotDisposed();

            DbRow row = _parsedData.Get(index - DbRow.Size);
            Debug.Assert(row.TokenType == JsonTokenType.PropertyName);
            WritePropertyName(row, writer);
        }

        private void WritePropertyName(in DbRow row, Utf8JsonWriter writer)
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

        private void WriteString(in DbRow row, Utf8JsonWriter writer)
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
            ReadOnlySpan<byte> utf8JsonSpan,
            JsonReaderOptions readerOptions,
            ref MetadataDb database,
            ref StackRowStack stack)
        {
            bool inArray = false;
            int arrayItemsOrPropertyCount = 0;
            int numberOfRowsForMembers = 0;
            int numberOfRowsForValues = 0;

            Utf8JsonReader reader = new Utf8JsonReader(
                utf8JsonSpan,
                isFinalBlock: true,
                new JsonReaderState(options: readerOptions));

            while (reader.Read())
            {
                JsonTokenType tokenType = reader.TokenType;

                // Since the input payload is contained within a Span,
                // token start index can never be larger than int.MaxValue (i.e. utf8JsonSpan.Length).
                Debug.Assert(reader.TokenStartIndex <= int.MaxValue);
                int tokenStart = (int)reader.TokenStartIndex;

                if (tokenType == JsonTokenType.StartObject)
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
                else if (tokenType == JsonTokenType.EndObject)
                {
                    int rowIndex = database.FindIndexOfFirstUnsetSizeOrLength(JsonTokenType.StartObject);

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
                else if (tokenType == JsonTokenType.StartArray)
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
                else if (tokenType == JsonTokenType.EndArray)
                {
                    int rowIndex = database.FindIndexOfFirstUnsetSizeOrLength(JsonTokenType.StartArray);

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
                else if (tokenType == JsonTokenType.StartSet)
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
                else if (tokenType == JsonTokenType.EndSet)
                {
                    int rowIndex = database.FindIndexOfFirstUnsetSizeOrLength(JsonTokenType.StartSet);

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
                else if (tokenType == JsonTokenType.StartMap)
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
                else if (tokenType == JsonTokenType.EndMap)
                {
                    int rowIndex = database.FindIndexOfFirstUnsetSizeOrLength(JsonTokenType.StartMap);

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
                else if (tokenType == JsonTokenType.PropertyName)
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
                    Debug.Assert((tokenType >= JsonTokenType.String && tokenType <= JsonTokenType.Null) || tokenType == JsonTokenType.RdnDateTime || tokenType == JsonTokenType.RdnTimeOnly || tokenType == JsonTokenType.RdnDuration);
                    numberOfRowsForValues++;
                    numberOfRowsForMembers++;

                    if (inArray)
                    {
                        arrayItemsOrPropertyCount++;
                    }

                    if (tokenType == JsonTokenType.String)
                    {
                        // Adding 1 to skip the start quote will never overflow
                        Debug.Assert(tokenStart < int.MaxValue);

                        database.Append(tokenType, tokenStart + 1, reader.ValueSpan.Length);

                        if (reader.ValueIsEscaped)
                        {
                            database.SetHasComplexChildren(database.Length - DbRow.Size);
                        }
                    }
                    else if (tokenType == JsonTokenType.RdnDateTime || tokenType == JsonTokenType.RdnTimeOnly || tokenType == JsonTokenType.RdnDuration)
                    {
                        // Adding 1 to skip the @ prefix will never overflow
                        Debug.Assert(tokenStart < int.MaxValue);

                        database.Append(tokenType, tokenStart + 1, reader.ValueSpan.Length);
                    }
                    else
                    {
                        database.Append(tokenType, tokenStart, reader.ValueSpan.Length);
                    }
                }

                inArray = reader.IsInArray;
            }

            Debug.Assert(reader.BytesConsumed == utf8JsonSpan.Length);
            database.CompleteAllocations();
        }

        private static void ValidateNoDuplicateProperties(JsonDocument document)
        {
            if (document.RootElement.ValueKind is JsonValueKind.Array or JsonValueKind.Object or JsonValueKind.Set or JsonValueKind.Map)
            {
                ValidateDuplicatePropertiesCore(document);
            }
        }

        private static void ValidateDuplicatePropertiesCore(JsonDocument document)
        {
            Debug.Assert(document.RootElement.ValueKind is JsonValueKind.Array or JsonValueKind.Object or JsonValueKind.Set or JsonValueKind.Map);

            using PropertyNameSet propertyNameSet = new PropertyNameSet();

            Stack<int> traversalPath = new Stack<int>();
            int? databaseIndexOflastProcessedChild = null;

            traversalPath.Push(document.RootElement.MetadataDbIndex);

            do
            {
                JsonElement curr = new JsonElement(document, traversalPath.Peek());

                switch (curr.ValueKind)
                {
                    case JsonValueKind.Object:
                    {
                        JsonElement.ObjectEnumerator enumerator = new(curr, databaseIndexOflastProcessedChild ?? -1);

                        while (enumerator.MoveNext())
                        {
                            if (enumerator.Current.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Set)
                            {
                                traversalPath.Push(enumerator.Current.Value.MetadataDbIndex);
                                databaseIndexOflastProcessedChild = null;
                                goto continueOuter;
                            }
                        }

                        // No more children, so process the current element.
                        enumerator.Reset();
                        propertyNameSet.SetCapacity(curr.GetPropertyCount());

                        foreach (JsonProperty property in enumerator)
                        {
                            propertyNameSet.AddPropertyName(property, document);
                        }

                        propertyNameSet.Reset();
                        databaseIndexOflastProcessedChild = traversalPath.Pop();
                        break;
                    }
                    case JsonValueKind.Array:
                    case JsonValueKind.Set:
                    case JsonValueKind.Map:
                    {
                        JsonElement.ArrayEnumerator enumerator = new(curr, databaseIndexOflastProcessedChild ?? -1);

                        while (enumerator.MoveNext())
                        {
                            if (enumerator.Current.ValueKind is JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Set or JsonValueKind.Map)
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
                        ThrowHelper.ThrowJsonException();
                        break;
                }

            continueOuter:
                ;
            } while (traversalPath.Count is not 0);
        }

        private void CheckNotDisposed()
        {
            if (_utf8Json.IsEmpty)
            {
                ThrowHelper.ThrowObjectDisposedException_JsonDocument();
            }
        }

        private static void CheckExpectedType(JsonTokenType expected, JsonTokenType actual)
        {
            if (expected != actual)
            {
                ThrowHelper.ThrowJsonElementWrongTypeException(expected, actual);
            }
        }

        private static void CheckSupportedOptions(
            JsonReaderOptions readerOptions,
            string paramName)
        {
            // Since these are coming from a valid instance of Utf8JsonReader, the JsonReaderOptions must already be valid
            Debug.Assert(readerOptions.CommentHandling >= 0 && readerOptions.CommentHandling <= JsonCommentHandling.Allow);

            if (readerOptions.CommentHandling == JsonCommentHandling.Allow)
            {
                throw new ArgumentException(SR.JsonDocumentDoesNotSupportComments, paramName);
            }
        }
    }
}
