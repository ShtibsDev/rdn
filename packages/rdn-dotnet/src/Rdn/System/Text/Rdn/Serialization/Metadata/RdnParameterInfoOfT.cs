// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Rdn.Serialization.Metadata
{
    /// <summary>
    /// Represents a strongly-typed parameter to prevent boxing where have less than 4 parameters.
    /// Holds relevant state like the default value of the parameter, and the position in the method's parameter list.
    /// </summary>
    internal sealed class RdnParameterInfo<T> : RdnParameterInfo
    {
        public new RdnConverter<T> EffectiveConverter => MatchingProperty.EffectiveConverter;
        public new RdnPropertyInfo<T> MatchingProperty { get; }
        public new T? EffectiveDefaultValue { get; }

        public RdnParameterInfo(RdnParameterInfoValues parameterInfoValues, RdnPropertyInfo<T> matchingPropertyInfo)
            : base(parameterInfoValues, matchingPropertyInfo)
        {
            Debug.Assert(parameterInfoValues.ParameterType == typeof(T));
            Debug.Assert(!matchingPropertyInfo.IsConfigured);

            if (parameterInfoValues is { HasDefaultValue: true, DefaultValue: object defaultValue })
            {
                EffectiveDefaultValue = (T)defaultValue;
            }

            MatchingProperty = matchingPropertyInfo;
            base.EffectiveDefaultValue = EffectiveDefaultValue;
        }
    }
}
