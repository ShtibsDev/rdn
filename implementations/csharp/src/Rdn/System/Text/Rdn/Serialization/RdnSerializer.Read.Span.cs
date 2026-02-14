// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Rdn.Serialization;
using Rdn.Serialization.Metadata;

namespace Rdn
{
    public static partial class RdnSerializer
    {
        /// <summary>
        /// Parses the UTF-8 encoded text representing a single RDN value into a <typeparamref name="TValue"/>.
        /// </summary>
        /// <typeparam name="TValue">The type to deserialize the RDN value into.</typeparam>
        /// <returns>A <typeparamref name="TValue"/> representation of the RDN value.</returns>
        /// <param name="utf8Rdn">RDN text to parse.</param>
        /// <param name="options">Options to control the behavior during parsing.</param>
        /// <exception cref="RdnException">
        /// The RDN is invalid,
        /// <typeparamref name="TValue"/> is not compatible with the RDN,
        /// or when there is remaining data in the Stream.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="Rdn.Serialization.RdnConverter"/>
        /// for <typeparamref name="TValue"/> or its serializable members.
        /// </exception>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static TValue? Deserialize<TValue>(ReadOnlySpan<byte> utf8Rdn, RdnSerializerOptions? options = null)
        {
            RdnTypeInfo<TValue> rdnTypeInfo = GetTypeInfo<TValue>(options);
            return ReadFromSpan(utf8Rdn, rdnTypeInfo);
        }

        /// <summary>
        /// Parses the UTF-8 encoded text representing a single RDN value into a <paramref name="returnType"/>.
        /// </summary>
        /// <returns>A <paramref name="returnType"/> representation of the RDN value.</returns>
        /// <param name="utf8Rdn">RDN text to parse.</param>
        /// <param name="returnType">The type of the object to convert to and return.</param>
        /// <param name="options">Options to control the behavior during parsing.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="returnType"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="RdnException">
        /// The RDN is invalid,
        /// <paramref name="returnType"/> is not compatible with the RDN,
        /// or when there is remaining data in the Stream.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="Rdn.Serialization.RdnConverter"/>
        /// for <paramref name="returnType"/> or its serializable members.
        /// </exception>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static object? Deserialize(ReadOnlySpan<byte> utf8Rdn, Type returnType, RdnSerializerOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(returnType);

            RdnTypeInfo rdnTypeInfo = GetTypeInfo(options, returnType);
            return ReadFromSpanAsObject(utf8Rdn, rdnTypeInfo);
        }

        /// <summary>
        /// Parses the UTF-8 encoded text representing a single RDN value into a <typeparamref name="TValue"/>.
        /// </summary>
        /// <typeparam name="TValue">The type to deserialize the RDN value into.</typeparam>
        /// <returns>A <typeparamref name="TValue"/> representation of the RDN value.</returns>
        /// <param name="utf8Rdn">RDN text to parse.</param>
        /// <param name="rdnTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="RdnException">
        /// The RDN is invalid,
        /// <typeparamref name="TValue"/> is not compatible with the RDN,
        /// or when there is remaining data in the buffer.
        /// </exception>
        public static TValue? Deserialize<TValue>(ReadOnlySpan<byte> utf8Rdn, RdnTypeInfo<TValue> rdnTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(rdnTypeInfo);

            rdnTypeInfo.EnsureConfigured();
            return ReadFromSpan(utf8Rdn, rdnTypeInfo);
        }

        /// <summary>
        /// Parses the UTF-8 encoded text representing a single RDN value into an instance specified by the <paramref name="rdnTypeInfo"/>.
        /// </summary>
        /// <returns>A <paramref name="rdnTypeInfo"/> representation of the RDN value.</returns>
        /// <param name="utf8Rdn">RDN text to parse.</param>
        /// <param name="rdnTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="RdnException">
        /// The RDN is invalid,
        /// or there is remaining data in the buffer.
        /// </exception>
        public static object? Deserialize(ReadOnlySpan<byte> utf8Rdn, RdnTypeInfo rdnTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(rdnTypeInfo);

            rdnTypeInfo.EnsureConfigured();
            return ReadFromSpanAsObject(utf8Rdn, rdnTypeInfo);
        }

        /// <summary>
        /// Parses the UTF-8 encoded text representing a single RDN value into a <paramref name="returnType"/>.
        /// </summary>
        /// <returns>A <paramref name="returnType"/> representation of the RDN value.</returns>
        /// <param name="utf8Rdn">RDN text to parse.</param>
        /// <param name="returnType">The type of the object to convert to and return.</param>
        /// <param name="context">A metadata provider for serializable types.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="returnType"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="RdnException">
        /// The RDN is invalid,
        /// <paramref name="returnType"/> is not compatible with the RDN,
        /// or when there is remaining data in the Stream.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="Rdn.Serialization.RdnConverter"/>
        /// for <paramref name="returnType"/> or its serializable members.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="RdnSerializerContext.GetTypeInfo(Type)"/> method on the provided <paramref name="context"/>
        /// did not return a compatible <see cref="RdnTypeInfo"/> for <paramref name="returnType"/>.
        /// </exception>
        public static object? Deserialize(ReadOnlySpan<byte> utf8Rdn, Type returnType, RdnSerializerContext context)
        {
            ArgumentNullException.ThrowIfNull(returnType);
            ArgumentNullException.ThrowIfNull(context);

            return ReadFromSpanAsObject(utf8Rdn, GetTypeInfo(context, returnType));
        }

        private static TValue? ReadFromSpan<TValue>(ReadOnlySpan<byte> utf8Rdn, RdnTypeInfo<TValue> rdnTypeInfo, int? actualByteCount = null)
        {
            Debug.Assert(rdnTypeInfo.IsConfigured);

            var readerState = new RdnReaderState(rdnTypeInfo.Options.GetReaderOptions());
            var reader = new Utf8RdnReader(utf8Rdn, isFinalBlock: true, readerState);

            ReadStack state = default;
            state.Initialize(rdnTypeInfo);

            TValue? value = rdnTypeInfo.Deserialize(ref reader, ref state);

            // The reader should have thrown if we have remaining bytes, unless AllowMultipleValues is true.
            Debug.Assert(reader.BytesConsumed == (actualByteCount ?? utf8Rdn.Length) || reader.CurrentState.Options.AllowMultipleValues);
            return value;
        }

        private static object? ReadFromSpanAsObject(ReadOnlySpan<byte> utf8Rdn, RdnTypeInfo rdnTypeInfo, int? actualByteCount = null)
        {
            Debug.Assert(rdnTypeInfo.IsConfigured);

            var readerState = new RdnReaderState(rdnTypeInfo.Options.GetReaderOptions());
            var reader = new Utf8RdnReader(utf8Rdn, isFinalBlock: true, readerState);

            ReadStack state = default;
            state.Initialize(rdnTypeInfo);

            object? value = rdnTypeInfo.DeserializeAsObject(ref reader, ref state);

            // The reader should have thrown if we have remaining bytes, unless AllowMultipleValues is true.
            Debug.Assert(reader.BytesConsumed == (actualByteCount ?? utf8Rdn.Length) || reader.CurrentState.Options.AllowMultipleValues);
            return value;
        }
    }
}
