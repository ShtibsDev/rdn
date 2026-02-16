// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Rdn.Encodings.Web;

namespace Rdn
{
    /// <summary>
    /// Provides a way to transform UTF-8 or UTF-16 encoded text into a form that is suitable for RDN.
    /// </summary>
    /// <remarks>
    /// This can be used to cache and store known strings used for writing RDN ahead of time by pre-encoding them up front.
    /// </remarks>
    public readonly struct RdnEncodedText : IEquatable<RdnEncodedText>
    {
        internal readonly byte[] _utf8Value;
        internal readonly string _value;

        /// <summary>
        /// Returns the UTF-8 encoded representation of the pre-encoded RDN text.
        /// </summary>
        public ReadOnlySpan<byte> EncodedUtf8Bytes => _utf8Value;

        /// <summary>
        /// Returns the UTF-16 encoded representation of the pre-encoded RDN text as a <see cref="string"/>.
        /// </summary>
        public string Value => _value ?? string.Empty;

        private RdnEncodedText(byte[] utf8Value)
        {
            Debug.Assert(utf8Value != null);

            _value = RdnReaderHelper.GetTextFromUtf8(utf8Value);
            _utf8Value = utf8Value;
        }

        /// <summary>
        /// Encodes the string text value as a RDN string.
        /// </summary>
        /// <param name="value">The value to be transformed as RDN encoded text.</param>
        /// <param name="encoder">The encoder to use when escaping the string, or <see langword="null" /> to use the default encoder.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if value is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified value is too large or if it contains invalid UTF-16 characters.
        /// </exception>
        public static RdnEncodedText Encode(string value, JavaScriptEncoder? encoder = null)
        {
            ArgumentNullException.ThrowIfNull(value);

            return Encode(value.AsSpan(), encoder);
        }

        /// <summary>
        /// Encodes the text value as a RDN string.
        /// </summary>
        /// <param name="value">The value to be transformed as RDN encoded text.</param>
        /// <param name="encoder">The encoder to use when escaping the string, or <see langword="null" /> to use the default encoder.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified value is too large or if it contains invalid UTF-16 characters.
        /// </exception>
        public static RdnEncodedText Encode(ReadOnlySpan<char> value, JavaScriptEncoder? encoder = null)
        {
            if (value.Length == 0)
            {
                return new RdnEncodedText(Array.Empty<byte>());
            }

            return TranscodeAndEncode(value, encoder);
        }

        private static RdnEncodedText TranscodeAndEncode(ReadOnlySpan<char> value, JavaScriptEncoder? encoder)
        {
            RdnWriterHelper.ValidateValue(value);

            int expectedByteCount = RdnReaderHelper.GetUtf8ByteCount(value);

            byte[]? array = null;
            Span<byte> utf8Bytes = expectedByteCount <= RdnConstants.StackallocByteThreshold ?
                stackalloc byte[RdnConstants.StackallocByteThreshold] :
                (array = ArrayPool<byte>.Shared.Rent(expectedByteCount));

            // Since GetUtf8ByteCount above already throws on invalid input, the transcoding
            // to UTF-8 is guaranteed to succeed here. Therefore, there's no need for a try-catch-finally block.
            int actualByteCount = RdnReaderHelper.GetUtf8FromText(value, utf8Bytes);
            utf8Bytes = utf8Bytes.Slice(0, actualByteCount);
            Debug.Assert(expectedByteCount == utf8Bytes.Length);

            RdnEncodedText encodedText = EncodeHelper(utf8Bytes, encoder);

            if (array is not null)
            {
                // On the basis that this is user data, go ahead and clear it.
                utf8Bytes.Clear();
                ArrayPool<byte>.Shared.Return(array);
            }

            return encodedText;
        }

        /// <summary>
        /// Encodes the UTF-8 text value as a RDN string.
        /// </summary>
        /// <param name="utf8Value">The UTF-8 encoded value to be transformed as RDN encoded text.</param>
        /// <param name="encoder">The encoder to use when escaping the string, or <see langword="null" /> to use the default encoder.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified value is too large or if it contains invalid UTF-8 bytes.
        /// </exception>
        public static RdnEncodedText Encode(ReadOnlySpan<byte> utf8Value, JavaScriptEncoder? encoder = null)
        {
            if (utf8Value.Length == 0)
            {
                return new RdnEncodedText(Array.Empty<byte>());
            }

            RdnWriterHelper.ValidateValue(utf8Value);
            return EncodeHelper(utf8Value, encoder);
        }

        private static RdnEncodedText EncodeHelper(ReadOnlySpan<byte> utf8Value, JavaScriptEncoder? encoder)
        {
            int idx = RdnWriterHelper.NeedsEscaping(utf8Value, encoder);

            if (idx != -1)
            {
                return new RdnEncodedText(RdnHelpers.EscapeValue(utf8Value, idx, encoder));
            }
            else
            {
                return new RdnEncodedText(utf8Value.ToArray());
            }
        }

        /// <summary>
        /// Determines whether this instance and another specified <see cref="RdnEncodedText"/> instance have the same value.
        /// </summary>
        /// <remarks>
        /// Default instances of <see cref="RdnEncodedText"/> are treated as equal.
        /// </remarks>
        public bool Equals(RdnEncodedText other)
        {
            if (_value == null)
            {
                return other._value == null;
            }
            else
            {
                return _value.Equals(other._value);
            }
        }

        /// <summary>
        /// Determines whether this instance and a specified object, which must also be a <see cref="RdnEncodedText"/> instance, have the same value.
        /// </summary>
        /// <remarks>
        /// If <paramref name="obj"/> is null, the method returns false.
        /// </remarks>
        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            if (obj is RdnEncodedText encodedText)
            {
                return Equals(encodedText);
            }
            return false;
        }

        /// <summary>
        /// Converts the value of this instance to a <see cref="string"/>.
        /// </summary>
        /// <remarks>
        /// Returns an empty string on a default instance of <see cref="RdnEncodedText"/>.
        /// </remarks>
        /// <returns>
        /// Returns the underlying UTF-16 encoded string.
        /// </returns>
        public override string ToString()
            => _value ?? string.Empty;

        /// <summary>
        /// Returns the hash code for this <see cref="RdnEncodedText"/>.
        /// </summary>
        /// <remarks>
        /// Returns 0 on a default instance of <see cref="RdnEncodedText"/>.
        /// </remarks>
        public override int GetHashCode()
            => _value == null ? 0 : _value.GetHashCode();
    }
}
