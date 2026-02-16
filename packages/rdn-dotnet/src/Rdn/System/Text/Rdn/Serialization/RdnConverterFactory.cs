// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Rdn.Serialization.Metadata;

namespace Rdn.Serialization
{
    /// <summary>
    /// Supports converting several types by using a factory pattern.
    /// </summary>
    /// <remarks>
    /// This is useful for converters supporting generics, such as a converter for <see cref="System.Collections.Generic.List{T}"/>.
    /// </remarks>
    public abstract class RdnConverterFactory : RdnConverter
    {
        /// <summary>
        /// When overridden, constructs a new <see cref="RdnConverterFactory"/> instance.
        /// </summary>
        protected RdnConverterFactory() { }

        private protected override ConverterStrategy GetDefaultConverterStrategy() => ConverterStrategy.None;

        /// <summary>
        /// Create a converter for the provided <see cref="System.Type"/>.
        /// </summary>
        /// <param name="typeToConvert">The <see cref="System.Type"/> being converted.</param>
        /// <param name="options">The <see cref="RdnSerializerOptions"/> being used.</param>
        /// <returns>
        /// An instance of a <see cref="RdnConverter{T}"/> where T is compatible with <paramref name="typeToConvert"/>.
        /// If <see langword="null"/> is returned, a <see cref="NotSupportedException"/> will be thrown.
        /// </returns>
        public abstract RdnConverter? CreateConverter(Type typeToConvert, RdnSerializerOptions options);

        internal RdnConverter GetConverterInternal(Type typeToConvert, RdnSerializerOptions options)
        {
            Debug.Assert(CanConvert(typeToConvert));

            RdnConverter? converter = CreateConverter(typeToConvert, options);
            switch (converter)
            {
                case null:
                    ThrowHelper.ThrowInvalidOperationException_SerializerConverterFactoryReturnsNull(GetType());
                    break;
                case RdnConverterFactory:
                    ThrowHelper.ThrowInvalidOperationException_SerializerConverterFactoryReturnsRdnConverterFactory(GetType());
                    break;
            }

            return converter;
        }

        internal sealed override object? ReadAsObject(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            Debug.Fail("We should never get here.");

            throw new InvalidOperationException();
        }

        internal sealed override bool OnTryReadAsObject(
            ref Utf8RdnReader reader,
            Type typeToConvert,
            RdnSerializerOptions options,
            scoped ref ReadStack state,
            out object? value)
        {
            Debug.Fail("We should never get here.");

            throw new InvalidOperationException();
        }

        internal sealed override bool TryReadAsObject(
            ref Utf8RdnReader reader,
            Type typeToConvert,
            RdnSerializerOptions options,
            scoped ref ReadStack state,
            out object? value)
        {
            Debug.Fail("We should never get here.");

            throw new InvalidOperationException();
        }

        internal sealed override object? ReadAsPropertyNameAsObject(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            Debug.Fail("We should never get here.");

            throw new InvalidOperationException();
        }

        internal sealed override object? ReadAsPropertyNameCoreAsObject(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            Debug.Fail("We should never get here.");

            throw new InvalidOperationException();
        }

        internal sealed override object? ReadNumberWithCustomHandlingAsObject(ref Utf8RdnReader reader, RdnNumberHandling handling, RdnSerializerOptions options)
        {
            Debug.Fail("We should never get here.");

            throw new InvalidOperationException();
        }

        internal sealed override void WriteAsObject(Utf8RdnWriter writer, object? value, RdnSerializerOptions options)
        {
            Debug.Fail("We should never get here.");

            throw new InvalidOperationException();
        }

        internal sealed override bool OnTryWriteAsObject(
            Utf8RdnWriter writer,
            object? value,
            RdnSerializerOptions options,
            ref WriteStack state)
        {
            Debug.Fail("We should never get here.");

            throw new InvalidOperationException();
        }

        internal sealed override bool TryWriteAsObject(
            Utf8RdnWriter writer,
            object? value,
            RdnSerializerOptions options,
            ref WriteStack state)
        {
            Debug.Fail("We should never get here.");

            throw new InvalidOperationException();
        }

        internal sealed override void WriteAsPropertyNameAsObject(Utf8RdnWriter writer, object? value, RdnSerializerOptions options)
        {
            Debug.Fail("We should never get here.");

            throw new InvalidOperationException();
        }

        /// <inheritdoc/>
        public sealed override Type? Type => null;

        internal sealed override void WriteAsPropertyNameCoreAsObject(
            Utf8RdnWriter writer,
            object? value,
            RdnSerializerOptions options,
            bool isWritingExtensionDataProperty)
        {
            Debug.Fail("We should never get here.");

            throw new InvalidOperationException();
        }

        internal sealed override void WriteNumberWithCustomHandlingAsObject(Utf8RdnWriter writer, object? value, RdnNumberHandling handling)
        {
            Debug.Fail("We should never get here.");

            throw new InvalidOperationException();
        }
    }
}
