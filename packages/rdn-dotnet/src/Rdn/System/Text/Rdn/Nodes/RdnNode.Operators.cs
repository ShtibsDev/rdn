// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;

namespace Rdn.Nodes
{
    public partial class RdnNode
    {
        /// <summary>
        ///   Defines an implicit conversion of a given <see cref="bool"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="bool"/> to implicitly convert.</param>
        /// <returns>A <see cref="RdnNode"/> instance converted from the <paramref name="value"/> parameter.</returns>
        public static implicit operator RdnNode(bool value) => RdnValue.Create(value);

        /// <summary>
        ///   Defines an implicit conversion of a given <see cref="bool"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="bool"/> to implicitly convert.</param>
        /// <returns>A <see cref="RdnNode"/> instance converted from the <paramref name="value"/> parameter.</returns>
        public static implicit operator RdnNode?(bool? value) => RdnValue.Create(value);

        /// <summary>
        ///   Defines an implicit conversion of a given <see cref="byte"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="byte"/> to implicitly convert.</param>
        /// <returns>A <see cref="RdnNode"/> instance converted from the <paramref name="value"/> parameter.</returns>
        public static implicit operator RdnNode(byte value) => RdnValue.Create(value);

        /// <summary>
        ///   Defines an implicit conversion of a given <see cref="byte"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="byte"/> to implicitly convert.</param>
        /// <returns>A <see cref="RdnNode"/> instance converted from the <paramref name="value"/> parameter.</returns>
        public static implicit operator RdnNode?(byte? value) => RdnValue.Create(value);

        /// <summary>
        ///   Defines an implicit conversion of a given <see cref="char"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="char"/> to implicitly convert.</param>
        /// <returns>A <see cref="RdnNode"/> instance converted from the <paramref name="value"/> parameter.</returns>
        public static implicit operator RdnNode(char value) => RdnValue.Create(value);

        /// <summary>
        ///   Defines an implicit conversion of a given <see cref="char"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="char"/> to implicitly convert.</param>
        /// <returns>A <see cref="RdnNode"/> instance converted from the <paramref name="value"/> parameter.</returns>
        public static implicit operator RdnNode?(char? value) => RdnValue.Create(value);

        /// <summary>
        ///   Defines an implicit conversion of a given <see cref="DateTime"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="DateTime"/> to implicitly convert.</param>
        /// <returns>A <see cref="RdnNode"/> instance converted from the <paramref name="value"/> parameter.</returns>
        public static implicit operator RdnNode(DateTime value) => RdnValue.Create(value);

        /// <summary>
        ///   Defines an implicit conversion of a given <see cref="DateTime"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="DateTime"/> to implicitly convert.</param>
        /// <returns>A <see cref="RdnNode"/> instance converted from the <paramref name="value"/> parameter.</returns>
        public static implicit operator RdnNode?(DateTime? value) => RdnValue.Create(value);

        /// <summary>
        ///   Defines an implicit conversion of a given <see cref="DateTimeOffset"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="DateTimeOffset"/> to implicitly convert.</param>
        /// <returns>A <see cref="RdnNode"/> instance converted from the <paramref name="value"/> parameter.</returns>
        public static implicit operator RdnNode(DateTimeOffset value) => RdnValue.Create(value);

        /// <summary>
        ///   Defines an implicit conversion of a given <see cref="DateTimeOffset"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="DateTimeOffset"/> to implicitly convert.</param>
        /// <returns>A <see cref="RdnNode"/> instance converted from the <paramref name="value"/> parameter.</returns>
        public static implicit operator RdnNode?(DateTimeOffset? value) => RdnValue.Create(value);

        /// <summary>
        ///   Defines an implicit conversion of a given <see cref="decimal"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="decimal"/> to implicitly convert.</param>
        /// <returns>A <see cref="RdnNode"/> instance converted from the <paramref name="value"/> parameter.</returns>
        public static implicit operator RdnNode(decimal value) => RdnValue.Create(value);

        /// <summary>
        ///   Defines an implicit conversion of a given <see cref="decimal"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="decimal"/> to implicitly convert.</param>
        /// <returns>A <see cref="RdnNode"/> instance converted from the <paramref name="value"/> parameter.</returns>
        public static implicit operator RdnNode?(decimal? value) => RdnValue.Create(value);

