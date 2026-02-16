// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using Rdn.Serialization;
using Rdn.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Rdn
{
    public static partial class RdnSerializer
    {
        /// <summary>
        /// Reads the UTF-8 encoded text representing a single RDN value into a <typeparamref name="TValue"/>.
        /// The PipeReader will be read to completion.
        /// </summary>
        /// <typeparam name="TValue">The type to deserialize the RDN value into.</typeparam>
        /// <returns>A <typeparamref name="TValue"/> representation of the RDN value.</returns>
        /// <param name="utf8Rdn">RDN data to parse.</param>
        /// <param name="options">Options to control the behavior during reading.</param>
        /// <param name="cancellationToken">
        /// The <see cref="System.Threading.CancellationToken"/> that can be used to cancel the read operation.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="utf8Rdn"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="RdnException">
        /// The RDN is invalid,
        /// <typeparamref name="TValue"/> is not compatible with the RDN,
        /// or when there is remaining data in the PipeReader.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="Rdn.Serialization.RdnConverter"/>
        /// for <typeparamref name="TValue"/> or its serializable members.
        /// </exception>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static ValueTask<TValue?> DeserializeAsync<TValue>(
            PipeReader utf8Rdn,
            RdnSerializerOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(utf8Rdn, nameof(utf8Rdn));

            RdnTypeInfo<TValue> rdnTypeInfo = GetTypeInfo<TValue>(options);
            return rdnTypeInfo.DeserializeAsync(utf8Rdn, cancellationToken);
        }

        /// <summary>
        /// Reads the UTF-8 encoded text representing a single RDN value into a <typeparamref name="TValue"/>.
        /// The PipeReader will be read to completion.
        /// </summary>
        /// <typeparam name="TValue">The type to deserialize the RDN value into.</typeparam>
        /// <returns>A <typeparamref name="TValue"/> representation of the RDN value.</returns>
        /// <param name="utf8Rdn">RDN data to parse.</param>
        /// <param name="rdnTypeInfo">Metadata about the type to convert.</param>
        /// <param name="cancellationToken">
        /// The <see cref="System.Threading.CancellationToken"/> that can be used to cancel the read operation.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="utf8Rdn"/> or <paramref name="rdnTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="RdnException">
        /// The RDN is invalid,
        /// <typeparamref name="TValue"/> is not compatible with the RDN,
        /// or when there is remaining data in the PipeReader.
        /// </exception>
        public static ValueTask<TValue?> DeserializeAsync<TValue>(
                PipeReader utf8Rdn,
                RdnTypeInfo<TValue> rdnTypeInfo,
                CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(utf8Rdn, nameof(utf8Rdn));
            ArgumentNullException.ThrowIfNull(rdnTypeInfo, nameof(rdnTypeInfo));

            rdnTypeInfo.EnsureConfigured();
            return rdnTypeInfo.DeserializeAsync(utf8Rdn, cancellationToken);
        }

        /// <summary>
        /// Reads the UTF-8 encoded text representing a single RDN value into an instance specified by the <paramref name="rdnTypeInfo"/>.
        /// The PipeReader will be read to completion.
        /// </summary>
        /// <returns>A <paramref name="rdnTypeInfo"/> representation of the RDN value.</returns>
        /// <param name="utf8Rdn">RDN data to parse.</param>
        /// <param name="rdnTypeInfo">Metadata about the type to convert.</param>
        /// <param name="cancellationToken">
        /// The <see cref="System.Threading.CancellationToken"/> that can be used to cancel the read operation.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="utf8Rdn"/> or <paramref name="rdnTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="RdnException">
        /// The RDN is invalid,
        /// or when there is remaining data in the PipeReader.
        /// </exception>
        public static ValueTask<object?> DeserializeAsync(
                PipeReader utf8Rdn,
                RdnTypeInfo rdnTypeInfo,
                CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(utf8Rdn, nameof(utf8Rdn));
            ArgumentNullException.ThrowIfNull(rdnTypeInfo, nameof(rdnTypeInfo));

            rdnTypeInfo.EnsureConfigured();
            return rdnTypeInfo.DeserializeAsObjectAsync(utf8Rdn, cancellationToken);
        }

        /// <summary>
        /// Reads the UTF-8 encoded text representing a single RDN value into a <paramref name="returnType"/>.
        /// The PipeReader will be read to completion.
        /// </summary>
        /// <returns>A <paramref name="returnType"/> representation of the RDN value.</returns>
        /// <param name="utf8Rdn">RDN data to parse.</param>
        /// <param name="returnType">The type of the object to convert to and return.</param>
        /// <param name="context">A metadata provider for serializable types.</param>
        /// <param name="cancellationToken">
        /// The <see cref="System.Threading.CancellationToken"/> that can be used to cancel the read operation.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="utf8Rdn"/>, <paramref name="returnType"/>, or <paramref name="context"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="RdnException">
        /// The RDN is invalid,
        /// the <paramref name="returnType"/> is not compatible with the RDN,
        /// or when there is remaining data in the PipeReader.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="Rdn.Serialization.RdnConverter"/>
        /// for <paramref name="returnType"/> or its serializable members.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="RdnSerializerContext.GetTypeInfo(Type)"/> method on the provided <paramref name="context"/>
        /// did not return a compatible <see cref="RdnTypeInfo"/> for <paramref name="returnType"/>.
        /// </exception>
        public static ValueTask<object?> DeserializeAsync(
                PipeReader utf8Rdn,
                Type returnType,
                RdnSerializerContext context,
                CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(utf8Rdn, nameof(utf8Rdn));
            ArgumentNullException.ThrowIfNull(returnType, nameof(returnType));
            ArgumentNullException.ThrowIfNull(context, nameof(context));

            RdnTypeInfo rdnTypeInfo = GetTypeInfo(context, returnType);
            return rdnTypeInfo.DeserializeAsObjectAsync(utf8Rdn, cancellationToken);
        }

        /// <summary>
        /// Reads the UTF-8 encoded text representing a single RDN value into a <paramref name="returnType"/>.
        /// The PipeReader will be read to completion.
        /// </summary>
        /// <returns>A <paramref name="returnType"/> representation of the RDN value.</returns>
        /// <param name="utf8Rdn">RDN data to parse.</param>
        /// <param name="returnType">The type of the object to convert to and return.</param>
        /// <param name="options">Options to control the behavior during reading.</param>
        /// <param name="cancellationToken">
        /// The <see cref="System.Threading.CancellationToken"/> that can be used to cancel the read operation.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="utf8Rdn"/> or <paramref name="returnType"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="RdnException">
        /// The RDN is invalid,
        /// the <paramref name="returnType"/> is not compatible with the RDN,
        /// or when there is remaining data in the PipeReader.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="Rdn.Serialization.RdnConverter"/>
        /// for <paramref name="returnType"/> or its serializable members.
        /// </exception>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static ValueTask<object?> DeserializeAsync(
               PipeReader utf8Rdn,
               Type returnType,
               RdnSerializerOptions? options = null,
               CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(utf8Rdn, nameof(utf8Rdn));
            ArgumentNullException.ThrowIfNull(returnType, nameof(returnType));

            RdnTypeInfo rdnTypeInfo = GetTypeInfo(options, returnType);
            return rdnTypeInfo.DeserializeAsObjectAsync(utf8Rdn, cancellationToken);
        }

        /// <summary>
        /// Wraps the UTF-8 encoded text into an <see cref="IAsyncEnumerable{TValue}" />
        /// that can be used to deserialize root-level RDN arrays in a streaming manner.
        /// </summary>
        /// <typeparam name="TValue">The element type to deserialize asynchronously.</typeparam>
        /// <returns>An <see cref="IAsyncEnumerable{TValue}" /> representation of the provided RDN array.</returns>
        /// <param name="utf8Rdn">RDN data to parse.</param>
        /// <param name="options">Options to control the behavior during reading.</param>
        /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> that can be used to cancel the read operation.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="utf8Rdn"/> is <see langword="null"/>.
        /// </exception>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static IAsyncEnumerable<TValue?> DeserializeAsyncEnumerable<TValue>(
                PipeReader utf8Rdn,
                RdnSerializerOptions? options = null,
                CancellationToken cancellationToken = default)
        {
            return DeserializeAsyncEnumerable<TValue>(utf8Rdn, topLevelValues: false, options, cancellationToken);
        }

        /// <summary>
        /// Wraps the UTF-8 encoded text into an <see cref="IAsyncEnumerable{TValue}" />
        /// that can be used to deserialize root-level RDN arrays in a streaming manner.
        /// </summary>
        /// <typeparam name="TValue">The element type to deserialize asynchronously.</typeparam>
        /// <returns>An <see cref="IAsyncEnumerable{TValue}" /> representation of the provided RDN array.</returns>
        /// <param name="utf8Rdn">RDN data to parse.</param>
        /// <param name="rdnTypeInfo">Metadata about the element type to convert.</param>
        /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> that can be used to cancel the read operation.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="utf8Rdn"/> or <paramref name="rdnTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        public static IAsyncEnumerable<TValue?> DeserializeAsyncEnumerable<TValue>(
                PipeReader utf8Rdn,
                RdnTypeInfo<TValue> rdnTypeInfo,
                CancellationToken cancellationToken = default)
        {
            return DeserializeAsyncEnumerable(utf8Rdn, rdnTypeInfo, topLevelValues: false, cancellationToken);
        }

        /// <summary>
        /// Wraps the UTF-8 encoded text into an <see cref="IAsyncEnumerable{TValue}" />
        /// that can be used to deserialize sequences of RDN values in a streaming manner.
        /// </summary>
        /// <typeparam name="TValue">The element type to deserialize asynchronously.</typeparam>
        /// <returns>An <see cref="IAsyncEnumerable{TValue}" /> representation of the provided RDN sequence.</returns>
        /// <param name="utf8Rdn">RDN data to parse.</param>
        /// <param name="rdnTypeInfo">Metadata about the element type to convert.</param>
        /// <param name="topLevelValues">Whether to deserialize from a sequence of top-level RDN values.</param>
        /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> that can be used to cancel the read operation.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="utf8Rdn"/> or <paramref name="rdnTypeInfo"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// When <paramref name="topLevelValues"/> is set to <see langword="true" />, treats the PipeReader as a sequence of
        /// whitespace separated top-level RDN values and attempts to deserialize each value into <typeparamref name="TValue"/>.
        /// When <paramref name="topLevelValues"/> is set to <see langword="false" />, treats the PipeReader as a RDN array and
        /// attempts to serialize each element into <typeparamref name="TValue"/>.
        /// </remarks>
        public static IAsyncEnumerable<TValue?> DeserializeAsyncEnumerable<TValue>(
            PipeReader utf8Rdn,
            RdnTypeInfo<TValue> rdnTypeInfo,
            bool topLevelValues,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(utf8Rdn, nameof(utf8Rdn));
            ArgumentNullException.ThrowIfNull(rdnTypeInfo, nameof(rdnTypeInfo));

            rdnTypeInfo.EnsureConfigured();
            return DeserializeAsyncEnumerableCore(utf8Rdn, rdnTypeInfo, topLevelValues, cancellationToken);
        }

        /// <summary>
        /// Wraps the UTF-8 encoded text into an <see cref="IAsyncEnumerable{TValue}" />
        /// that can be used to deserialize sequences of RDN values in a streaming manner.
        /// </summary>
        /// <typeparam name="TValue">The element type to deserialize asynchronously.</typeparam>
        /// <returns>An <see cref="IAsyncEnumerable{TValue}" /> representation of the provided RDN sequence.</returns>
        /// <param name="utf8Rdn">RDN data to parse.</param>
        /// <param name="topLevelValues"><see langword="true"/> to deserialize from a sequence of top-level RDN values, or <see langword="false"/> to deserialize from a single top-level array.</param>
        /// <param name="options">Options to control the behavior during reading.</param>
        /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> that can be used to cancel the read operation.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="utf8Rdn"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// When <paramref name="topLevelValues"/> is set to <see langword="true" />, treats the PipeReader as a sequence of
        /// whitespace separated top-level RDN values and attempts to deserialize each value into <typeparamref name="TValue"/>.
        /// When <paramref name="topLevelValues"/> is set to <see langword="false" />, treats the PipeReader as a RDN array and
        /// attempts to serialize each element into <typeparamref name="TValue"/>.
        /// </remarks>
        [RequiresUnreferencedCode(SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(SerializationRequiresDynamicCodeMessage)]
        public static IAsyncEnumerable<TValue?> DeserializeAsyncEnumerable<TValue>(
            PipeReader utf8Rdn,
            bool topLevelValues,
            RdnSerializerOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(utf8Rdn, nameof(utf8Rdn));

            RdnTypeInfo<TValue> rdnTypeInfo = GetTypeInfo<TValue>(options);
            return DeserializeAsyncEnumerableCore(utf8Rdn, rdnTypeInfo, topLevelValues, cancellationToken);
        }

        private static IAsyncEnumerable<T?> DeserializeAsyncEnumerableCore<T>(
            PipeReader utf8Rdn,
            RdnTypeInfo<T> rdnTypeInfo,
            bool topLevelValues,
            CancellationToken cancellationToken)
        {
            Debug.Assert(rdnTypeInfo.IsConfigured);

            RdnTypeInfo<List<T?>> listTypeInfo;
            RdnReaderOptions readerOptions = rdnTypeInfo.Options.GetReaderOptions();
            if (topLevelValues)
            {
                listTypeInfo = GetOrAddListTypeInfoForRootLevelValueMode(rdnTypeInfo);
                readerOptions.AllowMultipleValues = true;
            }
            else
            {
                listTypeInfo = GetOrAddListTypeInfoForArrayMode(rdnTypeInfo);
            }

            return CreateAsyncEnumerableFromArray(utf8Rdn, listTypeInfo, readerOptions, cancellationToken);

            static async IAsyncEnumerable<T?> CreateAsyncEnumerableFromArray(
                PipeReader utf8Rdn,
                RdnTypeInfo<List<T?>> listTypeInfo,
                RdnReaderOptions readerOptions,
                [EnumeratorCancellation] CancellationToken cancellationToken)
            {
                Debug.Assert(listTypeInfo.IsConfigured);

                ReadStack readStack = default;
                readStack.Initialize(listTypeInfo, supportContinuation: true);
                RdnReaderState rdnReaderState = new(readerOptions);
                PipeReadBufferState bufferState = new(utf8Rdn);

                try
                {
                    bool success;
                    do
                    {
                        bufferState = await bufferState.ReadAsync(utf8Rdn, cancellationToken, fillBuffer: false).ConfigureAwait(false);
                        success = listTypeInfo.ContinueDeserialize<PipeReadBufferState, PipeReader>(
                            ref bufferState,
                            ref rdnReaderState,
                            ref readStack,
                            out List<T?>? _);

                        if (readStack.Current.ReturnValue is { } returnValue)
                        {
                            var list = (List<T?>)returnValue;
                            foreach (T? item in list)
                            {
                                yield return item;
                            }

                            list.Clear();
                        }
                    } while (!success);
                }
                finally
                {
                    bufferState.Dispose();
                }
            }
        }
    }
}
