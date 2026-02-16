// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Rdn.Nodes;
using Rdn.Serialization;
using Rdn.Serialization.Metadata;

namespace Rdn
{
    public static partial class RdnSerializer
    {
        /// <summary>
        /// Converts the provided value into a <see cref="RdnNode"/>.
        /// </summary>
        /// <typeparam name="TValue">The type of the value to serialize.</typeparam>
        /// <returns>A <see cref="RdnNode"/> representation of the RDN value.</returns>
        /// <param name="value">The value to convert.</param>
        /// <param name="options">Options to control the conversion behavior.</param>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="Rdn.Serialization.RdnConverter"/>
        /// for <typeparamref name="TValue"/> or its serializable members.
        /// </exception>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static RdnNode? SerializeToNode<TValue>(TValue value, RdnSerializerOptions? options = null)
        {
            RdnTypeInfo<TValue> rdnTypeInfo = GetTypeInfo<TValue>(options);
            return WriteNode(value, rdnTypeInfo);
        }

        /// <summary>
        /// Converts the provided value into a <see cref="RdnNode"/>.
        /// </summary>
        /// <returns>A <see cref="RdnNode"/> representation of the value.</returns>
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
        public static RdnNode? SerializeToNode(object? value, Type inputType, RdnSerializerOptions? options = null)
        {
            ValidateInputType(value, inputType);
            RdnTypeInfo typeInfo = GetTypeInfo(options, inputType);
            return WriteNodeAsObject(value, typeInfo);
        }

        /// <summary>
        /// Converts the provided value into a <see cref="RdnNode"/>.
        /// </summary>
        /// <typeparam name="TValue">The type of the value to serialize.</typeparam>
        /// <returns>A <see cref="RdnNode"/> representation of the value.</returns>
        /// <param name="value">The value to convert.</param>
        /// <param name="rdnTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="rdnTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        public static RdnNode? SerializeToNode<TValue>(TValue value, RdnTypeInfo<TValue> rdnTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(rdnTypeInfo);

            rdnTypeInfo.EnsureConfigured();
            return WriteNode(value, rdnTypeInfo);
        }

        /// <summary>
        /// Converts the provided value into a <see cref="RdnNode"/>.
        /// </summary>
        /// <returns>A <see cref="RdnNode"/> representation of the value.</returns>
        /// <param name="value">The value to convert.</param>
        /// <param name="rdnTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="rdnTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidCastException">
        /// <paramref name="value"/> does not match the type of <paramref name="rdnTypeInfo"/>.
        /// </exception>
        public static RdnNode? SerializeToNode(object? value, RdnTypeInfo rdnTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(rdnTypeInfo);

            rdnTypeInfo.EnsureConfigured();
            return WriteNodeAsObject(value, rdnTypeInfo);
        }

        /// <summary>
        /// Converts the provided value into a <see cref="RdnNode"/>.
        /// </summary>
        /// <returns>A <see cref="RdnNode"/> representation of the value.</returns>
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
        public static RdnNode? SerializeToNode(object? value, Type inputType, RdnSerializerContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            ValidateInputType(value, inputType);
            RdnTypeInfo rdnTypeInfo = GetTypeInfo(context, inputType);
            return WriteNodeAsObject(value, rdnTypeInfo);
        }

        private static RdnNode? WriteNode<TValue>(in TValue value, RdnTypeInfo<TValue> rdnTypeInfo)
        {
            Debug.Assert(rdnTypeInfo.IsConfigured);
            RdnSerializerOptions options = rdnTypeInfo.Options;

            Utf8RdnWriter writer = Utf8RdnWriterCache.RentWriterAndBuffer(rdnTypeInfo.Options, out PooledByteBufferWriter output);

            try
            {
                rdnTypeInfo.Serialize(writer, value);
                return RdnNode.Parse(output.WrittenSpan, options.GetNodeOptions(), options.GetDocumentOptions());
            }
            finally
            {
                Utf8RdnWriterCache.ReturnWriterAndBuffer(writer, output);
            }
        }

        private static RdnNode? WriteNodeAsObject(object? value, RdnTypeInfo rdnTypeInfo)
        {
            Debug.Assert(rdnTypeInfo.IsConfigured);
            RdnSerializerOptions options = rdnTypeInfo.Options;

            Utf8RdnWriter writer = Utf8RdnWriterCache.RentWriterAndBuffer(rdnTypeInfo.Options, out PooledByteBufferWriter output);

            try
            {
                rdnTypeInfo.SerializeAsObject(writer, value);
                return RdnNode.Parse(output.WrittenSpan, options.GetNodeOptions(), options.GetDocumentOptions());
            }
            finally
            {
                Utf8RdnWriterCache.ReturnWriterAndBuffer(writer, output);
            }
        }
    }
}
