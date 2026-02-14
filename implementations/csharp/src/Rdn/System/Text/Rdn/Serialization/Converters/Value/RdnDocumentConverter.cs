// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Rdn.Schema;
using Rdn.Nodes;

namespace Rdn.Serialization.Converters
{
    internal sealed class RdnDocumentConverter : RdnConverter<RdnDocument?>
    {
        public override bool HandleNull => true;

        public override RdnDocument Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options) =>
            RdnDocument.ParseValue(ref reader, options.AllowDuplicateProperties);

        public override void Write(Utf8RdnWriter writer, RdnDocument? value, RdnSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            value.WriteTo(writer);
        }

        internal override RdnSchema? GetSchema(RdnNumberHandling _) => RdnSchema.CreateTrueSchema();
    }
}
