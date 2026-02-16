// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Rdn.Nodes;
using Rdn.Serialization.Metadata;

namespace Rdn.Serialization.Converters
{
    internal sealed class RdnNodeConverterFactory : RdnConverterFactory
    {
        private static readonly RdnArrayConverter s_arrayConverter = new RdnArrayConverter();
        private static readonly RdnObjectConverter s_objectConverter = new RdnObjectConverter();
        private static readonly RdnValueConverter s_valueConverter = new RdnValueConverter();
        private static readonly RdnSetConverter s_setConverter = new RdnSetConverter();
        private static readonly RdnMapConverter s_mapConverter = new RdnMapConverter();

        public override RdnConverter? CreateConverter(Type typeToConvert, RdnSerializerOptions options)
        {
            if (typeof(RdnValue).IsAssignableFrom(typeToConvert))
            {
                return s_valueConverter;
            }

            if (typeof(RdnObject) == typeToConvert)
            {
                return s_objectConverter;
            }

            if (typeof(RdnArray) == typeToConvert)
            {
                return s_arrayConverter;
            }

            if (typeof(RdnSet) == typeToConvert)
            {
                return s_setConverter;
            }

            if (typeof(RdnMap) == typeToConvert)
            {
                return s_mapConverter;
            }

            Debug.Assert(typeof(RdnNode) == typeToConvert);
            return RdnNodeConverter.Instance;
        }

        public override bool CanConvert(Type typeToConvert) => typeof(RdnNode).IsAssignableFrom(typeToConvert);
    }
}