        /// <summary>
        ///   Defines an implicit conversion of a given <see cref="double"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="double"/> to implicitly convert.</param>
        /// <returns>A <see cref="RdnNode"/> instance converted from the <paramref name="value"/> parameter.</returns>
        public static implicit operator RdnNode(double value) => RdnValue.Create(value);

        /// <summary>
        ///   Defines an implicit conversion of a given <see cref="double"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="double"/> to implicitly convert.</param>
        /// <returns>A <see cref="RdnNode"/> instance converted from the <paramref name="value"/> parameter.</returns>
        public static implicit operator RdnNode?(double? value) => RdnValue.Create(value);

        /// <summary>
        ///   Defines an implicit conversion of a given <see cref="Guid"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="Guid"/> to implicitly convert.</param>
        /// <returns>A <see cref="RdnNode"/> instance converted from the <paramref name="value"/> parameter.</returns>
        public static implicit operator RdnNode(Guid value) => RdnValue.Create(value);

        /// <summary>
        ///   Defines an implicit conversion of a given <see cref="Guid"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="Guid"/> to implicitly convert.</param>
        /// <returns>A <see cref="RdnNode"/> instance converted from the <paramref name="value"/> parameter.</returns>
        public static implicit operator RdnNode?(Guid? value) => RdnValue.Create(value);

        /// <summary>
        ///   Defines an implicit conversion of a given <see cref="short"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="short"/> to implicitly convert.</param>
        /// <returns>A <see cref="RdnNode"/> instance converted from the <paramref name="value"/> parameter.</returns>
        public static implicit operator RdnNode(short value) => RdnValue.Create(value);

        /// <summary>
        ///   Defines an implicit conversion of a given <see cref="short"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="short"/> to implicitly convert.</param>
        /// <returns>A <see cref="RdnNode"/> instance converted from the <paramref name="value"/> parameter.</returns>
        public static implicit operator RdnNode?(short? value) => RdnValue.Create(value);

        /// <summary>
        ///   Defines an implicit conversion of a given <see cref="int"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="int"/> to implicitly convert.</param>
        /// <returns>A <see cref="RdnNode"/> instance converted from the <paramref name="value"/> parameter.</returns>
        public static implicit operator RdnNode(int value) => RdnValue.Create(value);

        /// <summary>
        ///   Defines an implicit conversion of a given <see cref="int"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="int"/> to implicitly convert.</param>
        /// <returns>A <see cref="RdnNode"/> instance converted from the <paramref name="value"/> parameter.</returns>
        public static implicit operator RdnNode?(int? value) => RdnValue.Create(value);

        /// <summary>
        ///   Defines an implicit conversion of a given <see cref="long"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="long"/> to implicitly convert.</param>
        /// <returns>A <see cref="RdnNode"/> instance converted from the <paramref name="value"/> parameter.</returns>
        public static implicit operator RdnNode(long value) => RdnValue.Create(value);

        /// <summary>
        ///   Defines an implicit conversion of a given <see cref="long"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="long"/> to implicitly convert.</param>
        /// <returns>A <see cref="RdnNode"/> instance converted from the <paramref name="value"/> parameter.</returns>
        public static implicit operator RdnNode?(long? value) => RdnValue.Create(value);

        /// <summary>
        ///   Defines an implicit conversion of a given <see cref="sbyte"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="sbyte"/> to implicitly convert.</param>
        /// <returns>A <see cref="RdnNode"/> instance converted from the <paramref name="value"/> parameter.</returns>
        [System.CLSCompliantAttribute(false)]
        public static implicit operator RdnNode(sbyte value) => RdnValue.Create(value);

        /// <summary>
        ///   Defines an implicit conversion of a given <see cref="sbyte"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="sbyte"/> to implicitly convert.</param>
        /// <returns>A <see cref="RdnNode"/> instance converted from the <paramref name="value"/> parameter.</returns>
        [System.CLSCompliantAttribute(false)]
        public static implicit operator RdnNode?(sbyte? value) => RdnValue.Create(value);

        /// <summary>
        ///   Defines an implicit conversion of a given <see cref="float"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="float"/> to implicitly convert.</param>
        /// <returns>A <see cref="RdnNode"/> instance converted from the <paramref name="value"/> parameter.</returns>
        public static implicit operator RdnNode(float value) => RdnValue.Create(value);

        /// <summary>
        ///   Defines an implicit conversion of a given <see cref="float"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="float"/> to implicitly convert.</param>
        /// <returns>A <see cref="RdnNode"/> instance converted from the <paramref name="value"/> parameter.</returns>
        public static implicit operator RdnNode?(float? value) => RdnValue.Create(value);

