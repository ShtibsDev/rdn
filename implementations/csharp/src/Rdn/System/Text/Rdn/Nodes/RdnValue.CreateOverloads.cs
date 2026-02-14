// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Rdn.Serialization.Metadata;

namespace Rdn.Nodes
{
    public partial class RdnValue
    {
        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="RdnValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        public static RdnValue Create(bool value, RdnNodeOptions? options = null) => new RdnValuePrimitive<bool>(value, RdnMetadataServices.BooleanConverter, options);

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="RdnValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        public static RdnValue? Create(bool? value, RdnNodeOptions? options = null) => value.HasValue ? new RdnValuePrimitive<bool>(value.Value, RdnMetadataServices.BooleanConverter, options) : null;

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="RdnValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        public static RdnValue Create(byte value, RdnNodeOptions? options = null) => new RdnValuePrimitive<byte>(value, RdnMetadataServices.ByteConverter, options);

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="RdnValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        public static RdnValue? Create(byte? value, RdnNodeOptions? options = null) => value.HasValue ? new RdnValuePrimitive<byte>(value.Value, RdnMetadataServices.ByteConverter, options) : null;

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="RdnValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        public static RdnValue Create(char value, RdnNodeOptions? options = null) => new RdnValuePrimitive<char>(value, RdnMetadataServices.CharConverter, options);

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="RdnValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        public static RdnValue? Create(char? value, RdnNodeOptions? options = null) => value.HasValue ? new RdnValuePrimitive<char>(value.Value, RdnMetadataServices.CharConverter, options) : null;

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="RdnValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        public static RdnValue Create(DateTime value, RdnNodeOptions? options = null) => new RdnValuePrimitive<DateTime>(value, RdnMetadataServices.DateTimeConverter, options);

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="RdnValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        public static RdnValue? Create(DateTime? value, RdnNodeOptions? options = null) => value.HasValue ? new RdnValuePrimitive<DateTime>(value.Value, RdnMetadataServices.DateTimeConverter, options) : null;

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="RdnValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        public static RdnValue Create(DateTimeOffset value, RdnNodeOptions? options = null) => new RdnValuePrimitive<DateTimeOffset>(value, RdnMetadataServices.DateTimeOffsetConverter, options);

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="RdnValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        public static RdnValue? Create(DateTimeOffset? value, RdnNodeOptions? options = null) => value.HasValue ? new RdnValuePrimitive<DateTimeOffset>(value.Value, RdnMetadataServices.DateTimeOffsetConverter, options) : null;

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="RdnValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        public static RdnValue Create(decimal value, RdnNodeOptions? options = null) => new RdnValuePrimitive<decimal>(value, RdnMetadataServices.DecimalConverter, options);

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="RdnValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        public static RdnValue? Create(decimal? value, RdnNodeOptions? options = null) => value.HasValue ? new RdnValuePrimitive<decimal>(value.Value, RdnMetadataServices.DecimalConverter, options) : null;

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="RdnValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        public static RdnValue Create(double value, RdnNodeOptions? options = null) => new RdnValuePrimitive<double>(value, RdnMetadataServices.DoubleConverter, options);

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="RdnValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        public static RdnValue? Create(double? value, RdnNodeOptions? options = null) => value.HasValue ? new RdnValuePrimitive<double>(value.Value, RdnMetadataServices.DoubleConverter, options) : null;

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="RdnValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        public static RdnValue Create(Guid value, RdnNodeOptions? options = null) => new RdnValuePrimitive<Guid>(value, RdnMetadataServices.GuidConverter, options);

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="RdnValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        public static RdnValue? Create(Guid? value, RdnNodeOptions? options = null) => value.HasValue ? new RdnValuePrimitive<Guid>(value.Value, RdnMetadataServices.GuidConverter, options) : null;

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="RdnValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        public static RdnValue Create(short value, RdnNodeOptions? options = null) => new RdnValuePrimitive<short>(value, RdnMetadataServices.Int16Converter, options);

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="RdnValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        public static RdnValue? Create(short? value, RdnNodeOptions? options = null) => value.HasValue ? new RdnValuePrimitive<short>(value.Value, RdnMetadataServices.Int16Converter, options) : null;

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="RdnValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        public static RdnValue Create(int value, RdnNodeOptions? options = null) => new RdnValuePrimitive<int>(value, RdnMetadataServices.Int32Converter, options);

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="RdnValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        public static RdnValue? Create(int? value, RdnNodeOptions? options = null) => value.HasValue ? new RdnValuePrimitive<int>(value.Value, RdnMetadataServices.Int32Converter, options) : null;

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="RdnValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        public static RdnValue Create(long value, RdnNodeOptions? options = null) => new RdnValuePrimitive<long>(value, RdnMetadataServices.Int64Converter, options);

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="RdnValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        public static RdnValue? Create(long? value, RdnNodeOptions? options = null) => value.HasValue ? new RdnValuePrimitive<long>(value.Value, RdnMetadataServices.Int64Converter, options) : null;

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="RdnValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        [CLSCompliantAttribute(false)]
        public static RdnValue Create(sbyte value, RdnNodeOptions? options = null) => new RdnValuePrimitive<sbyte>(value, RdnMetadataServices.SByteConverter, options);

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="RdnValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        [CLSCompliantAttribute(false)]
        public static RdnValue? Create(sbyte? value, RdnNodeOptions? options = null) => value.HasValue ? new RdnValuePrimitive<sbyte>(value.Value, RdnMetadataServices.SByteConverter, options) : null;

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="RdnValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        public static RdnValue Create(float value, RdnNodeOptions? options = null) => new RdnValuePrimitive<float>(value, RdnMetadataServices.SingleConverter, options);

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="RdnValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        public static RdnValue? Create(float? value, RdnNodeOptions? options = null) => value.HasValue ? new RdnValuePrimitive<float>(value.Value, RdnMetadataServices.SingleConverter, options) : null;

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="RdnValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        [return: NotNullIfNotNull(nameof(value))]
        public static RdnValue? Create(string? value, RdnNodeOptions? options = null) => value != null ? new RdnValuePrimitive<string>(value, RdnMetadataServices.StringConverter!, options) : null;

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="RdnValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        [CLSCompliantAttribute(false)]
        public static RdnValue Create(ushort value, RdnNodeOptions? options = null) => new RdnValuePrimitive<ushort>(value, RdnMetadataServices.UInt16Converter, options);

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="RdnValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        [CLSCompliantAttribute(false)]
        public static RdnValue? Create(ushort? value, RdnNodeOptions? options = null) => value.HasValue ? new RdnValuePrimitive<ushort>(value.Value, RdnMetadataServices.UInt16Converter, options) : null;

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="RdnValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        [CLSCompliantAttribute(false)]
        public static RdnValue Create(uint value, RdnNodeOptions? options = null) => new RdnValuePrimitive<uint>(value, RdnMetadataServices.UInt32Converter, options);

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="RdnValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        [CLSCompliantAttribute(false)]
        public static RdnValue? Create(uint? value, RdnNodeOptions? options = null) => value.HasValue ? new RdnValuePrimitive<uint>(value.Value, RdnMetadataServices.UInt32Converter, options) : null;

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="RdnValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        [CLSCompliantAttribute(false)]
        public static RdnValue Create(ulong value, RdnNodeOptions? options = null) => new RdnValuePrimitive<ulong>(value, RdnMetadataServices.UInt64Converter, options);

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="RdnValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        [CLSCompliantAttribute(false)]
        public static RdnValue? Create(ulong? value, RdnNodeOptions? options = null) => value.HasValue ? new RdnValuePrimitive<ulong>(value.Value, RdnMetadataServices.UInt64Converter, options) : null;

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="RdnValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        public static RdnValue? Create(RdnElement value, RdnNodeOptions? options = null) => RdnValue.CreateFromElement(ref value, options);

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="RdnValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        public static RdnValue? Create(RdnElement? value, RdnNodeOptions? options = null) => value is RdnElement element ? RdnValue.CreateFromElement(ref element, options) : null;

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <param name="value">The underlying value of the new <see cref="RdnValue"/> instance.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        public static RdnValue? Create(System.Text.RegularExpressions.Regex? value, RdnNodeOptions? options = null) => value != null ? new RdnValuePrimitive<System.Text.RegularExpressions.Regex>(value, RdnMetadataServices.RegexConverter, options) : null;
    }
}
