// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Rdn.Schema;
using Rdn.Nodes;

namespace Rdn.Serialization.Converters
{
    internal sealed class RdnElementConverter : RdnConverter<RdnElement>
    {
        public override RdnElement Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            return RdnElement.ParseValue(ref reader, options.AllowDuplicateProperties);
        }

        public override void Write(Utf8RdnWriter writer, RdnElement value, RdnSerializerOptions options)
        {
            value.WriteTo(writer);
        }

        internal override RdnSchema? GetSchema(RdnNumberHandling _) => RdnSchema.CreateTrueSchema();
    }
}
