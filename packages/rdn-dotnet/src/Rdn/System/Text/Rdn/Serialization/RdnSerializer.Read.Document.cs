// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Rdn.Serialization;
using Rdn.Serialization.Metadata;

namespace Rdn
{
    public static partial class RdnSerializer
    {
        /// <summary>
        /// Converts the <see cref="RdnDocument"/> representing a single RDN value into a <typeparamref name="TValue"/>.
        /// </summary>
        /// <typeparam name="TValue">The type to deserialize the RDN value into.</typeparam>
        /// <returns>A <typeparamref name="TValue"/> representation of the RDN value.</returns>
        /// <param name="document">The <see cref="RdnDocument"/> to convert.</param>
        /// <param name="options">Options to control the behavior during parsing.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="document"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="RdnException">
        /// <typeparamref name="TValue" /> is not compatible with the RDN.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="Rdn.Serialization.RdnConverter"/>
        /// for <typeparamref name="TValue"/> or its serializable members.
        /// </exception>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static TValue? Deserialize<TValue>(this RdnDocument document, RdnSerializerOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(document);

            RdnTypeInfo<TValue> rdnTypeInfo = GetTypeInfo<TValue>(options);
            ReadOnlySpan<byte> utf8Rdn = document.GetRootRawValue().Span;
            return ReadFromSpan(utf8Rdn, rdnTypeInfo);
        }

        /// <summary>
        /// Converts the <see cref="RdnDocument"/> representing a single RDN value into a <paramref name="returnType"/>.
        /// </summary>
        /// <returns>A <paramref name="returnType"/> representation of the RDN value.</returns>
        /// <param name="document">The <see cref="RdnDocument"/> to convert.</param>
        /// <param name="returnType">The type of the object to convert to and return.</param>
        /// <param name="options">Options to control the behavior during parsing.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="document"/> or <paramref name="returnType"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="RdnException">
        /// <paramref name="returnType"/> is not compatible with the RDN.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="Rdn.Serialization.RdnConverter"/>
        /// for <paramref name="returnType"/> or its serializable members.
        /// </exception>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static object? Deserialize(this RdnDocument document, Type returnType, RdnSerializerOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentNullException.ThrowIfNull(returnType);

            RdnTypeInfo rdnTypeInfo = GetTypeInfo(options, returnType);
            ReadOnlySpan<byte> utf8Rdn = document.GetRootRawValue().Span;
            return ReadFromSpanAsObject(utf8Rdn, rdnTypeInfo);
        }

        /// <summary>
        /// Converts the <see cref="RdnDocument"/> representing a single RDN value into a <typeparamref name="TValue"/>.
        /// </summary>
        /// <typeparam name="TValue">The type to deserialize the RDN value into.</typeparam>
        /// <returns>A <typeparamref name="TValue"/> representation of the RDN value.</returns>
        /// <param name="document">The <see cref="RdnDocument"/> to convert.</param>
        /// <param name="rdnTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="document"/> is <see langword="null"/>.
        ///
        /// -or-
        ///
        /// <paramref name="rdnTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="RdnException">
        /// <typeparamref name="TValue" /> is not compatible with the RDN.
        /// </exception>
        public static TValue? Deserialize<TValue>(this RdnDocument document, RdnTypeInfo<TValue> rdnTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentNullException.ThrowIfNull(rdnTypeInfo);

            rdnTypeInfo.EnsureConfigured();
            ReadOnlySpan<byte> utf8Rdn = document.GetRootRawValue().Span;
            return ReadFromSpan(utf8Rdn, rdnTypeInfo);
        }

        /// <summary>
        /// Converts the <see cref="RdnDocument"/> representing a single RDN value into an instance specified by the <paramref name="rdnTypeInfo"/>.
        /// </summary>
        /// <param name="document">The <see cref="RdnDocument"/> to convert.</param>
        /// <param name="rdnTypeInfo">Metadata about the type to convert.</param>
        /// <returns>A <paramref name="rdnTypeInfo"/> representation of the RDN value.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="document"/> is <see langword="null"/>.
        ///
        /// -or-
        ///
        /// <paramref name="rdnTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        public static object? Deserialize(this RdnDocument document, RdnTypeInfo rdnTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentNullException.ThrowIfNull(rdnTypeInfo);

            rdnTypeInfo.EnsureConfigured();
            ReadOnlySpan<byte> utf8Rdn = document.GetRootRawValue().Span;
            return ReadFromSpanAsObject(utf8Rdn, rdnTypeInfo);
        }

        /// <summary>
        /// Converts the <see cref="RdnDocument"/> representing a single RDN value into a <paramref name="returnType"/>.
        /// </summary>
        /// <returns>A <paramref name="returnType"/> representation of the RDN value.</returns>
        /// <param name="document">The <see cref="RdnDocument"/> to convert.</param>
        /// <param name="returnType">The type of the object to convert to and return.</param>
        /// <param name="context">A metadata provider for serializable types.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="document"/> is <see langword="null"/>.
        ///
        /// -or-
        ///
        /// <paramref name="returnType"/> is <see langword="null"/>.
        ///
        /// -or-
        ///
        /// <paramref name="context"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="RdnException">
        /// The RDN is invalid.
        ///
        /// -or-
        ///
        /// <paramref name="returnType" /> is not compatible with the RDN.
        ///
        /// -or-
        ///
        /// There is remaining data in the string beyond a single RDN value.</exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="Rdn.Serialization.RdnConverter"/>
        /// for <paramref name="returnType"/> or its serializable members.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="RdnSerializerContext.GetTypeInfo(Type)"/> method of the provided
        /// <paramref name="context"/> returns <see langword="null"/> for the type to convert.
        /// </exception>
        public static object? Deserialize(this RdnDocument document, Type returnType, RdnSerializerContext context)
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentNullException.ThrowIfNull(returnType);
            ArgumentNullException.ThrowIfNull(context);

            RdnTypeInfo rdnTypeInfo = GetTypeInfo(context, returnType);
            ReadOnlySpan<byte> utf8Rdn = document.GetRootRawValue().Span;
            return ReadFromSpanAsObject(utf8Rdn, rdnTypeInfo);
        }
    }
}
