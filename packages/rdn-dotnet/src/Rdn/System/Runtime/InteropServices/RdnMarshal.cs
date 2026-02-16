// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Rdn;

namespace System.Runtime.InteropServices
{
    /// <summary>
    /// An unsafe class that provides a set of methods to access the underlying data representations of RDN types.
    /// </summary>
    public static class RdnMarshal
    {
        /// <summary>
        /// Gets a <see cref="ReadOnlySpan{T}"/> view over the raw RDN data of the given <see cref="RdnElement"/>.
        /// </summary>
        /// <param name="element">The RDN element from which to extract the span.</param>
        /// <returns>The span containing the raw RDN data of<paramref name="element"/>.</returns>
        /// <exception cref="ObjectDisposedException">The underlying <see cref="RdnDocument"/> has been disposed.</exception>
        /// <remarks>
        /// While the method itself does check for disposal of the underlying <see cref="RdnDocument"/>,
        /// it is possible that it could be disposed after the method returns, which would result in
        /// the span pointing to a buffer that has been returned to the shared pool. Callers should take
        /// extra care to make sure that such a scenario isn't possible to avoid potential data corruption.
        /// </remarks>
        public static ReadOnlySpan<byte> GetRawUtf8Value(RdnElement element)
        {
            return element.GetRawValue().Span;
        }

        /// <summary>
        /// Gets a <see cref="ReadOnlySpan{T}"/> view over the raw RDN data of the given <see cref="RdnProperty"/> name.
        /// </summary>
        /// <param name="property">The RDN property from which to extract the span.</param>
        /// <returns>The span containing the raw RDN data of the <paramref name="property"/> name. This will not include the enclosing quotes.</returns>
        /// <exception cref="ObjectDisposedException">The underlying <see cref="RdnDocument"/> has been disposed.</exception>
        /// <remarks>
        /// <para>
        /// While the method itself does check for disposal of the underlying <see cref="RdnDocument"/>,
        /// it is possible that it could be disposed after the method returns, which would result in
        /// the span pointing to a buffer that has been returned to the shared pool. Callers should take
        /// extra care to make sure that such a scenario isn't possible to avoid potential data corruption.
        /// </para>
        /// </remarks>
        public static ReadOnlySpan<byte> GetRawUtf8PropertyName(RdnProperty property)
        {
            return property.NameSpan;
        }
    }
}
