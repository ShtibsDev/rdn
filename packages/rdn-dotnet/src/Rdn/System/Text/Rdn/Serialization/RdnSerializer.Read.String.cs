// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Rdn.Serialization;
using Rdn.Serialization.Metadata;

namespace Rdn
{
    /// <summary>
    /// Provides functionality to serialize objects or value types to RDN and
    /// deserialize RDN into objects or value types.
    /// </summary>
    public static partial class RdnSerializer
    {
        /// <summary>
        /// Parses the text representing a single RDN value into a <typeparamref name="TValue"/>.
        /// </summary>
        /// <typeparam name="TValue">The type to deserialize the RDN value into.</typeparam>
        /// <returns>A <typeparamref name="TValue"/> representation of the RDN value.</returns>
        /// <param name="rdn">RDN text to parse.</param>
        /// <param name="options">Options to control the behavior during parsing.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="rdn"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="RdnException">
        /// The RDN is invalid.
        ///
        /// -or-
        ///
        /// <typeparamref name="TValue" /> is not compatible with the RDN.
        ///
        /// -or-
        ///
        /// There is remaining data in the string beyond a single RDN value.</exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="Rdn.Serialization.RdnConverter"/>
        /// for <typeparamref name="TValue"/> or its serializable members.
        /// </exception>
        /// <remarks>Using a <see cref="string"/> is not as efficient as using the
        /// UTF-8 methods since the implementation natively uses UTF-8.
        /// </remarks>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static TValue? Deserialize<TValue>([StringSyntax(StringSyntaxAttribute.Json)] string rdn, RdnSerializerOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(rdn);

            RdnTypeInfo<TValue> rdnTypeInfo = GetTypeInfo<TValue>(options);
            return ReadFromSpan(rdn.AsSpan(), rdnTypeInfo);
        }

        /// <summary>
        /// Parses the text representing a single RDN value into an instance of the type specified by a generic type parameter.
        /// </summary>
        /// <typeparam name="TValue">The type to deserialize the RDN value into.</typeparam>
        /// <returns>A <typeparamref name="TValue"/> representation of the RDN value.</returns>
        /// <param name="rdn">The RDN text to parse.</param>
        /// <param name="options">Options to control the behavior during parsing.</param>
        /// <exception cref="RdnException">
        /// The RDN is invalid.
        ///
        /// -or-
        ///
        /// <typeparamref name="TValue" /> is not compatible with the RDN.
        ///
        /// -or-
        ///
        /// There is remaining data in the span beyond a single RDN value.</exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="Rdn.Serialization.RdnConverter"/>
        /// for <typeparamref name="TValue"/> or its serializable members.
        /// </exception>
        /// <remarks>Using a UTF-16 span is not as efficient as using the
        /// UTF-8 methods since the implementation natively uses UTF-8.
        /// </remarks>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static TValue? Deserialize<TValue>([StringSyntax(StringSyntaxAttribute.Json)] ReadOnlySpan<char> rdn, RdnSerializerOptions? options = null)
        {
            RdnTypeInfo<TValue> rdnTypeInfo = GetTypeInfo<TValue>(options);
            return ReadFromSpan(rdn, rdnTypeInfo);
        }

        /// <summary>
        /// Parses the text representing a single RDN value into a <paramref name="returnType"/>.
        /// </summary>
        /// <returns>A <paramref name="returnType"/> representation of the RDN value.</returns>
        /// <param name="rdn">RDN text to parse.</param>
        /// <param name="returnType">The type of the object to convert to and return.</param>
        /// <param name="options">Options to control the behavior during parsing.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="rdn"/> or <paramref name="returnType"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="RdnException">
        /// The RDN is invalid.
        ///
        /// -or-
        ///
        /// <paramref name="returnType"/> is not compatible with the RDN.
        ///
        /// -or-
        ///
        /// There is remaining data in the string beyond a single RDN value.</exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="Rdn.Serialization.RdnConverter"/>
        /// for <paramref name="returnType"/> or its serializable members.
        /// </exception>
        /// <remarks>Using a <see cref="string"/> is not as efficient as using the
        /// UTF-8 methods since the implementation natively uses UTF-8.
        /// </remarks>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static object? Deserialize([StringSyntax(StringSyntaxAttribute.Json)] string rdn, Type returnType, RdnSerializerOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(rdn);
            ArgumentNullException.ThrowIfNull(returnType);

