// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace Rdn
{
    public readonly partial struct RdnElement
    {
        /// <summary>
        ///   Parses one RDN value (including objects or arrays) from the provided reader.
        /// </summary>
        /// <param name="reader">The reader to read.</param>
        /// <returns>
        ///   A RdnElement representing the value (and nested values) read from the reader.
        /// </returns>
        /// <remarks>
        ///   <para>
        ///     If the <see cref="Utf8RdnReader.TokenType"/> property of <paramref name="reader"/>
        ///     is <see cref="RdnTokenType.PropertyName"/> or <see cref="RdnTokenType.None"/>, the
        ///     reader will be advanced by one call to <see cref="Utf8RdnReader.Read"/> to determine
        ///     the start of the value.
        ///   </para>
        ///
        ///   <para>
        ///     Upon completion of this method, <paramref name="reader"/> will be positioned at the
        ///     final token in the RDN value. If an exception is thrown, the reader is reset to
        ///     the state it was in when the method was called.
        ///   </para>
        ///
        ///   <para>
        ///     This method makes a copy of the data the reader acted on, so there is no caller
        ///     requirement to maintain data integrity beyond the return of this method.
        ///   </para>
        /// </remarks>
        /// <exception cref="ArgumentException">
        ///   <paramref name="reader"/> is using unsupported options.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   The current <paramref name="reader"/> token does not start or represent a value.
        /// </exception>
        /// <exception cref="RdnException">
        ///   A value could not be read from the reader.
        /// </exception>
        public static RdnElement ParseValue(ref Utf8RdnReader reader)
        {
            bool ret = RdnDocument.TryParseValue(ref reader, out RdnDocument? document, shouldThrow: true, useArrayPools: false);

            Debug.Assert(ret, "TryParseValue returned false with shouldThrow: true.");
            Debug.Assert(document != null, "null document returned with shouldThrow: true.");
            return document.RootElement;
        }

        internal static RdnElement ParseValue(ref Utf8RdnReader reader, bool allowDuplicateProperties)
        {
            bool ret = RdnDocument.TryParseValue(
                ref reader,
                out RdnDocument? document,
                shouldThrow: true,
                useArrayPools: false,
                allowDuplicateProperties: allowDuplicateProperties);

            Debug.Assert(ret, "TryParseValue returned false with shouldThrow: true.");
            Debug.Assert(document != null, "null document returned with shouldThrow: true.");
            return document.RootElement;
        }

        internal static RdnElement ParseValue(Stream utf8Rdn, RdnDocumentOptions options)
        {
            RdnDocument document = RdnDocument.ParseValue(utf8Rdn, options);
            return document.RootElement;
        }

        /// <summary>
        /// Parses UTF8-encoded text representing a single RDN value into a <see cref="RdnElement"/>.
        /// </summary>
        /// <param name="utf8Rdn">The RDN text to parse.</param>
        /// <param name="options">Options to control the reader behavior during parsing.</param>
        /// <returns>A <see cref="RdnElement"/> representation of the RDN value.</returns>
        /// <exception cref="RdnException"><paramref name="utf8Rdn"/> does not represent a valid single RDN value.</exception>
        /// <exception cref="ArgumentException"><paramref name="options"/> contains unsupported options.</exception>
        public static RdnElement Parse([StringSyntax(StringSyntaxAttribute.Json)] ReadOnlySpan<byte> utf8Rdn, RdnDocumentOptions options = default)
        {
            RdnDocument document = RdnDocument.ParseValue(utf8Rdn, options);
            return document.RootElement;
        }

        /// <summary>
        /// Parses text representing a single RDN value into a <see cref="RdnElement"/>.
        /// </summary>
        /// <param name="rdn">The RDN text to parse.</param>
        /// <param name="options">Options to control the reader behavior during parsing.</param>
        /// <returns>A <see cref="RdnElement"/> representation of the RDN value.</returns>
        /// <exception cref="RdnException"><paramref name="rdn"/> does not represent a valid single RDN value.</exception>
        /// <exception cref="ArgumentException"><paramref name="options"/> contains unsupported options.</exception>
        public static RdnElement Parse([StringSyntax(StringSyntaxAttribute.Json)] ReadOnlySpan<char> rdn, RdnDocumentOptions options = default)
        {
            RdnDocument document = RdnDocument.ParseValue(rdn, options);
            return document.RootElement;
        }

        /// <summary>
        /// Parses text representing a single RDN value into a <see cref="RdnElement"/>.
        /// </summary>
        /// <param name="rdn">The RDN text to parse.</param>
        /// <param name="options">Options to control the reader behavior during parsing.</param>
        /// <returns>A <see cref="RdnElement"/> representation of the RDN value.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="rdn"/> is <see langword="null"/>.</exception>
        /// <exception cref="RdnException"><paramref name="rdn"/> does not represent a valid single RDN value.</exception>
        /// <exception cref="ArgumentException"><paramref name="options"/> contains unsupported options.</exception>
        public static RdnElement Parse([StringSyntax(StringSyntaxAttribute.Json)] string rdn, RdnDocumentOptions options = default)
        {
            ArgumentNullException.ThrowIfNull(rdn);

            RdnDocument document = RdnDocument.ParseValue(rdn, options);
            return document.RootElement;
        }

        /// <summary>
        ///   Attempts to parse one RDN value (including objects or arrays) from the provided reader.
        /// </summary>
        /// <param name="reader">The reader to read.</param>
        /// <param name="element">Receives the parsed element.</param>
        /// <returns>
        ///   <see langword="true"/> if a value was read and parsed into a RdnElement;
        ///   <see langword="false"/> if the reader ran out of data while parsing.
        ///   All other situations result in an exception being thrown.
        /// </returns>
        /// <remarks>
        ///   <para>
        ///     If the <see cref="Utf8RdnReader.TokenType"/> property of <paramref name="reader"/>
        ///     is <see cref="RdnTokenType.PropertyName"/> or <see cref="RdnTokenType.None"/>, the
        ///     reader will be advanced by one call to <see cref="Utf8RdnReader.Read"/> to determine
        ///     the start of the value.
        ///   </para>
        ///
        ///   <para>
        ///     Upon completion of this method, <paramref name="reader"/> will be positioned at the
        ///     final token in the RDN value.  If an exception is thrown, or <see langword="false"/>
        ///     is returned, the reader is reset to the state it was in when the method was called.
        ///   </para>
        ///
        ///   <para>
        ///     This method makes a copy of the data the reader acted on, so there is no caller
        ///     requirement to maintain data integrity beyond the return of this method.
        ///   </para>
        /// </remarks>
        /// <exception cref="ArgumentException">
        ///   <paramref name="reader"/> is using unsupported options.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   The current <paramref name="reader"/> token does not start or represent a value.
        /// </exception>
        /// <exception cref="RdnException">
        ///   A value could not be read from the reader.
        /// </exception>
        public static bool TryParseValue(ref Utf8RdnReader reader, [NotNullWhen(true)] out RdnElement? element)
        {
            bool ret = RdnDocument.TryParseValue(ref reader, out RdnDocument? document, shouldThrow: false, useArrayPools: false);
            element = document?.RootElement;
            return ret;
        }
    }
}
