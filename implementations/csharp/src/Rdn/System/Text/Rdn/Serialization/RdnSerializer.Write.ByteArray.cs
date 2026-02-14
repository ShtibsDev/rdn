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
        /// Converts the provided value into a <see cref="byte"/> array.
        /// </summary>
        /// <returns>A UTF-8 representation of the value.</returns>
        /// <param name="value">The value to convert.</param>
        /// <param name="options">Options to control the conversion behavior.</param>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="Rdn.Serialization.RdnConverter"/>
        /// for <typeparamref name="TValue"/> or its serializable members.
        /// </exception>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static byte[] SerializeToUtf8Bytes<TValue>(
            TValue value,
            RdnSerializerOptions? options = null)
        {
            RdnTypeInfo<TValue> rdnTypeInfo = GetTypeInfo<TValue>(options);
            return WriteBytes(value, rdnTypeInfo);
        }

        /// <summary>
        /// Converts the provided value into a <see cref="byte"/> array.
        /// </summary>
        /// <returns>A UTF-8 representation of the value.</returns>
        /// <param name="value">The value to convert.</param>
        /// <param name="inputType">The type of the <paramref name="value"/> to convert.</param>
        /// <param name="options">Options to control the conversion behavior.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="inputType"/> is not compatible with <paramref name="value"/>.
        /// </exception>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="inputType"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="Rdn.Serialization.RdnConverter"/>
        /// for <paramref name="inputType"/>  or its serializable members.
        /// </exception>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static byte[] SerializeToUtf8Bytes(
            object? value,
            Type inputType,
            RdnSerializerOptions? options = null)
        {
            ValidateInputType(value, inputType);
            RdnTypeInfo rdnTypeInfo = GetTypeInfo(options, inputType);
            return WriteBytesAsObject(value, rdnTypeInfo);
        }

        /// <summary>
        /// Converts the provided value into a <see cref="byte"/> array.
        /// </summary>
        /// <returns>A UTF-8 representation of the value.</returns>
        /// <param name="value">The value to convert.</param>
        /// <param name="rdnTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="rdnTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        public static byte[] SerializeToUtf8Bytes<TValue>(TValue value, RdnTypeInfo<TValue> rdnTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(rdnTypeInfo);

            rdnTypeInfo.EnsureConfigured();
            return WriteBytes(value, rdnTypeInfo);
        }

        /// <summary>
        /// Converts the provided value into a <see cref="byte"/> array.
        /// </summary>
        /// <returns>A UTF-8 representation of the value.</returns>
        /// <param name="value">The value to convert.</param>
        /// <param name="rdnTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="rdnTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidCastException">
        /// <paramref name="value"/> does not match the type of <paramref name="rdnTypeInfo"/>.
        /// </exception>
        public static byte[] SerializeToUtf8Bytes(object? value, RdnTypeInfo rdnTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(rdnTypeInfo);

            rdnTypeInfo.EnsureConfigured();
            return WriteBytesAsObject(value, rdnTypeInfo);
        }

        /// <summary>
        /// Converts the provided value into a <see cref="byte"/> array.
        /// </summary>
        /// <returns>A UTF-8 representation of the value.</returns>
        /// <param name="value">The value to convert.</param>
        /// <param name="inputType">The type of the <paramref name="value"/> to convert.</param>
        /// <param name="context">A metadata provider for serializable types.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="inputType"/> is not compatible with <paramref name="value"/>.
        /// </exception>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="inputType"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="Rdn.Serialization.RdnConverter"/>
        /// for <paramref name="inputType"/>  or its serializable members.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="RdnSerializerContext.GetTypeInfo(Type)"/> method of the provided
        /// <paramref name="context"/> returns <see langword="null"/> for the type to convert.
        /// </exception>
        public static byte[] SerializeToUtf8Bytes(object? value, Type inputType, RdnSerializerContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            ValidateInputType(value, inputType);
            RdnTypeInfo rdnTypeInfo = GetTypeInfo(context, inputType);
            return WriteBytesAsObject(value, rdnTypeInfo);
        }

        private static byte[] WriteBytes<TValue>(in TValue value, RdnTypeInfo<TValue> rdnTypeInfo)
        {
            Debug.Assert(rdnTypeInfo.IsConfigured);

            Utf8RdnWriter writer = Utf8RdnWriterCache.RentWriterAndBuffer(rdnTypeInfo.Options, out PooledByteBufferWriter output);

            try
            {
                rdnTypeInfo.Serialize(writer, value);
                return output.WrittenSpan.ToArray();
            }
            finally
            {
                Utf8RdnWriterCache.ReturnWriterAndBuffer(writer, output);
            }
        }

        private static byte[] WriteBytesAsObject(object? value, RdnTypeInfo rdnTypeInfo)
        {
            Debug.Assert(rdnTypeInfo.IsConfigured);

            Utf8RdnWriter writer = Utf8RdnWriterCache.RentWriterAndBuffer(rdnTypeInfo.Options, out PooledByteBufferWriter output);

            try
            {
                rdnTypeInfo.SerializeAsObject(writer, value);
                return output.WrittenSpan.ToArray();
            }
            finally
            {
                Utf8RdnWriterCache.ReturnWriterAndBuffer(writer, output);
            }
        }
    }
}