            RdnTypeInfo rdnTypeInfo = GetTypeInfo(options, returnType);
            return ReadFromSpanAsObject(rdn.AsSpan(), rdnTypeInfo);
        }

        /// <summary>
        /// Parses the text representing a single RDN value into an instance of a specified type.
        /// </summary>
        /// <returns>A <paramref name="returnType"/> representation of the RDN value.</returns>
        /// <param name="rdn">The RDN text to parse.</param>
        /// <param name="returnType">The type of the object to convert to and return.</param>
        /// <param name="options">Options to control the behavior during parsing.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="returnType"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="RdnException">
        /// The RDN is invalid.
        ///
        /// -or-
        ///
        /// <paramref name="returnType"/> is not compatible with the RDN.
        ///
        /// -or-
        ///
        /// There is remaining data in the span beyond a single RDN value.</exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="Rdn.Serialization.RdnConverter"/>
        /// for <paramref name="returnType"/> or its serializable members.
        /// </exception>
        /// <remarks>Using a UTF-16 span is not as efficient as using the
        /// UTF-8 methods since the implementation natively uses UTF-8.
        /// </remarks>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static object? Deserialize([StringSyntax(StringSyntaxAttribute.Json)] ReadOnlySpan<char> rdn, Type returnType, RdnSerializerOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(returnType);

            // default/null span is treated as empty

            RdnTypeInfo rdnTypeInfo = GetTypeInfo(options, returnType);
            return ReadFromSpanAsObject(rdn, rdnTypeInfo);
        }

        /// <summary>
        /// Parses the text representing a single RDN value into a <typeparamref name="TValue"/>.
        /// </summary>
        /// <typeparam name="TValue">The type to deserialize the RDN value into.</typeparam>
        /// <returns>A <typeparamref name="TValue"/> representation of the RDN value.</returns>
        /// <param name="rdn">RDN text to parse.</param>
        /// <param name="rdnTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="rdn"/> is <see langword="null"/>.
        ///
        /// -or-
        ///
        /// <paramref name="rdnTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="RdnException">
        /// The RDN is invalid.
        ///
        /// -or-
        ///
        /// <typeparamref name="TValue" /> is not compatible with the RDN.
        ///
        /// -or-
        ///
        /// There is remaining data in the string beyond a single RDN value.</exception>
        /// <remarks>Using a <see cref="string"/> is not as efficient as using the
        /// UTF-8 methods since the implementation natively uses UTF-8.
        /// </remarks>
        public static TValue? Deserialize<TValue>([StringSyntax(StringSyntaxAttribute.Json)] string rdn, RdnTypeInfo<TValue> rdnTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(rdn);
            ArgumentNullException.ThrowIfNull(rdnTypeInfo);

            rdnTypeInfo.EnsureConfigured();
            return ReadFromSpan(rdn.AsSpan(), rdnTypeInfo);
        }

        /// <summary>
        /// Parses the text representing a single RDN value into a <typeparamref name="TValue"/>.
        /// </summary>
        /// <typeparam name="TValue">The type to deserialize the RDN value into.</typeparam>
        /// <returns>A <typeparamref name="TValue"/> representation of the RDN value.</returns>
        /// <param name="rdn">RDN text to parse.</param>
        /// <param name="rdnTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="rdn"/> is <see langword="null"/>.
        ///
        /// -or-
        ///
        /// <paramref name="rdnTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="RdnException">
        /// The RDN is invalid.
        ///
        /// -or-
        ///
        /// <typeparamref name="TValue" /> is not compatible with the RDN.
        ///
        /// -or-
        ///
        /// There is remaining data in the string beyond a single RDN value.</exception>
        /// <remarks>Using a <see cref="string"/> is not as efficient as using the
        /// UTF-8 methods since the implementation natively uses UTF-8.
        /// </remarks>
        public static TValue? Deserialize<TValue>([StringSyntax(StringSyntaxAttribute.Json)] ReadOnlySpan<char> rdn, RdnTypeInfo<TValue> rdnTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(rdnTypeInfo);

            rdnTypeInfo.EnsureConfigured();
            return ReadFromSpan(rdn, rdnTypeInfo);
        }

        /// <summary>
        /// Parses the text representing a single RDN value into an instance specified by the <paramref name="rdnTypeInfo"/>.
        /// </summary>
        /// <returns>A <paramref name="rdnTypeInfo"/> representation of the RDN value.</returns>
        /// <param name="rdn">RDN text to parse.</param>
        /// <param name="rdnTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="rdn"/> is <see langword="null"/>.
        ///
        /// -or-
        ///
        /// <paramref name="rdnTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="RdnException">
        /// The RDN is invalid.
        ///
        /// -or-
        ///
        /// There is remaining data in the string beyond a single RDN value.</exception>
        /// <remarks>Using a <see cref="string"/> is not as efficient as using the
        /// UTF-8 methods since the implementation natively uses UTF-8.
        /// </remarks>
        public static object? Deserialize([StringSyntax(StringSyntaxAttribute.Json)] string rdn, RdnTypeInfo rdnTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(rdn);
            ArgumentNullException.ThrowIfNull(rdnTypeInfo);

            rdnTypeInfo.EnsureConfigured();
            return ReadFromSpanAsObject(rdn.AsSpan(), rdnTypeInfo);
        }

        /// <summary>
        /// Parses the text representing a single RDN value into an instance specified by the <paramref name="rdnTypeInfo"/>.
        /// </summary>
        /// <returns>A <paramref name="rdnTypeInfo"/> representation of the RDN value.</returns>
        /// <param name="rdn">RDN text to parse.</param>
        /// <param name="rdnTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="rdnTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="RdnException">
        /// The RDN is invalid.
        ///
        /// -or-
        ///
        /// There is remaining data in the string beyond a single RDN value.</exception>
        /// <remarks>Using a <see cref="string"/> is not as efficient as using the
        /// UTF-8 methods since the implementation natively uses UTF-8.
        /// </remarks>
        public static object? Deserialize([StringSyntax(StringSyntaxAttribute.Json)] ReadOnlySpan<char> rdn, RdnTypeInfo rdnTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(rdnTypeInfo);

            rdnTypeInfo.EnsureConfigured();
            return ReadFromSpanAsObject(rdn, rdnTypeInfo);
        }

        /// <summary>
        /// Parses the text representing a single RDN value into a <paramref name="returnType"/>.
        /// </summary>
        /// <returns>A <paramref name="returnType"/> representation of the RDN value.</returns>
        /// <param name="rdn">RDN text to parse.</param>
        /// <param name="returnType">The type of the object to convert to and return.</param>
        /// <param name="context">A metadata provider for serializable types.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="rdn"/> or <paramref name="returnType"/> is <see langword="null"/>.
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
        /// <remarks>Using a <see cref="string"/> is not as efficient as using the
        /// UTF-8 methods since the implementation natively uses UTF-8.
        /// </remarks>
        public static object? Deserialize([StringSyntax(StringSyntaxAttribute.Json)] string rdn, Type returnType, RdnSerializerContext context)
        {
            ArgumentNullException.ThrowIfNull(rdn);
            ArgumentNullException.ThrowIfNull(returnType);
            ArgumentNullException.ThrowIfNull(context);

            RdnTypeInfo rdnTypeInfo = GetTypeInfo(context, returnType);
            return ReadFromSpanAsObject(rdn.AsSpan(), rdnTypeInfo);
        }

        /// <summary>
        /// Parses the text representing a single RDN value into a <paramref name="returnType"/>.
        /// </summary>
        /// <returns>A <paramref name="returnType"/> representation of the RDN value.</returns>
        /// <param name="rdn">RDN text to parse.</param>
        /// <param name="returnType">The type of the object to convert to and return.</param>
        /// <param name="context">A metadata provider for serializable types.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="rdn"/> or <paramref name="returnType"/> is <see langword="null"/>.
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
        /// <remarks>Using a <see cref="string"/> is not as efficient as using the
        /// UTF-8 methods since the implementation natively uses UTF-8.
        /// </remarks>
        public static object? Deserialize([StringSyntax(StringSyntaxAttribute.Json)] ReadOnlySpan<char> rdn, Type returnType, RdnSerializerContext context)
        {
            ArgumentNullException.ThrowIfNull(returnType);
            ArgumentNullException.ThrowIfNull(context);

            RdnTypeInfo rdnTypeInfo = GetTypeInfo(context, returnType);
            return ReadFromSpanAsObject(rdn, rdnTypeInfo);
        }

        private static TValue? ReadFromSpan<TValue>(ReadOnlySpan<char> rdn, RdnTypeInfo<TValue> rdnTypeInfo)
        {
            Debug.Assert(rdnTypeInfo.IsConfigured);
            byte[]? tempArray = null;

            // For performance, avoid obtaining actual byte count unless memory usage is higher than the threshold.
            Span<byte> utf8 =
                // Use stack memory
                rdn.Length <= (RdnConstants.StackallocByteThreshold / RdnConstants.MaxExpansionFactorWhileTranscoding) ? stackalloc byte[RdnConstants.StackallocByteThreshold] :
                // Use a pooled array
                rdn.Length <= (RdnConstants.ArrayPoolMaxSizeBeforeUsingNormalAlloc / RdnConstants.MaxExpansionFactorWhileTranscoding) ? tempArray = ArrayPool<byte>.Shared.Rent(rdn.Length * RdnConstants.MaxExpansionFactorWhileTranscoding) :
                // Use a normal alloc since the pool would create a normal alloc anyway based on the threshold (per current implementation)
                // and by using a normal alloc we can avoid the Clear().
                new byte[RdnReaderHelper.GetUtf8ByteCount(rdn)];

            try
            {
                int actualByteCount = RdnReaderHelper.GetUtf8FromText(rdn, utf8);
                utf8 = utf8.Slice(0, actualByteCount);
                return ReadFromSpan(utf8, rdnTypeInfo, actualByteCount);
            }
            finally
            {
                if (tempArray != null)
                {
                    utf8.Clear();
                    ArrayPool<byte>.Shared.Return(tempArray);
                }
            }
        }

        private static object? ReadFromSpanAsObject(ReadOnlySpan<char> rdn, RdnTypeInfo rdnTypeInfo)
        {
            Debug.Assert(rdnTypeInfo.IsConfigured);
            byte[]? tempArray = null;

            // For performance, avoid obtaining actual byte count unless memory usage is higher than the threshold.
            Span<byte> utf8 =
                // Use stack memory
                rdn.Length <= (RdnConstants.StackallocByteThreshold / RdnConstants.MaxExpansionFactorWhileTranscoding) ? stackalloc byte[RdnConstants.StackallocByteThreshold] :
                // Use a pooled array
                rdn.Length <= (RdnConstants.ArrayPoolMaxSizeBeforeUsingNormalAlloc / RdnConstants.MaxExpansionFactorWhileTranscoding) ? tempArray = ArrayPool<byte>.Shared.Rent(rdn.Length * RdnConstants.MaxExpansionFactorWhileTranscoding) :
                // Use a normal alloc since the pool would create a normal alloc anyway based on the threshold (per current implementation)
                // and by using a normal alloc we can avoid the Clear().
                new byte[RdnReaderHelper.GetUtf8ByteCount(rdn)];

            try
            {
                int actualByteCount = RdnReaderHelper.GetUtf8FromText(rdn, utf8);
                utf8 = utf8.Slice(0, actualByteCount);
                return ReadFromSpanAsObject(utf8, rdnTypeInfo, actualByteCount);
            }
            finally
            {
                if (tempArray != null)
                {
                    utf8.Clear();
                    ArrayPool<byte>.Shared.Return(tempArray);
                }
            }
        }
    }
}
