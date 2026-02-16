// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.IO;
using Rdn.Serialization;
using Rdn.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Rdn
{
    public static partial class RdnSerializer
    {
        // We flush the Stream when the buffer is >=90% of capacity.
        // This threshold is a compromise between buffer utilization and minimizing cases where the buffer
        // needs to be expanded\doubled because it is not large enough to write the current property or element.
        // We check for flush after each RDN property and element is written to the buffer.
        // Once the buffer is expanded to contain the largest single element\property, a 90% threshold
        // means the buffer may be expanded a maximum of 4 times: 1-(1/(2^4))==.9375.
        internal const float FlushThreshold = .90f;

        /// <summary>
        /// Converts the provided value to UTF-8 encoded RDN text and write it to the <see cref="System.IO.Stream"/>.
        /// </summary>
        /// <typeparam name="TValue">The type of the value to serialize.</typeparam>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        /// <param name="utf8Rdn">The UTF-8 <see cref="System.IO.Stream"/> to write to.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="options">Options to control the conversion behavior.</param>
        /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> that can be used to cancel the write operation.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="utf8Rdn"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="Rdn.Serialization.RdnConverter"/>
        /// for <typeparamref name="TValue"/> or its serializable members.
        /// </exception>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static Task SerializeAsync<TValue>(
            Stream utf8Rdn,
            TValue value,
            RdnSerializerOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(utf8Rdn);

            RdnTypeInfo<TValue> rdnTypeInfo = GetTypeInfo<TValue>(options);
            return rdnTypeInfo.SerializeAsync(utf8Rdn, value, cancellationToken);
        }

        /// <summary>
        /// Converts the provided value to UTF-8 encoded RDN text and write it to the <see cref="System.IO.Stream"/>.
        /// </summary>
        /// <typeparam name="TValue">The type of the value to serialize.</typeparam>
        /// <param name="utf8Rdn">The UTF-8 <see cref="System.IO.Stream"/> to write to.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="options">Options to control the conversion behavior.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="utf8Rdn"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="Rdn.Serialization.RdnConverter"/>
        /// for <typeparamref name="TValue"/> or its serializable members.
        /// </exception>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static void Serialize<TValue>(
            Stream utf8Rdn,
            TValue value,
            RdnSerializerOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(utf8Rdn);

            RdnTypeInfo<TValue> rdnTypeInfo = GetTypeInfo<TValue>(options);
            rdnTypeInfo.Serialize(utf8Rdn, value);
        }

        /// <summary>
        /// Converts the provided value to UTF-8 encoded RDN text and write it to the <see cref="System.IO.Stream"/>.
        /// </summary>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        /// <param name="utf8Rdn">The UTF-8 <see cref="System.IO.Stream"/> to write to.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="inputType">The type of the <paramref name="value"/> to convert.</param>
        /// <param name="options">Options to control the conversion behavior.</param>
        /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> that can be used to cancel the write operation.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="inputType"/> is not compatible with <paramref name="value"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="utf8Rdn"/> or <paramref name="inputType"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="Rdn.Serialization.RdnConverter"/>
        /// for <paramref name="inputType"/>  or its serializable members.
        /// </exception>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static Task SerializeAsync(
            Stream utf8Rdn,
            object? value,
            Type inputType,
            RdnSerializerOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(utf8Rdn);

            ValidateInputType(value, inputType);
            RdnTypeInfo rdnTypeInfo = GetTypeInfo(options, inputType);
            return rdnTypeInfo.SerializeAsObjectAsync(utf8Rdn, value, cancellationToken);
        }

        /// <summary>
        /// Converts the provided value to UTF-8 encoded RDN text and write it to the <see cref="System.IO.Stream"/>.
        /// </summary>
        /// <param name="utf8Rdn">The UTF-8 <see cref="System.IO.Stream"/> to write to.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="inputType">The type of the <paramref name="value"/> to convert.</param>
        /// <param name="options">Options to control the conversion behavior.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="inputType"/> is not compatible with <paramref name="value"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="utf8Rdn"/> or <paramref name="inputType"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="Rdn.Serialization.RdnConverter"/>
        /// for <paramref name="inputType"/>  or its serializable members.
        /// </exception>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static void Serialize(
            Stream utf8Rdn,
            object? value,
            Type inputType,
            RdnSerializerOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(utf8Rdn);

            ValidateInputType(value, inputType);
            RdnTypeInfo rdnTypeInfo = GetTypeInfo(options, inputType);
            rdnTypeInfo.SerializeAsObject(utf8Rdn, value);
        }

        /// <summary>
        /// Converts the provided value to UTF-8 encoded RDN text and write it to the <see cref="System.IO.Stream"/>.
        /// </summary>
        /// <typeparam name="TValue">The type of the value to serialize.</typeparam>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        /// <param name="utf8Rdn">The UTF-8 <see cref="System.IO.Stream"/> to write to.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="rdnTypeInfo">Metadata about the type to convert.</param>
        /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> that can be used to cancel the write operation.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="utf8Rdn"/> is <see langword="null"/>.
        /// </exception>
        public static Task SerializeAsync<TValue>(
            Stream utf8Rdn,
            TValue value,
            RdnTypeInfo<TValue> rdnTypeInfo,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(utf8Rdn);
            ArgumentNullException.ThrowIfNull(rdnTypeInfo);

            rdnTypeInfo.EnsureConfigured();
            return rdnTypeInfo.SerializeAsync(utf8Rdn, value, cancellationToken);
        }

        /// <summary>
        /// Converts the provided value to UTF-8 encoded RDN text and write it to the <see cref="System.IO.Stream"/>.
        /// </summary>
        /// <typeparam name="TValue">The type of the value to serialize.</typeparam>
        /// <param name="utf8Rdn">The UTF-8 <see cref="System.IO.Stream"/> to write to.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="rdnTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="utf8Rdn"/> is <see langword="null"/>.
        /// </exception>
        public static void Serialize<TValue>(
            Stream utf8Rdn,
            TValue value,
            RdnTypeInfo<TValue> rdnTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(utf8Rdn);
            ArgumentNullException.ThrowIfNull(rdnTypeInfo);

            rdnTypeInfo.EnsureConfigured();
            rdnTypeInfo.Serialize(utf8Rdn, value);
        }

        /// <summary>
        /// Converts the provided value to UTF-8 encoded RDN text and write it to the <see cref="System.IO.Stream"/>.
        /// </summary>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        /// <param name="utf8Rdn">The UTF-8 <see cref="System.IO.Stream"/> to write to.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="rdnTypeInfo">Metadata about the type to convert.</param>
        /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> that can be used to cancel the write operation.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="utf8Rdn"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidCastException">
        /// <paramref name="value"/> does not match the type of <paramref name="rdnTypeInfo"/>.
        /// </exception>
        public static Task SerializeAsync(
            Stream utf8Rdn,
            object? value,
            RdnTypeInfo rdnTypeInfo,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(utf8Rdn);
            ArgumentNullException.ThrowIfNull(rdnTypeInfo);

            rdnTypeInfo.EnsureConfigured();
            return rdnTypeInfo.SerializeAsObjectAsync(utf8Rdn, value, cancellationToken);
        }

        /// <summary>
        /// Converts the provided value to UTF-8 encoded RDN text and write it to the <see cref="System.IO.Stream"/>.
        /// </summary>
        /// <param name="utf8Rdn">The UTF-8 <see cref="System.IO.Stream"/> to write to.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="rdnTypeInfo">Metadata about the type to convert.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="utf8Rdn"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidCastException">
        /// <paramref name="value"/> does not match the type of <paramref name="rdnTypeInfo"/>.
        /// </exception>
        public static void Serialize(
            Stream utf8Rdn,
            object? value,
            RdnTypeInfo rdnTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(utf8Rdn);
            ArgumentNullException.ThrowIfNull(rdnTypeInfo);

            rdnTypeInfo.EnsureConfigured();
            rdnTypeInfo.SerializeAsObject(utf8Rdn, value);
        }

        /// <summary>
        /// Converts the provided value to UTF-8 encoded RDN text and write it to the <see cref="System.IO.Stream"/>.
        /// </summary>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        /// <param name="utf8Rdn">The UTF-8 <see cref="System.IO.Stream"/> to write to.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="inputType">The type of the <paramref name="value"/> to convert.</param>
        /// <param name="context">A metadata provider for serializable types.</param>
        /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> that can be used to cancel the write operation.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="inputType"/> is not compatible with <paramref name="value"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="utf8Rdn"/>, <paramref name="inputType"/>, or <paramref name="context"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="Rdn.Serialization.RdnConverter"/>
        /// for <paramref name="inputType"/>  or its serializable members.
        /// </exception>
        public static Task SerializeAsync(
            Stream utf8Rdn,
            object? value,
            Type inputType,
            RdnSerializerContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(utf8Rdn);
            ArgumentNullException.ThrowIfNull(context);

            ValidateInputType(value, inputType);
            RdnTypeInfo rdnTypeInfo = GetTypeInfo(context, inputType);
            return rdnTypeInfo.SerializeAsObjectAsync(utf8Rdn, value, cancellationToken);
        }

        /// <summary>
        /// Converts the provided value to UTF-8 encoded RDN text and write it to the <see cref="System.IO.Stream"/>.
        /// </summary>
        /// <param name="utf8Rdn">The UTF-8 <see cref="System.IO.Stream"/> to write to.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="inputType">The type of the <paramref name="value"/> to convert.</param>
        /// <param name="context">A metadata provider for serializable types.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="inputType"/> is not compatible with <paramref name="value"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="utf8Rdn"/>, <paramref name="inputType"/>, or <paramref name="context"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="Rdn.Serialization.RdnConverter"/>
        /// for <paramref name="inputType"/>  or its serializable members.
        /// </exception>
        public static void Serialize(
            Stream utf8Rdn,
            object? value,
            Type inputType,
            RdnSerializerContext context)
        {
            ArgumentNullException.ThrowIfNull(utf8Rdn);
            ArgumentNullException.ThrowIfNull(context);

            ValidateInputType(value, inputType);
            RdnTypeInfo rdnTypeInfo = GetTypeInfo(context, inputType);
            rdnTypeInfo.SerializeAsObject(utf8Rdn, value);
        }
    }
}
