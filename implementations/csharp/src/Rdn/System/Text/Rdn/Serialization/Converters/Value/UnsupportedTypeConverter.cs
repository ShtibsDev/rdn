// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Rdn.Schema;
using Rdn.Nodes;

namespace Rdn.Serialization.Converters
{
    internal sealed class UnsupportedTypeConverter<T> : RdnConverter<T>
    {
        private readonly string? _errorMessage;

        public UnsupportedTypeConverter(string? errorMessage = null) => _errorMessage = errorMessage;

        public string ErrorMessage => _errorMessage ?? SR.Format(SR.SerializeTypeInstanceNotSupported, typeof(T).FullName);

        public override T Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options) =>
            throw new NotSupportedException(ErrorMessage);

        public override void Write(Utf8RdnWriter writer, T value, RdnSerializerOptions options) =>
            throw new NotSupportedException(ErrorMessage);

        internal override RdnSchema? GetSchema(RdnNumberHandling _) =>
            new RdnSchema { Comment = "Unsupported .NET type", Not = RdnSchema.CreateTrueSchema() };
    }
}
