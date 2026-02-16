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
        /// Converts the provided value into a <see cref="RdnDocument"/>.
        /// </summary>
        /// <typeparam name="TValue">The type of the value to serialize.</typeparam>
        /// <returns>A <see cref="RdnDocument"/> representation of the RDN value.</returns>
        /// <param name="value">The value to convert.</param>
        /// <param name="options">Options to control the conversion behavior.</param>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="Rdn.Serialization.RdnConverter"/>
        /// for <typeparamref name="TValue"/> or its serializable members.
        /// </exception>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static RdnDocument SerializeToDocument<TValue>(TValue value, RdnSerializerOptions? options = null)
        {
            RdnTypeInfo<TValue> rdnTypeInfo = GetTypeInfo<TValue>(options);
            return WriteDocument(value, rdnTypeInfo);
        }

        /// <summary>
        /// Converts the provided value into a <see cref="RdnDocument"/>.
        /// </summary>
        /// <returns>A <see cref="RdnDocument"/> representation of the value.</returns>
        /// <param name="value">The value to convert.</param>
        /// <param name="inputType">The type of the <paramref name="value"/> to convert.</param>
        /// <param name="options">Options to control the conversion behavior.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="inputType"/> is not compatible with <paramref name="value"/>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <exception cref="ArgumentNullException">
        /// <paramref name="inputType"/> is <see langword="null"/>.
        /// </exception>
        /// There is no compatible <see cref="Rdn.Serialization.RdnConverter"/>
        /// for <paramref name="inputType"/>  or its serializable members.
        /// </exception>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static RdnDocument SerializeToDocument(object? value, Type inputType, RdnSerializerOptions? options = null)
        {
            ValidateInputType(value, inputType);
            RdnTypeInfo rdnTypeInfo = GetTypeInfo(options, inputType);
            return WriteDocumentAsObject(value, rdnTypeInfo);
        }

        /// <summary>
        /// Converts the provided value into a <see cref="RdnDocument"/>.
        /// </summary>
        /// <typeparam name="TValue">The type of the value to serialize.</typeparam>
        /// <returns>A <see cref="RdnDocument"/> representation of the value.</returns>
        /// <param name="value">The value to convert.</param>
        /// <param name="rdnTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="rdnTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        public static RdnDocument SerializeToDocument<TValue>(TValue value, RdnTypeInfo<TValue> rdnTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(rdnTypeInfo);

            rdnTypeInfo.EnsureConfigured();
            return WriteDocument(value, rdnTypeInfo);
        }

        /// <summary>
        /// Converts the provided value into a <see cref="RdnDocument"/>.
        /// </summary>
        /// <returns>A <see cref="RdnDocument"/> representation of the value.</returns>
        /// <param name="value">The value to convert.</param>
        /// <param name="rdnTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="rdnTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidCastException">
        /// <paramref name="value"/> does not match the type of <paramref name="rdnTypeInfo"/>.
        /// </exception>
        public static RdnDocument SerializeToDocument(object? value, RdnTypeInfo rdnTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(rdnTypeInfo);

            rdnTypeInfo.EnsureConfigured();
            return WriteDocumentAsObject(value, rdnTypeInfo);
        }

        /// <summary>
        /// Converts the provided value into a <see cref="RdnDocument"/>.
        /// </summary>
        /// <returns>A <see cref="RdnDocument"/> representation of the value.</returns>
        /// <param name="value">The value to convert.</param>
        /// <param name="inputType">The type of the <paramref name="value"/> to convert.</param>
        /// <param name="context">A metadata provider for serializable types.</param>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="Rdn.Serialization.RdnConverter"/>
        /// for <paramref name="inputType"/> or its serializable members.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="RdnSerializerContext.GetTypeInfo(Type)"/> method of the provided
        /// <paramref name="context"/> returns <see langword="null"/> for the type to convert.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="inputType"/> or <paramref name="context"/> is <see langword="null"/>.
        /// </exception>
        public static RdnDocument SerializeToDocument(object? value, Type inputType, RdnSerializerContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            ValidateInputType(value, inputType);
            return WriteDocumentAsObject(value, GetTypeInfo(context, inputType));
        }

        private static RdnDocument WriteDocument<TValue>(in TValue value, RdnTypeInfo<TValue> rdnTypeInfo)
        {
            Debug.Assert(rdnTypeInfo.IsConfigured);
            RdnSerializerOptions options = rdnTypeInfo.Options;

            // For performance, share the same buffer across serialization and deserialization.
            // The PooledByteBufferWriter is cleared and returned when RdnDocument.Dispose() is called.
            PooledByteBufferWriter output = new(options.DefaultBufferSize);
            Utf8RdnWriter writer = Utf8RdnWriterCache.RentWriter(options, output);

            try
            {
                rdnTypeInfo.Serialize(writer, value);
                return RdnDocument.ParseRented(output, options.GetDocumentOptions());
            }
            finally
            {
                Utf8RdnWriterCache.ReturnWriter(writer);
            }
        }

        private static RdnDocument WriteDocumentAsObject(object? value, RdnTypeInfo rdnTypeInfo)
        {
            Debug.Assert(rdnTypeInfo.IsConfigured);
            RdnSerializerOptions options = rdnTypeInfo.Options;

            // For performance, share the same buffer across serialization and deserialization.
            // The PooledByteBufferWriter is cleared and returned when RdnDocument.Dispose() is called.
            PooledByteBufferWriter output = new(options.DefaultBufferSize);
            Utf8RdnWriter writer = Utf8RdnWriterCache.RentWriter(options, output);

            try
            {
                rdnTypeInfo.SerializeAsObject(writer, value);
                return RdnDocument.ParseRented(output, options.GetDocumentOptions());
            }
            finally
            {
                Utf8RdnWriterCache.ReturnWriter(writer);
            }
        }
    }
}
