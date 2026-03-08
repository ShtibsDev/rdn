// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Rdn.Nodes;
using Rdn.Serialization.Metadata;

namespace Rdn.Serialization.Converters
{
    internal abstract class ObjectConverter : RdnConverter<object?>
    {
        private protected override ConverterStrategy GetDefaultConverterStrategy() => ConverterStrategy.Object;

        public ObjectConverter()
        {
            CanBePolymorphic = true;
        }

        public sealed override object ReadAsPropertyName(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            ThrowHelper.ThrowNotSupportedException_DictionaryKeyTypeNotSupported(Type, this);
            return null!;
        }

        internal sealed override object ReadAsPropertyNameCore(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            ThrowHelper.ThrowNotSupportedException_DictionaryKeyTypeNotSupported(Type, this);
            return null!;
        }

        public sealed override void Write(Utf8RdnWriter writer, object? value, RdnSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStartObject();
            writer.WriteEndObject();
        }

        public sealed override void WriteAsPropertyName(Utf8RdnWriter writer, object value, RdnSerializerOptions options)
        {
            WriteAsPropertyNameCore(writer, value, options, isWritingExtensionDataProperty: false);
        }

        internal sealed override void WriteAsPropertyNameCore(Utf8RdnWriter writer, object value, RdnSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            ArgumentNullException.ThrowIfNull(value);

            Type runtimeType = value.GetType();
            if (runtimeType == Type)
            {
                ThrowHelper.ThrowNotSupportedException_DictionaryKeyTypeNotSupported(runtimeType, this);
            }

            RdnConverter runtimeConverter = options.GetConverterInternal(runtimeType);
            runtimeConverter.WriteAsPropertyNameCoreAsObject(writer, value, options, isWritingExtensionDataProperty);
        }
    }

    /// <summary>
    /// Defines an object converter that only supports (polymorphic) serialization but not deserialization.
    /// This is done to avoid rooting dependencies to RdnNode/RdnElement necessary to drive object deserialization.
    /// Source generator users need to explicitly declare support for object so that the derived converter gets used.
    /// </summary>
    internal sealed class SlimObjectConverter : ObjectConverter
    {
        // Keep track of the originating resolver so that the converter surfaces
        // an accurate error message whenever deserialization is attempted.
        private readonly IRdnTypeInfoResolver _originatingResolver;

        public SlimObjectConverter(IRdnTypeInfoResolver originatingResolver)
            => _originatingResolver = originatingResolver;

        public override object? Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            ThrowHelper.ThrowNotSupportedException_NoMetadataForType(typeToConvert, _originatingResolver);
            return null;
        }
    }

    /// <summary>
    /// Defines an object converter that supports deserialization via RdnElement/RdnNode representations.
    /// Used as the default in reflection or if object is declared in the type info resolver type graph.
    /// </summary>
    internal sealed class DefaultObjectConverter : ObjectConverter
    {
        public DefaultObjectConverter()
        {
            // RdnElement/RdnNode parsing does not support async; force read ahead for now.
            RequiresReadAhead = true;
        }

        public override object? Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            if (options.UnknownTypeHandling == RdnUnknownTypeHandling.RdnElement)
            {
                return RdnElement.ParseValue(ref reader, options.AllowDuplicateProperties);
            }

            Debug.Assert(options.UnknownTypeHandling == RdnUnknownTypeHandling.RdnNode);
            return RdnNodeConverter.Instance.Read(ref reader, typeToConvert, options);
        }

        internal override bool OnTryRead(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options, scoped ref ReadStack state, out object? value)
        {
            if (options.UnknownTypeHandling == RdnUnknownTypeHandling.RdnElement)
            {
                value = RdnElement.ParseValue(ref reader, options.AllowDuplicateProperties);
                return true;
            }

            Debug.Assert(options.UnknownTypeHandling == RdnUnknownTypeHandling.RdnNode);
            value = RdnNodeConverter.Instance.Read(ref reader, typeToConvert, options);
            return true;
        }
    }
}