        /// <summary>
        ///   Defines an implicit conversion of a given <see cref="string"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="string"/> to implicitly convert.</param>
        /// <returns>A <see cref="RdnNode"/> instance converted from the <paramref name="value"/> parameter.</returns>
        [return: NotNullIfNotNull(nameof(value))]
        public static implicit operator RdnNode?(string? value) => RdnValue.Create(value);

        /// <summary>
        ///   Defines an implicit conversion of a given <see cref="ushort"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="ushort"/> to implicitly convert.</param>
        /// <returns>A <see cref="RdnNode"/> instance converted from the <paramref name="value"/> parameter.</returns>
        [System.CLSCompliantAttribute(false)]
        public static implicit operator RdnNode(ushort value) => RdnValue.Create(value);

        /// <summary>
        ///   Defines an implicit conversion of a given <see cref="ushort"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="ushort"/> to implicitly convert.</param>
        /// <returns>A <see cref="RdnNode"/> instance converted from the <paramref name="value"/> parameter.</returns>
        [System.CLSCompliantAttribute(false)]
        public static implicit operator RdnNode?(ushort? value) => RdnValue.Create(value);

        /// <summary>
        ///   Defines an implicit conversion of a given <see cref="uint"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="uint"/> to implicitly convert.</param>
        /// <returns>A <see cref="RdnNode"/> instance converted from the <paramref name="value"/> parameter.</returns>
        [System.CLSCompliantAttribute(false)]
        public static implicit operator RdnNode(uint value) => RdnValue.Create(value);

        /// <summary>
        ///   Defines an implicit conversion of a given <see cref="uint"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="uint"/> to implicitly convert.</param>
        /// <returns>A <see cref="RdnNode"/> instance converted from the <paramref name="value"/> parameter.</returns>
        [System.CLSCompliantAttribute(false)]
        public static implicit operator RdnNode?(uint? value) => RdnValue.Create(value);

        /// <summary>
        ///   Defines an implicit conversion of a given <see cref="ulong"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="ulong"/> to implicitly convert.</param>
        /// <returns>A <see cref="RdnNode"/> instance converted from the <paramref name="value"/> parameter.</returns>
        [System.CLSCompliantAttribute(false)]
        public static implicit operator RdnNode(ulong value) => RdnValue.Create(value);

        /// <summary>
        ///   Defines an implicit conversion of a given <see cref="ulong"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="ulong"/> to implicitly convert.</param>
        /// <returns>A <see cref="RdnNode"/> instance converted from the <paramref name="value"/> parameter.</returns>
        [System.CLSCompliantAttribute(false)]
        public static implicit operator RdnNode?(ulong? value) => RdnValue.Create(value);

        /// <summary>
        ///   Defines an explicit conversion of a given <see cref="bool"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="bool"/> to explicitly convert.</param>
        /// <returns>A value converted from the <see cref="RdnNode"/> instance.</returns>
        public static explicit operator bool(RdnNode value) => value.GetValue<bool>();

        /// <summary>
        ///   Defines an explicit conversion of a given <see cref="bool"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="bool"/> to explicitly convert.</param>
        /// <returns>A value converted from the <see cref="RdnNode"/> instance.</returns>
        public static explicit operator bool?(RdnNode? value) => value?.GetValue<bool>();

        /// <summary>
        ///   Defines an explicit conversion of a given <see cref="byte"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="byte"/> to explicitly convert.</param>
        /// <returns>A value converted from the <see cref="RdnNode"/> instance.</returns>
        public static explicit operator byte(RdnNode value) => value.GetValue<byte>();

        /// <summary>
        ///   Defines an explicit conversion of a given <see cref="byte"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="byte"/> to explicitly convert.</param>
        /// <returns>A value converted from the <see cref="RdnNode"/> instance.</returns>
        public static explicit operator byte?(RdnNode? value) => value?.GetValue<byte>();

        /// <summary>
        ///   Defines an explicit conversion of a given <see cref="char"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="char"/> to explicitly convert.</param>
        /// <returns>A value converted from the <see cref="RdnNode"/> instance.</returns>
        public static explicit operator char(RdnNode value) => value.GetValue<char>();

        /// <summary>
        ///   Defines an explicit conversion of a given <see cref="char"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="char"/> to explicitly convert.</param>
        /// <returns>A value converted from the <see cref="RdnNode"/> instance.</returns>
        public static explicit operator char?(RdnNode? value) => value?.GetValue<char>();

