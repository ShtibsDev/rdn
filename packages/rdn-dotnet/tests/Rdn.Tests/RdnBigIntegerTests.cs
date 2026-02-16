using System.Buffers;
using System.Numerics;
using Rdn;
using Rdn.Nodes;
using Rdn.Serialization;
using Xunit;

namespace Rdn.Tests;

public class RdnBigIntegerTests
{
    // --- Reader Tests ---

    [Fact]
    public void Reader_ParseSimpleBigInteger()
    {
        var reader = new Utf8RdnReader("42n"u8);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.RdnBigInteger, reader.TokenType);
        BigInteger value = reader.GetBigInteger();
        Assert.Equal(new BigInteger(42), value);
    }

    [Fact]
    public void Reader_ParseZeroBigInteger()
    {
        var reader = new Utf8RdnReader("0n"u8);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.RdnBigInteger, reader.TokenType);
        BigInteger value = reader.GetBigInteger();
        Assert.Equal(BigInteger.Zero, value);
    }

    [Fact]
    public void Reader_ParseNegativeBigInteger()
    {
        var reader = new Utf8RdnReader("-42n"u8);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.RdnBigInteger, reader.TokenType);
        BigInteger value = reader.GetBigInteger();
        Assert.Equal(new BigInteger(-42), value);
    }

    [Fact]
    public void Reader_ParseLargeBigInteger()
    {
        byte[] input = System.Text.Encoding.UTF8.GetBytes("99999999999999999999999999999999999999n");
        var reader = new Utf8RdnReader(input);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.RdnBigInteger, reader.TokenType);
        BigInteger value = reader.GetBigInteger();
        Assert.Equal(BigInteger.Parse("99999999999999999999999999999999999999"), value);
    }

    [Fact]
    public void Reader_ParseLargeNegativeBigInteger()
    {
        byte[] input = System.Text.Encoding.UTF8.GetBytes("-123456789012345678901234567890n");
        var reader = new Utf8RdnReader(input);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.RdnBigInteger, reader.TokenType);
        BigInteger value = reader.GetBigInteger();
        Assert.Equal(BigInteger.Parse("-123456789012345678901234567890"), value);
    }

    [Fact]
    public void Reader_ParseBigIntegerInArray()
    {
        byte[] input = "[42n, 0n, -7n]"u8.ToArray();
        var reader = new Utf8RdnReader(input);
        Assert.True(reader.Read()); // StartArray
        Assert.True(reader.Read()); // 42n
        Assert.Equal(RdnTokenType.RdnBigInteger, reader.TokenType);
        Assert.Equal(new BigInteger(42), reader.GetBigInteger());
        Assert.True(reader.Read()); // 0n
        Assert.Equal(RdnTokenType.RdnBigInteger, reader.TokenType);
        Assert.Equal(BigInteger.Zero, reader.GetBigInteger());
        Assert.True(reader.Read()); // -7n
        Assert.Equal(RdnTokenType.RdnBigInteger, reader.TokenType);
        Assert.Equal(new BigInteger(-7), reader.GetBigInteger());
    }

    [Fact]
    public void Reader_ParseBigIntegerInObject()
    {
        byte[] input = """{"count": 42n}"""u8.ToArray();
        var reader = new Utf8RdnReader(input);
        Assert.True(reader.Read()); // StartObject
        Assert.True(reader.Read()); // PropertyName
        Assert.Equal("count", reader.GetString());
        Assert.True(reader.Read()); // 42n
        Assert.Equal(RdnTokenType.RdnBigInteger, reader.TokenType);
        Assert.Equal(new BigInteger(42), reader.GetBigInteger());
    }

    [Fact]
    public void Reader_RegularNumberNotBigInteger()
    {
        var reader = new Utf8RdnReader("42"u8);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.Number, reader.TokenType);
    }

    [Fact]
    public void Reader_TryGetBigInteger()
    {
        var reader = new Utf8RdnReader("42n"u8);
        Assert.True(reader.Read());
        Assert.True(reader.TryGetBigInteger(out BigInteger value));
        Assert.Equal(new BigInteger(42), value);
    }

    // --- Writer Tests ---

    [Fact]
    public void Writer_WriteBigIntegerValue()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8RdnWriter(buffer);
        writer.WriteBigIntegerValue(new BigInteger(42));
        writer.Flush();

        string output = System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
        Assert.Equal("42n", output);
    }

    [Fact]
    public void Writer_WriteZeroBigInteger()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8RdnWriter(buffer);
        writer.WriteBigIntegerValue(BigInteger.Zero);
        writer.Flush();

        string output = System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
        Assert.Equal("0n", output);
    }

    [Fact]
    public void Writer_WriteNegativeBigInteger()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8RdnWriter(buffer);
        writer.WriteBigIntegerValue(new BigInteger(-42));
        writer.Flush();

        string output = System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
        Assert.Equal("-42n", output);
    }

    [Fact]
    public void Writer_WriteLargeBigInteger()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8RdnWriter(buffer);
        BigInteger large = BigInteger.Parse("99999999999999999999999999999999999999");
        writer.WriteBigIntegerValue(large);
        writer.Flush();

        string output = System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
        Assert.Equal("99999999999999999999999999999999999999n", output);
    }

    [Fact]
    public void Writer_WriteBigIntegerProperty()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8RdnWriter(buffer);
        writer.WriteStartObject();
        writer.WriteBigInteger("count", new BigInteger(42));
        writer.WriteEndObject();
        writer.Flush();

        string output = System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
        Assert.Equal("""{"count":42n}""", output);
    }

    [Fact]
    public void Writer_WriteBigIntegerInArray()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8RdnWriter(buffer);
        writer.WriteStartArray();
        writer.WriteBigIntegerValue(new BigInteger(42));
        writer.WriteBigIntegerValue(BigInteger.Zero);
        writer.WriteBigIntegerValue(new BigInteger(-7));
        writer.WriteEndArray();
        writer.Flush();

        string output = System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
        Assert.Equal("[42n,0n,-7n]", output);
    }

    // --- Document Tests ---

    [Fact]
    public void Document_ParseBigInteger()
    {
        string rdn = """{"count": 42n}""";
        using var doc = RdnDocument.Parse(rdn);
        var element = doc.RootElement.GetProperty("count");
        Assert.Equal(RdnValueKind.RdnBigInteger, element.ValueKind);
        Assert.Equal(new BigInteger(42), element.GetBigInteger());
    }

    [Fact]
    public void Document_TryGetBigIntegerReturnsTrue()
    {
        string rdn = "42n";
        using var doc = RdnDocument.Parse(rdn);
        Assert.True(doc.RootElement.TryGetBigInteger(out BigInteger value));
        Assert.Equal(new BigInteger(42), value);
    }

    [Fact]
    public void Document_TryGetBigIntegerReturnsFalseForNumber()
    {
        string rdn = "42";
        using var doc = RdnDocument.Parse(rdn);
        Assert.False(doc.RootElement.TryGetBigInteger(out _));
    }

    [Fact]
    public void Document_BigIntegerToString()
    {
        string rdn = "42n";
        using var doc = RdnDocument.Parse(rdn);
        Assert.Equal("42n", doc.RootElement.ToString());
    }

    [Fact]
    public void Document_NegativeBigIntegerToString()
    {
        string rdn = "-42n";
        using var doc = RdnDocument.Parse(rdn);
        Assert.Equal("-42n", doc.RootElement.ToString());
    }

    // --- Document Roundtrip Tests ---

    [Fact]
    public void Document_BigIntegerRoundtrip()
    {
        string rdn = """{"count":42n}""";
        using var doc = RdnDocument.Parse(rdn);

        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8RdnWriter(buffer);
        doc.WriteTo(writer);
        writer.Flush();

        string output = System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
        Assert.Equal("""{"count":42n}""", output);
    }

    [Fact]
    public void Document_BigIntegerArrayRoundtrip()
    {
        string rdn = "[42n,0n,-7n]";
        using var doc = RdnDocument.Parse(rdn);

        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8RdnWriter(buffer);
        doc.WriteTo(writer);
        writer.Flush();

        string output = System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
        Assert.Equal("[42n,0n,-7n]", output);
    }

    [Fact]
    public void Document_LargeBigIntegerRoundtrip()
    {
        string rdn = "99999999999999999999999999999999999999n";
        using var doc = RdnDocument.Parse(rdn);

        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8RdnWriter(buffer);
        doc.WriteTo(writer);
        writer.Flush();

        string output = System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
        Assert.Equal("99999999999999999999999999999999999999n", output);
    }

    // --- DeepEquals Tests ---

    [Fact]
    public void Document_BigIntegerDeepEquals()
    {
        using var doc1 = RdnDocument.Parse("42n");
        using var doc2 = RdnDocument.Parse("42n");
        Assert.True(RdnElement.DeepEquals(doc1.RootElement, doc2.RootElement));
    }

    [Fact]
    public void Document_BigIntegerDeepNotEquals()
    {
        using var doc1 = RdnDocument.Parse("42n");
        using var doc2 = RdnDocument.Parse("43n");
        Assert.False(RdnElement.DeepEquals(doc1.RootElement, doc2.RootElement));
    }

    [Fact]
    public void Document_BigIntegerNotEqualToNumber()
    {
        using var doc1 = RdnDocument.Parse("42n");
        using var doc2 = RdnDocument.Parse("42");
        Assert.False(RdnElement.DeepEquals(doc1.RootElement, doc2.RootElement));
    }

    // --- Serialization Tests ---

    public class BigIntegerModel
    {
        public BigInteger Value { get; set; }
    }

    [Fact]
    public void Serializer_SerializeBigInteger()
    {
        var model = new BigIntegerModel { Value = new BigInteger(42) };
        string rdn = RdnSerializer.Serialize(model);
        Assert.Contains("42n", rdn);
    }

    [Fact]
    public void Serializer_DeserializeBigInteger()
    {
        string rdn = """{"Value": 42n}""";
        var model = RdnSerializer.Deserialize<BigIntegerModel>(rdn);
        Assert.NotNull(model);
        Assert.Equal(new BigInteger(42), model!.Value);
    }

    [Fact]
    public void Serializer_DeserializeBigIntegerFromNumber()
    {
        // Regular numbers should also deserialize to BigInteger
        string rdn = """{"Value": 42}""";
        var model = RdnSerializer.Deserialize<BigIntegerModel>(rdn);
        Assert.NotNull(model);
        Assert.Equal(new BigInteger(42), model!.Value);
    }

    [Fact]
    public void Serializer_RoundtripBigInteger()
    {
        var original = new BigIntegerModel { Value = BigInteger.Parse("99999999999999999999999999999999999999") };
        string rdn = RdnSerializer.Serialize(original);
        var deserialized = RdnSerializer.Deserialize<BigIntegerModel>(rdn);
        Assert.NotNull(deserialized);
        Assert.Equal(original.Value, deserialized!.Value);
    }

    [Fact]
    public void Serializer_RoundtripNegativeBigInteger()
    {
        var original = new BigIntegerModel { Value = new BigInteger(-42) };
        string rdn = RdnSerializer.Serialize(original);
        var deserialized = RdnSerializer.Deserialize<BigIntegerModel>(rdn);
        Assert.NotNull(deserialized);
        Assert.Equal(original.Value, deserialized!.Value);
    }

    // --- Mutable DOM Tests ---

    [Fact]
    public void MutableDom_CreateBigIntegerValue()
    {
        var value = RdnValue.Create(new BigInteger(42));
        Assert.NotNull(value);
    }

    [Fact]
    public void MutableDom_CreateNullableBigIntegerValue()
    {
        BigInteger? nullValue = null;
        var value = RdnValue.Create(nullValue);
        Assert.Null(value);

        BigInteger? nonNullValue = new BigInteger(42);
        var value2 = RdnValue.Create(nonNullValue);
        Assert.NotNull(value2);
    }
}
