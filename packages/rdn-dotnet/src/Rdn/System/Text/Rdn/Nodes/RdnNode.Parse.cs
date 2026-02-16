// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.IO;
using Rdn.Serialization.Converters;
using System.Threading;
using System.Threading.Tasks;

namespace Rdn.Nodes
{
    public abstract partial class RdnNode
    {
        /// <summary>
        ///   Parses one RDN value (including objects or arrays) from the provided reader.
        /// </summary>
        /// <param name="reader">The reader to read.</param>
        /// <param name="nodeOptions">Options to control the behavior.</param>
        /// <returns>
        ///   The <see cref="RdnNode"/> from the reader.
        /// </returns>
        /// <remarks>
        ///   <para>
        ///     If the <see cref="Utf8RdnReader.TokenType"/> property of <paramref name="reader"/>
        ///     is <see cref="RdnTokenType.PropertyName"/> or <see cref="RdnTokenType.None"/>, the
        ///     reader will be advanced by one call to <see cref="Utf8RdnReader.Read"/> to determine
        ///     the start of the value.
        ///   </para>
        ///   <para>
        ///     Upon completion of this method, <paramref name="reader"/> will be positioned at the
        ///     final token in the RDN value.  If an exception is thrown, the reader is reset to the state it was in when the method was called.
        ///   </para>
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
        public static RdnNode? Parse(
            ref Utf8RdnReader reader,
            RdnNodeOptions? nodeOptions = null)
        {
            RdnElement element = RdnElement.ParseValue(ref reader);
            return RdnNodeConverter.Create(element, nodeOptions);
        }

        /// <summary>
        ///   Parses text representing a single RDN value.
        /// </summary>
        /// <param name="rdn">RDN text to parse.</param>
        /// <param name="nodeOptions">Options to control the node behavior after parsing.</param>
        /// <param name="documentOptions">Options to control the document behavior during parsing.</param>
        /// <returns>
        ///   A <see cref="RdnNode"/> representation of the RDN value.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="rdn"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="RdnException">
        ///   <paramref name="rdn"/> does not represent a valid single RDN value.
        /// </exception>
        public static RdnNode? Parse(
            [StringSyntax(StringSyntaxAttribute.Json)] string rdn,
            RdnNodeOptions? nodeOptions = null,
            RdnDocumentOptions documentOptions = default(RdnDocumentOptions))
        {
            ArgumentNullException.ThrowIfNull(rdn);

            RdnElement element = RdnElement.Parse(rdn, documentOptions);
            return RdnNodeConverter.Create(element, nodeOptions);
        }

        /// <summary>
        ///   Parses text representing a single RDN value.
        /// </summary>
        /// <param name="utf8Rdn">RDN text to parse.</param>
        /// <param name="nodeOptions">Options to control the node behavior after parsing.</param>
        /// <param name="documentOptions">Options to control the document behavior during parsing.</param>
        /// <returns>
        ///   A <see cref="RdnNode"/> representation of the RDN value.
        /// </returns>
        /// <exception cref="RdnException">
        ///   <paramref name="utf8Rdn"/> does not represent a valid single RDN value.
        /// </exception>
        public static RdnNode? Parse(
            ReadOnlySpan<byte> utf8Rdn,
            RdnNodeOptions? nodeOptions = null,
            RdnDocumentOptions documentOptions = default(RdnDocumentOptions))
        {
            RdnElement element = RdnElement.Parse(utf8Rdn, documentOptions);
            return RdnNodeConverter.Create(element, nodeOptions);
        }

        /// <summary>
        ///   Parse a <see cref="Stream"/> as UTF-8 encoded data representing a single RDN value into a
        ///   <see cref="RdnNode"/>.  The Stream will be read to completion.
        /// </summary>
        /// <param name="utf8Rdn">RDN text to parse.</param>
        /// <param name="nodeOptions">Options to control the node behavior after parsing.</param>
        /// <param name="documentOptions">Options to control the document behavior during parsing.</param>
        /// <returns>
        ///   A <see cref="RdnNode"/> representation of the RDN value.
        /// </returns>
        /// <exception cref="RdnException">
        ///   <paramref name="utf8Rdn"/> does not represent a valid single RDN value.
        /// </exception>
        public static RdnNode? Parse(
            Stream utf8Rdn,
            RdnNodeOptions? nodeOptions = null,
            RdnDocumentOptions documentOptions = default)
        {
            ArgumentNullException.ThrowIfNull(utf8Rdn);

            RdnElement element = RdnElement.ParseValue(utf8Rdn, documentOptions);
            return RdnNodeConverter.Create(element, nodeOptions);
        }

        /// <summary>
        ///   Parse a <see cref="Stream"/> as UTF-8 encoded data representing a single RDN value into a
        ///   <see cref="RdnNode"/>.  The Stream will be read to completion.
        /// </summary>
        /// <param name="utf8Rdn">RDN text to parse.</param>
        /// <param name="nodeOptions">Options to control the node behavior after parsing.</param>
        /// <param name="documentOptions">Options to control the document behavior during parsing.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>
        ///   A <see cref="Task"/> to produce a <see cref="RdnNode"/> representation of the RDN value.
        /// </returns>
        /// <exception cref="RdnException">
        ///   <paramref name="utf8Rdn"/> does not represent a valid single RDN value.
        /// </exception>
        public static async Task<RdnNode?> ParseAsync(
            Stream utf8Rdn,
            RdnNodeOptions? nodeOptions = null,
            RdnDocumentOptions documentOptions = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(utf8Rdn);

            RdnDocument document = await RdnDocument.ParseAsyncCoreUnrented(utf8Rdn, documentOptions, cancellationToken).ConfigureAwait(false);
            return RdnNodeConverter.Create(document.RootElement, nodeOptions);
        }
    }
}