        /// <summary>
        ///   Defines an explicit conversion of a given <see cref="DateTime"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="DateTime"/> to explicitly convert.</param>
        /// <returns>A value converted from the <see cref="RdnNode"/> instance.</returns>
        public static explicit operator DateTime(RdnNode value) => value.GetValue<DateTime>();

        /// <summary>
        ///   Defines an explicit conversion of a given <see cref="DateTime"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="DateTime"/> to explicitly convert.</param>
        /// <returns>A value converted from the <see cref="RdnNode"/> instance.</returns>
        public static explicit operator DateTime?(RdnNode? value) => value?.GetValue<DateTime>();

        /// <summary>
        ///   Defines an explicit conversion of a given <see cref="DateTimeOffset"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="DateTimeOffset"/> to explicitly convert.</param>
        /// <returns>A value converted from the <see cref="RdnNode"/> instance.</returns>
        public static explicit operator DateTimeOffset(RdnNode value) => value.GetValue<DateTimeOffset>();

        /// <summary>
        ///   Defines an explicit conversion of a given <see cref="DateTimeOffset"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="DateTimeOffset"/> to explicitly convert.</param>
        /// <returns>A value converted from the <see cref="RdnNode"/> instance.</returns>
        public static explicit operator DateTimeOffset?(RdnNode? value) => value?.GetValue<DateTimeOffset>();

        /// <summary>
        ///   Defines an explicit conversion of a given <see cref="decimal"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="decimal"/> to explicitly convert.</param>
        /// <returns>A value converted from the <see cref="RdnNode"/> instance.</returns>
        public static explicit operator decimal(RdnNode value) => value.GetValue<decimal>();

        /// <summary>
        ///   Defines an explicit conversion of a given <see cref="decimal"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="decimal"/> to explicitly convert.</param>
        /// <returns>A value converted from the <see cref="RdnNode"/> instance.</returns>
        public static explicit operator decimal?(RdnNode? value) => value?.GetValue<decimal>();

        /// <summary>
        ///   Defines an explicit conversion of a given <see cref="double"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="double"/> to explicitly convert.</param>
        /// <returns>A value converted from the <see cref="RdnNode"/> instance.</returns>
        public static explicit operator double(RdnNode value) => value.GetValue<double>();

        /// <summary>
        ///   Defines an explicit conversion of a given <see cref="double"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="double"/> to explicitly convert.</param>
        /// <returns>A value converted from the <see cref="RdnNode"/> instance.</returns>
        public static explicit operator double?(RdnNode? value) => value?.GetValue<double>();

        /// <summary>
        ///   Defines an explicit conversion of a given <see cref="Guid"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="Guid"/> to explicitly convert.</param>
        /// <returns>A value converted from the <see cref="RdnNode"/> instance.</returns>
        public static explicit operator Guid(RdnNode value) => value.GetValue<Guid>();

        /// <summary>
        ///   Defines an explicit conversion of a given <see cref="Guid"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="Guid"/> to explicitly convert.</param>
        /// <returns>A value converted from the <see cref="RdnNode"/> instance.</returns>
        public static explicit operator Guid?(RdnNode? value) => value?.GetValue<Guid>();

        /// <summary>
        ///   Defines an explicit conversion of a given <see cref="short"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="short"/> to explicitly convert.</param>
        /// <returns>A value converted from the <see cref="RdnNode"/> instance.</returns>
        public static explicit operator short(RdnNode value) => value.GetValue<short>();

        /// <summary>
        ///   Defines an explicit conversion of a given <see cref="short"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="short"/> to explicitly convert.</param>
        /// <returns>A value converted from the <see cref="RdnNode"/> instance.</returns>
        public static explicit operator short?(RdnNode? value) => value?.GetValue<short>();

        /// <summary>
        ///   Defines an explicit conversion of a given <see cref="int"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="int"/> to explicitly convert.</param>
        /// <returns>A value converted from the <see cref="RdnNode"/> instance.</returns>
        public static explicit operator int(RdnNode value) => value.GetValue<int>();

        /// <summary>
        ///   Defines an explicit conversion of a given <see cref="int"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="int"/> to explicitly convert.</param>
        /// <returns>A value converted from the <see cref="RdnNode"/> instance.</returns>
        public static explicit operator int?(RdnNode? value) => value?.GetValue<int>();

