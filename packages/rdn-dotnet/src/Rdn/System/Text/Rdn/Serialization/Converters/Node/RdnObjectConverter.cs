// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Rdn.Nodes;
using Rdn.Schema;
using Rdn.Serialization.Metadata;

namespace Rdn.Serialization.Converters
{
    internal sealed class RdnObjectConverter : RdnConverter<RdnObject?>
    {
        internal override void ConfigureRdnTypeInfo(RdnTypeInfo rdnTypeInfo, RdnSerializerOptions options)
        {
            rdnTypeInfo.CreateObjectForExtensionDataProperty = () => new RdnObject(options.GetNodeOptions());
        }

        internal override void ReadElementAndSetProperty(
            object obj,
            string propertyName,
            ref Utf8RdnReader reader,
            RdnSerializerOptions options,
            scoped ref ReadStack state)
        {
            bool success = RdnNodeConverter.Instance.TryRead(ref reader, typeof(RdnNode), options, ref state, out RdnNode? value, out _);
            Debug.Assert(success); // Node converters are not resumable.

            Debug.Assert(obj is RdnObject);
            RdnObject jObject = (RdnObject)obj;

            Debug.Assert(value == null || value is RdnNode);
            RdnNode? jNodeValue = value;

            if (options.AllowDuplicateProperties)
            {
                jObject[propertyName] = jNodeValue;
            }
            else if (!jObject.TryAdd(propertyName, jNodeValue))
            {
                ThrowHelper.ThrowRdnException_DuplicatePropertyNotAllowed(propertyName);
            }
        }

        internal override void WriteExtensionDataValue(Utf8RdnWriter writer, RdnObject? value, RdnSerializerOptions options)
        {
            Debug.Assert(value is not null);
            value.WriteContentsTo(writer, options);
        }

        public override void Write(Utf8RdnWriter writer, RdnObject? value, RdnSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            value.WriteTo(writer, options);
        }

        public override RdnObject? Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case RdnTokenType.StartObject:
                    return options.AllowDuplicateProperties
                        ? ReadAsRdnElement(ref reader, options.GetNodeOptions())
                        : ReadAsRdnNode(ref reader, options.GetNodeOptions());
                case RdnTokenType.Null:
                    return null;
                default:
                    throw ThrowHelper.GetInvalidOperationException_ExpectedObject(reader.TokenType);
            }
        }

        internal static RdnObject ReadAsRdnElement(ref Utf8RdnReader reader, RdnNodeOptions options)
        {
            RdnElement jElement = RdnElement.ParseValue(ref reader);
            return new RdnObject(jElement, options);
        }

        internal static RdnObject ReadAsRdnNode(ref Utf8RdnReader reader, RdnNodeOptions options)
        {
            Debug.Assert(reader.TokenType == RdnTokenType.StartObject);

            RdnObject jObject = new RdnObject(options);

            while (reader.Read())
            {
                if (reader.TokenType == RdnTokenType.EndObject)
                {
                    return jObject;
                }

                if (reader.TokenType != RdnTokenType.PropertyName)
                {
                    // RDN is invalid so reader would have already thrown.
                    Debug.Fail("Property name expected.");
                    ThrowHelper.ThrowRdnException();
                }

                string propertyName = reader.GetString()!;
                reader.Read(); // Move to the value token.
                RdnNode? value = RdnNodeConverter.ReadAsRdnNode(ref reader, options);

                // To have parity with the lazy RdnObject, we throw on duplicates.
                jObject.Add(propertyName, value);
            }

            // RDN is invalid so reader would have already thrown.
            Debug.Fail("End object token not found.");
            ThrowHelper.ThrowRdnException();
            return null;
        }

        internal override RdnSchema? GetSchema(RdnNumberHandling _) => new() { Type = RdnSchemaType.Object };
    }
}
