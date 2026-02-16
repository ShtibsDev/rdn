// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Rdn.Serialization.Metadata;

namespace Rdn.Serialization.Converters
{
    internal sealed class TupleConverter<TTuple, T1> : RdnConverter<TTuple>
    {
        public override TTuple? Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            if (reader.TokenType != RdnTokenType.StartArray)
                throw new RdnException($"Expected StartArray for tuple, got {reader.TokenType}");

            reader.Read();
            T1? v1 = RdnSerializer.Deserialize<T1>(ref reader, options);
            reader.Read(); // EndArray

            if (TupleConverterFactory.IsValueTupleType(typeToConvert))
                return (TTuple)(object)ValueTuple.Create(v1!);
            return (TTuple)(object)Tuple.Create(v1!);
        }

        public override void Write(Utf8RdnWriter writer, TTuple value, RdnSerializerOptions options)
        {
            writer.WriteStartTuple();
            if (TupleConverterFactory.IsValueTupleType(typeof(TTuple)))
            {
                var vt = (ITuple)value!;
                RdnSerializer.Serialize(writer, (T1?)vt[0], options);
            }
            else
            {
                var rt = (ITuple)value!;
                RdnSerializer.Serialize(writer, (T1?)rt[0], options);
            }
            writer.WriteEndTuple();
        }
    }

    internal sealed class TupleConverter<TTuple, T1, T2> : RdnConverter<TTuple>
    {
        public override TTuple? Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            if (reader.TokenType != RdnTokenType.StartArray)
                throw new RdnException($"Expected StartArray for tuple, got {reader.TokenType}");

            reader.Read();
            T1? v1 = RdnSerializer.Deserialize<T1>(ref reader, options);
            reader.Read();
            T2? v2 = RdnSerializer.Deserialize<T2>(ref reader, options);
            reader.Read(); // EndArray

            if (TupleConverterFactory.IsValueTupleType(typeToConvert))
                return (TTuple)(object)(v1!, v2!);
            return (TTuple)(object)Tuple.Create(v1!, v2!);
        }

        public override void Write(Utf8RdnWriter writer, TTuple value, RdnSerializerOptions options)
        {
            var t = (ITuple)value!;
            writer.WriteStartTuple();
            RdnSerializer.Serialize(writer, (T1?)t[0], options);
            RdnSerializer.Serialize(writer, (T2?)t[1], options);
            writer.WriteEndTuple();
        }
    }

    internal sealed class TupleConverter<TTuple, T1, T2, T3> : RdnConverter<TTuple>
    {
        public override TTuple? Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            if (reader.TokenType != RdnTokenType.StartArray)
                throw new RdnException($"Expected StartArray for tuple, got {reader.TokenType}");

            reader.Read();
            T1? v1 = RdnSerializer.Deserialize<T1>(ref reader, options);
            reader.Read();
            T2? v2 = RdnSerializer.Deserialize<T2>(ref reader, options);
            reader.Read();
            T3? v3 = RdnSerializer.Deserialize<T3>(ref reader, options);
            reader.Read(); // EndArray

            if (TupleConverterFactory.IsValueTupleType(typeToConvert))
                return (TTuple)(object)(v1!, v2!, v3!);
            return (TTuple)(object)Tuple.Create(v1!, v2!, v3!);
        }

        public override void Write(Utf8RdnWriter writer, TTuple value, RdnSerializerOptions options)
        {
            var t = (ITuple)value!;
            writer.WriteStartTuple();
            RdnSerializer.Serialize(writer, (T1?)t[0], options);
            RdnSerializer.Serialize(writer, (T2?)t[1], options);
            RdnSerializer.Serialize(writer, (T3?)t[2], options);
            writer.WriteEndTuple();
        }
    }

    internal sealed class TupleConverter<TTuple, T1, T2, T3, T4> : RdnConverter<TTuple>
    {
        public override TTuple? Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            if (reader.TokenType != RdnTokenType.StartArray)
                throw new RdnException($"Expected StartArray for tuple, got {reader.TokenType}");

            reader.Read();
            T1? v1 = RdnSerializer.Deserialize<T1>(ref reader, options);
            reader.Read();
            T2? v2 = RdnSerializer.Deserialize<T2>(ref reader, options);
            reader.Read();
            T3? v3 = RdnSerializer.Deserialize<T3>(ref reader, options);
            reader.Read();
            T4? v4 = RdnSerializer.Deserialize<T4>(ref reader, options);
            reader.Read(); // EndArray

            if (TupleConverterFactory.IsValueTupleType(typeToConvert))
                return (TTuple)(object)(v1!, v2!, v3!, v4!);
            return (TTuple)(object)Tuple.Create(v1!, v2!, v3!, v4!);
        }

        public override void Write(Utf8RdnWriter writer, TTuple value, RdnSerializerOptions options)
        {
            var t = (ITuple)value!;
            writer.WriteStartTuple();
            RdnSerializer.Serialize(writer, (T1?)t[0], options);
            RdnSerializer.Serialize(writer, (T2?)t[1], options);
            RdnSerializer.Serialize(writer, (T3?)t[2], options);
            RdnSerializer.Serialize(writer, (T4?)t[3], options);
            writer.WriteEndTuple();
        }
    }

    internal sealed class TupleConverter<TTuple, T1, T2, T3, T4, T5> : RdnConverter<TTuple>
    {
        public override TTuple? Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            if (reader.TokenType != RdnTokenType.StartArray)
                throw new RdnException($"Expected StartArray for tuple, got {reader.TokenType}");

            reader.Read();
            T1? v1 = RdnSerializer.Deserialize<T1>(ref reader, options);
            reader.Read();
            T2? v2 = RdnSerializer.Deserialize<T2>(ref reader, options);
            reader.Read();
            T3? v3 = RdnSerializer.Deserialize<T3>(ref reader, options);
            reader.Read();
            T4? v4 = RdnSerializer.Deserialize<T4>(ref reader, options);
            reader.Read();
            T5? v5 = RdnSerializer.Deserialize<T5>(ref reader, options);
            reader.Read(); // EndArray

            if (TupleConverterFactory.IsValueTupleType(typeToConvert))
                return (TTuple)(object)(v1!, v2!, v3!, v4!, v5!);
            return (TTuple)(object)Tuple.Create(v1!, v2!, v3!, v4!, v5!);
        }

        public override void Write(Utf8RdnWriter writer, TTuple value, RdnSerializerOptions options)
        {
            var t = (ITuple)value!;
            writer.WriteStartTuple();
            RdnSerializer.Serialize(writer, (T1?)t[0], options);
            RdnSerializer.Serialize(writer, (T2?)t[1], options);
            RdnSerializer.Serialize(writer, (T3?)t[2], options);
            RdnSerializer.Serialize(writer, (T4?)t[3], options);
            RdnSerializer.Serialize(writer, (T5?)t[4], options);
            writer.WriteEndTuple();
        }
    }

    internal sealed class TupleConverter<TTuple, T1, T2, T3, T4, T5, T6> : RdnConverter<TTuple>
    {
        public override TTuple? Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            if (reader.TokenType != RdnTokenType.StartArray)
                throw new RdnException($"Expected StartArray for tuple, got {reader.TokenType}");

            reader.Read();
            T1? v1 = RdnSerializer.Deserialize<T1>(ref reader, options);
            reader.Read();
            T2? v2 = RdnSerializer.Deserialize<T2>(ref reader, options);
            reader.Read();
            T3? v3 = RdnSerializer.Deserialize<T3>(ref reader, options);
            reader.Read();
            T4? v4 = RdnSerializer.Deserialize<T4>(ref reader, options);
            reader.Read();
            T5? v5 = RdnSerializer.Deserialize<T5>(ref reader, options);
            reader.Read();
            T6? v6 = RdnSerializer.Deserialize<T6>(ref reader, options);
            reader.Read(); // EndArray

            if (TupleConverterFactory.IsValueTupleType(typeToConvert))
                return (TTuple)(object)(v1!, v2!, v3!, v4!, v5!, v6!);
            return (TTuple)(object)Tuple.Create(v1!, v2!, v3!, v4!, v5!, v6!);
        }

        public override void Write(Utf8RdnWriter writer, TTuple value, RdnSerializerOptions options)
        {
            var t = (ITuple)value!;
            writer.WriteStartTuple();
            RdnSerializer.Serialize(writer, (T1?)t[0], options);
            RdnSerializer.Serialize(writer, (T2?)t[1], options);
            RdnSerializer.Serialize(writer, (T3?)t[2], options);
            RdnSerializer.Serialize(writer, (T4?)t[3], options);
            RdnSerializer.Serialize(writer, (T5?)t[4], options);
            RdnSerializer.Serialize(writer, (T6?)t[5], options);
            writer.WriteEndTuple();
        }
    }

    internal sealed class TupleConverter<TTuple, T1, T2, T3, T4, T5, T6, T7> : RdnConverter<TTuple>
    {
        public override TTuple? Read(ref Utf8RdnReader reader, Type typeToConvert, RdnSerializerOptions options)
        {
            if (reader.TokenType != RdnTokenType.StartArray)
                throw new RdnException($"Expected StartArray for tuple, got {reader.TokenType}");

            reader.Read();
            T1? v1 = RdnSerializer.Deserialize<T1>(ref reader, options);
            reader.Read();
            T2? v2 = RdnSerializer.Deserialize<T2>(ref reader, options);
            reader.Read();
            T3? v3 = RdnSerializer.Deserialize<T3>(ref reader, options);
            reader.Read();
            T4? v4 = RdnSerializer.Deserialize<T4>(ref reader, options);
            reader.Read();
            T5? v5 = RdnSerializer.Deserialize<T5>(ref reader, options);
            reader.Read();
            T6? v6 = RdnSerializer.Deserialize<T6>(ref reader, options);
            reader.Read();
            T7? v7 = RdnSerializer.Deserialize<T7>(ref reader, options);
            reader.Read(); // EndArray

            if (TupleConverterFactory.IsValueTupleType(typeToConvert))
                return (TTuple)(object)(v1!, v2!, v3!, v4!, v5!, v6!, v7!);
            return (TTuple)(object)Tuple.Create(v1!, v2!, v3!, v4!, v5!, v6!, v7!);
        }

        public override void Write(Utf8RdnWriter writer, TTuple value, RdnSerializerOptions options)
        {
            var t = (ITuple)value!;
            writer.WriteStartTuple();
            RdnSerializer.Serialize(writer, (T1?)t[0], options);
            RdnSerializer.Serialize(writer, (T2?)t[1], options);
            RdnSerializer.Serialize(writer, (T3?)t[2], options);
            RdnSerializer.Serialize(writer, (T4?)t[3], options);
            RdnSerializer.Serialize(writer, (T5?)t[4], options);
            RdnSerializer.Serialize(writer, (T6?)t[5], options);
            RdnSerializer.Serialize(writer, (T7?)t[6], options);
            writer.WriteEndTuple();
        }
    }
}