        /// <summary>
        ///   Defines an explicit conversion of a given <see cref="long"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="long"/> to explicitly convert.</param>
        /// <returns>A value converted from the <see cref="RdnNode"/> instance.</returns>
        public static explicit operator long(RdnNode value) => value.GetValue<long>();

        /// <summary>
        ///   Defines an explicit conversion of a given <see cref="long"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="long"/> to explicitly convert.</param>
        /// <returns>A value converted from the <see cref="RdnNode"/> instance.</returns>
        public static explicit operator long?(RdnNode? value) => value?.GetValue<long>();

        /// <summary>
        ///   Defines an explicit conversion of a given <see cref="sbyte"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="sbyte"/> to explicitly convert.</param>
        /// <returns>A value converted from the <see cref="RdnNode"/> instance.</returns>
        [System.CLSCompliantAttribute(false)]
        public static explicit operator sbyte(RdnNode value) => value.GetValue<sbyte>();

        /// <summary>
        ///   Defines an explicit conversion of a given <see cref="sbyte"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="sbyte"/> to explicitly convert.</param>
        /// <returns>A value converted from the <see cref="RdnNode"/> instance.</returns>
        [System.CLSCompliantAttribute(false)]
        public static explicit operator sbyte?(RdnNode? value) => value?.GetValue<sbyte>();

        /// <summary>
        ///   Defines an explicit conversion of a given <see cref="float"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="float"/> to explicitly convert.</param>
        /// <returns>A value converted from the <see cref="RdnNode"/> instance.</returns>
        public static explicit operator float(RdnNode value) => value.GetValue<float>();

        /// <summary>
        ///   Defines an explicit conversion of a given <see cref="float"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="float"/> to explicitly convert.</param>
        /// <returns>A value converted from the <see cref="RdnNode"/> instance.</returns>
        public static explicit operator float?(RdnNode? value) => value?.GetValue<float>();

        /// <summary>
        ///   Defines an explicit conversion of a given <see cref="string"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="string"/> to explicitly convert.</param>
        /// <returns>A value converted from the <see cref="RdnNode"/> instance.</returns>
        public static explicit operator string?(RdnNode? value) => value?.GetValue<string>();

        /// <summary>
        ///   Defines an explicit conversion of a given <see cref="ushort"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="ushort"/> to explicitly convert.</param>
        /// <returns>A value converted from the <see cref="RdnNode"/> instance.</returns>
        [System.CLSCompliantAttribute(false)]
        public static explicit operator ushort(RdnNode value) => value.GetValue<ushort>();

        /// <summary>
        ///   Defines an explicit conversion of a given <see cref="ushort"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="ushort"/> to explicitly convert.</param>
        /// <returns>A value converted from the <see cref="RdnNode"/> instance.</returns>
        [System.CLSCompliantAttribute(false)]
        public static explicit operator ushort?(RdnNode? value) => value?.GetValue<ushort>();

        /// <summary>
        ///   Defines an explicit conversion of a given <see cref="uint"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="uint"/> to explicitly convert.</param>
        /// <returns>A value converted from the <see cref="RdnNode"/> instance.</returns>
        [System.CLSCompliantAttribute(false)]
        public static explicit operator uint(RdnNode value) => value.GetValue<uint>();

        /// <summary>
        ///   Defines an explicit conversion of a given <see cref="uint"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="uint"/> to explicitly convert.</param>
        /// <returns>A value converted from the <see cref="RdnNode"/> instance.</returns>
        [System.CLSCompliantAttribute(false)]
        public static explicit operator uint?(RdnNode? value) => value?.GetValue<uint>();

        /// <summary>
        ///   Defines an explicit conversion of a given <see cref="ulong"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="ulong"/> to explicitly convert.</param>
        /// <returns>A value converted from the <see cref="RdnNode"/> instance.</returns>
        [System.CLSCompliantAttribute(false)]
        public static explicit operator ulong(RdnNode value) => value.GetValue<ulong>();

        /// <summary>
        ///   Defines an explicit conversion of a given <see cref="ulong"/> to a <see cref="RdnNode"/>.
        /// </summary>
        /// <param name="value">A <see cref="ulong"/> to explicitly convert.</param>
        /// <returns>A value converted from the <see cref="RdnNode"/> instance.</returns>
        [System.CLSCompliantAttribute(false)]
        public static explicit operator ulong?(RdnNode? value) => value?.GetValue<ulong>();
    }
}
