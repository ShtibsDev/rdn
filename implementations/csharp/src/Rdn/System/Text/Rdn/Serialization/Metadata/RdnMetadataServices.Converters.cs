// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Rdn.Nodes;
using Rdn.Serialization.Converters;

namespace Rdn.Serialization.Metadata
{
    public static partial class RdnMetadataServices
    {
        /// <summary>
        /// Returns a <see cref="RdnConverter{T}"/> instance that converts <see cref="bool"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public static RdnConverter<bool> BooleanConverter => s_booleanConverter ??= new BooleanConverter();
        private static RdnConverter<bool>? s_booleanConverter;

        /// <summary>
        /// Returns a <see cref="RdnConverter{T}"/> instance that converts byte array values.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public static RdnConverter<byte[]?> ByteArrayConverter => s_byteArrayConverter ??= new ByteArrayConverter();
        private static RdnConverter<byte[]?>? s_byteArrayConverter;

        /// <summary>
        /// Returns a <see cref="RdnConverter{T}"/> instance that converts <see cref="byte"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public static RdnConverter<byte> ByteConverter => s_byteConverter ??= new ByteConverter();
        private static RdnConverter<byte>? s_byteConverter;

        /// <summary>
        /// Returns a <see cref="RdnConverter{T}"/> instance that converts <see cref="char"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public static RdnConverter<char> CharConverter => s_charConverter ??= new CharConverter();
        private static RdnConverter<char>? s_charConverter;

        /// <summary>
        /// Returns a <see cref="RdnConverter{T}"/> instance that converts <see cref="DateTime"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public static RdnConverter<DateTime> DateTimeConverter => s_dateTimeConverter ??= new DateTimeConverter();
        private static RdnConverter<DateTime>? s_dateTimeConverter;

        /// <summary>
        /// Returns a <see cref="RdnConverter{T}"/> instance that converts <see cref="DateTimeOffset"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public static RdnConverter<DateTimeOffset> DateTimeOffsetConverter => s_dateTimeOffsetConverter ??= new DateTimeOffsetConverter();
        private static RdnConverter<DateTimeOffset>? s_dateTimeOffsetConverter;

#if NET
        /// <summary>
        /// Returns a <see cref="RdnConverter{T}"/> instance that converts <see cref="DateOnly"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public static RdnConverter<DateOnly> DateOnlyConverter => s_dateOnlyConverter ??= new DateOnlyConverter();
        private static RdnConverter<DateOnly>? s_dateOnlyConverter;

        /// <summary>
        /// Returns a <see cref="RdnConverter{T}"/> instance that converts <see cref="TimeOnly"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public static RdnConverter<TimeOnly> TimeOnlyConverter => s_timeOnlyConverter ??= new TimeOnlyConverter();
        private static RdnConverter<TimeOnly>? s_timeOnlyConverter;
#endif

        /// <summary>
        /// Returns a <see cref="RdnConverter{T}"/> instance that converts <see cref="RdnDuration"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public static RdnConverter<RdnDuration> RdnDurationConverter => s_rdnDurationConverter ??= new RdnDurationConverter();
        private static RdnConverter<RdnDuration>? s_rdnDurationConverter;

        /// <summary>
        /// Returns a <see cref="RdnConverter{T}"/> instance that converts <see cref="System.Text.RegularExpressions.Regex"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public static RdnConverter<System.Text.RegularExpressions.Regex> RegexConverter => s_regexConverter ??= new RegexConverter();
        private static RdnConverter<System.Text.RegularExpressions.Regex>? s_regexConverter;

        /// <summary>
        /// Returns a <see cref="RdnConverter{T}"/> instance that converts <see cref="decimal"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public static RdnConverter<decimal> DecimalConverter => s_decimalConverter ??= new DecimalConverter();
        private static RdnConverter<decimal>? s_decimalConverter;

        /// <summary>
        /// Returns a <see cref="RdnConverter{T}"/> instance that converts <see cref="double"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public static RdnConverter<double> DoubleConverter => s_doubleConverter ??= new DoubleConverter();
        private static RdnConverter<double>? s_doubleConverter;

        /// <summary>
        /// Returns a <see cref="RdnConverter{T}"/> instance that converts <see cref="Guid"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public static RdnConverter<Guid> GuidConverter => s_guidConverter ??= new GuidConverter();
        private static RdnConverter<Guid>? s_guidConverter;

        /// <summary>
        /// Returns a <see cref="RdnConverter{T}"/> instance that converts <see cref="short"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public static RdnConverter<short> Int16Converter => s_int16Converter ??= new Int16Converter();
        private static RdnConverter<short>? s_int16Converter;

        /// <summary>
        /// Returns a <see cref="RdnConverter{T}"/> instance that converts <see cref="int"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public static RdnConverter<int> Int32Converter => s_int32Converter ??= new Int32Converter();
        private static RdnConverter<int>? s_int32Converter;

        /// <summary>
        /// Returns a <see cref="RdnConverter{T}"/> instance that converts <see cref="long"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public static RdnConverter<long> Int64Converter => s_int64Converter ??= new Int64Converter();
        private static RdnConverter<long>? s_int64Converter;

#if NET
        /// <summary>
        /// Returns a <see cref="RdnConverter{T}"/> instance that converts <see cref="Int128"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public static RdnConverter<Int128> Int128Converter => s_int128Converter ??= new Int128Converter();
        private static RdnConverter<Int128>? s_int128Converter;

        /// <summary>
        /// Returns a <see cref="RdnConverter{T}"/> instance that converts <see cref="UInt128"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        [CLSCompliant(false)]
        public static RdnConverter<UInt128> UInt128Converter => s_uint128Converter ??= new UInt128Converter();
        private static RdnConverter<UInt128>? s_uint128Converter;
#endif

        /// <summary>
        /// Returns a <see cref="RdnConverter{T}"/> instance that converts <see cref="RdnArray"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public static RdnConverter<RdnArray?> RdnArrayConverter => s_rdnArrayConverter ??= new RdnArrayConverter();
        private static RdnConverter<RdnArray?>? s_rdnArrayConverter;

        /// <summary>
        /// Returns a <see cref="RdnConverter{T}"/> instance that converts <see cref="RdnElement"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public static RdnConverter<RdnElement> RdnElementConverter => s_rdnElementConverter ??= new RdnElementConverter();
        private static RdnConverter<RdnElement>? s_rdnElementConverter;

        /// <summary>
        /// Returns a <see cref="RdnConverter{T}"/> instance that converts <see cref="RdnNode"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public static RdnConverter<RdnNode?> RdnNodeConverter => s_rdnNodeConverter ??= new RdnNodeConverter();
        private static RdnConverter<RdnNode?>? s_rdnNodeConverter;

        /// <summary>
        /// Returns a <see cref="RdnConverter{T}"/> instance that converts <see cref="RdnObject"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public static RdnConverter<RdnObject?> RdnObjectConverter => s_rdnObjectConverter ??= new RdnObjectConverter();
        private static RdnConverter<RdnObject?>? s_rdnObjectConverter;

        /// <summary>
        /// Returns a <see cref="RdnConverter{T}"/> instance that converts <see cref="RdnArray"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public static RdnConverter<RdnValue?> RdnValueConverter => s_rdnValueConverter ??= new RdnValueConverter();
        private static RdnConverter<RdnValue?>? s_rdnValueConverter;

        /// <summary>
        /// Returns a <see cref="RdnConverter{T}"/> instance that converts <see cref="RdnDocument"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public static RdnConverter<RdnDocument?> RdnDocumentConverter => s_rdnDocumentConverter ??= new RdnDocumentConverter();
        private static RdnConverter<RdnDocument?>? s_rdnDocumentConverter;

        /// <summary>
        /// Returns a <see cref="RdnConverter{T}"/> instance that converts <see cref="Memory{Byte}"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public static RdnConverter<Memory<byte>> MemoryByteConverter => s_memoryByteConverter ??= new MemoryByteConverter();
        private static RdnConverter<Memory<byte>>? s_memoryByteConverter;

        /// <summary>
        /// Returns a <see cref="RdnConverter{T}"/> instance that converts <see cref="ReadOnlyMemory{Byte}"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public static RdnConverter<ReadOnlyMemory<byte>> ReadOnlyMemoryByteConverter => s_readOnlyMemoryByteConverter ??= new ReadOnlyMemoryByteConverter();
        private static RdnConverter<ReadOnlyMemory<byte>>? s_readOnlyMemoryByteConverter;

        /// <summary>
        /// Returns a <see cref="RdnConverter{T}"/> instance that converts <see cref="object"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public static RdnConverter<object?> ObjectConverter => s_objectConverter ??= new DefaultObjectConverter();
        private static RdnConverter<object?>? s_objectConverter;

#if NET
        /// <summary>
        /// Returns a <see cref="RdnConverter{T}"/> instance that converts <see cref="Half"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public static RdnConverter<Half> HalfConverter => s_halfConverter ??= new HalfConverter();
        private static RdnConverter<Half>? s_halfConverter;
#endif

        /// <summary>
        /// Returns a <see cref="RdnConverter{T}"/> instance that converts <see cref="float"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public static RdnConverter<float> SingleConverter => s_singleConverter ??= new SingleConverter();
        private static RdnConverter<float>? s_singleConverter;

        /// <summary>
        /// Returns a <see cref="RdnConverter{T}"/> instance that converts <see cref="sbyte"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        [CLSCompliant(false)]
        public static RdnConverter<sbyte> SByteConverter => s_sbyteConverter ??= new SByteConverter();
        private static RdnConverter<sbyte>? s_sbyteConverter;

        /// <summary>
        /// Returns a <see cref="RdnConverter{T}"/> instance that converts <see cref="string"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public static RdnConverter<string?> StringConverter => s_stringConverter ??= new StringConverter();
        private static RdnConverter<string?>? s_stringConverter;

        /// <summary>
        /// Returns a <see cref="RdnConverter{T}"/> instance that converts <see cref="TimeSpan"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public static RdnConverter<TimeSpan> TimeSpanConverter => s_timeSpanConverter ??= new TimeSpanConverter();
        private static RdnConverter<TimeSpan>? s_timeSpanConverter;

        /// <summary>
        /// Returns a <see cref="RdnConverter{T}"/> instance that converts <see cref="ushort"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        [CLSCompliant(false)]
        public static RdnConverter<ushort> UInt16Converter => s_uint16Converter ??= new UInt16Converter();
        private static RdnConverter<ushort>? s_uint16Converter;

        /// <summary>
        /// Returns a <see cref="RdnConverter{T}"/> instance that converts <see cref="uint"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        [CLSCompliant(false)]
        public static RdnConverter<uint> UInt32Converter => s_uint32Converter ??= new UInt32Converter();
        private static RdnConverter<uint>? s_uint32Converter;

        /// <summary>
        /// Returns a <see cref="RdnConverter{T}"/> instance that converts <see cref="ulong"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        [CLSCompliant(false)]
        public static RdnConverter<ulong> UInt64Converter => s_uint64Converter ??= new UInt64Converter();
        private static RdnConverter<ulong>? s_uint64Converter;

        /// <summary>
        /// Returns a <see cref="RdnConverter{T}"/> instance that converts <see cref="Uri"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public static RdnConverter<Uri?> UriConverter => s_uriConverter ??= new UriConverter();
        private static RdnConverter<Uri?>? s_uriConverter;

        /// <summary>
        /// Returns a <see cref="RdnConverter{T}"/> instance that converts <see cref="Version"/> values.
        /// </summary>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public static RdnConverter<Version?> VersionConverter => s_versionConverter ??= new VersionConverter();
        private static RdnConverter<Version?>? s_versionConverter;

        /// <summary>
        /// Creates a <see cref="RdnConverter{T}"/> instance that throws <see cref="NotSupportedException"/>.
        /// </summary>
        /// <typeparam name="T">The generic definition for the type.</typeparam>
        /// <returns>A <see cref="RdnConverter{T}"/> instance that throws <see cref="NotSupportedException"/></returns>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public static RdnConverter<T> GetUnsupportedTypeConverter<T>()
            => new UnsupportedTypeConverter<T>();

        /// <summary>
        /// Creates a <see cref="RdnConverter{T}"/> instance that converts <typeparamref name="T"/> values.
        /// </summary>
        /// <typeparam name="T">The generic definition for the enum type.</typeparam>
        /// <param name="options">The <see cref="RdnSerializerOptions"/> to use for serialization and deserialization.</param>
        /// <returns>A <see cref="RdnConverter{T}"/> instance that converts <typeparamref name="T"/> values.</returns>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public static RdnConverter<T> GetEnumConverter<T>(RdnSerializerOptions options) where T : struct, Enum
        {
            ArgumentNullException.ThrowIfNull(options);

            return EnumConverterFactory.Helpers.Create<T>(EnumConverterOptions.AllowNumbers, options);
        }

        /// <summary>
        /// Creates a <see cref="RdnConverter{T}"/> instance that converts <typeparamref name="T?"/> values.
        /// </summary>
        /// <typeparam name="T">The generic definition for the underlying nullable type.</typeparam>
        /// <param name="underlyingTypeInfo">Serialization metadata for the underlying nullable type.</param>
        /// <returns>A <see cref="RdnConverter{T}"/> instance that converts <typeparamref name="T?"/> values</returns>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public static RdnConverter<T?> GetNullableConverter<T>(RdnTypeInfo<T> underlyingTypeInfo) where T : struct
        {
            ArgumentNullException.ThrowIfNull(underlyingTypeInfo);

            RdnConverter<T> underlyingConverter = GetTypedConverter<T>(underlyingTypeInfo.Converter);

            return new NullableConverter<T>(underlyingConverter);
        }

        /// <summary>
        /// Creates a <see cref="RdnConverter{T}"/> instance that converts <typeparamref name="T?"/> values.
        /// </summary>
        /// <typeparam name="T">The generic definition for the underlying nullable type.</typeparam>
        /// <param name="options">The <see cref="RdnSerializerOptions"/> to use for serialization and deserialization.</param>
        /// <returns>A <see cref="RdnConverter{T}"/> instance that converts <typeparamref name="T?"/> values</returns>
        /// <remarks>This API is for use by the output of the Rdn source generator and should not be called directly.</remarks>
        public static RdnConverter<T?> GetNullableConverter<T>(RdnSerializerOptions options) where T : struct
        {
            ArgumentNullException.ThrowIfNull(options);

            RdnConverter<T> underlyingConverter = GetTypedConverter<T>(options.GetConverterInternal(typeof(T)));

            return new NullableConverter<T>(underlyingConverter);
        }

        internal static RdnConverter<T> GetTypedConverter<T>(RdnConverter converter)
        {
            RdnConverter<T>? typedConverter = converter as RdnConverter<T>;
            if (typedConverter == null)
            {
                throw new InvalidOperationException(SR.Format(SR.SerializationConverterNotCompatible, typedConverter, typeof(T)));
            }

            return typedConverter;
        }
    }
}
