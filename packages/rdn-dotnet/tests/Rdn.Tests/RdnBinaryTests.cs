using System.Buffers;
using Rdn;
using Rdn.Serialization;
using Xunit;

namespace Rdn.Tests;

public class RdnBinaryTests
{
    // --- Reader Tests ---

    [Fact]
    public void Reader_ParseBase64Binary()
    {
        var reader = new Utf8RdnReader("b\"SGVsbG8=\""u8);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.RdnBinary, reader.TokenType);
        byte[] value = reader.GetRdnBinary();
        Assert.Equal("Hello"u8.ToArray(), value);
    }

    [Fact]
    public void Reader_ParseHexBinary()
    {
        var reader = new Utf8RdnReader("x\"48656C6C6F\""u8);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.RdnBinary, reader.TokenType);
        byte[] value = reader.GetRdnBinary();
        Assert.Equal("Hello"u8.ToArray(), value);
    }

    [Fact]
    public void Reader_ParseEmptyBase64()
    {
        var reader = new Utf8RdnReader("b\"\""u8);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.RdnBinary, reader.TokenType);
        byte[] value = reader.GetRdnBinary();
        Assert.Empty(value);
    }

    [Fact]
    public void Reader_ParseEmptyHex()
    {
        var reader = new Utf8RdnReader("x\"\""u8);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.RdnBinary, reader.TokenType);
        byte[] value = reader.GetRdnBinary();
        Assert.Empty(value);
    }

    [Fact]
    public void Reader_ParseHexLowercase()
    {
        var reader = new Utf8RdnReader("x\"ff00ab\""u8);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.RdnBinary, reader.TokenType);
        byte[] value = reader.GetRdnBinary();
        Assert.Equal(new byte[] { 0xFF, 0x00, 0xAB }, value);
    }

    [Fact]
    public void Reader_ParseBase64WithPadding()
    {
        // "AB" in base64 = "QUI="
        var reader = new Utf8RdnReader("b\"QUI=\""u8);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.RdnBinary, reader.TokenType);
        byte[] value = reader.GetRdnBinary();
        Assert.Equal("AB"u8.ToArray(), value);
    }

    [Fact]
    public void Reader_ParseBase64WithDoublePadding()
    {
        // "A" in base64 = "QQ=="
        var reader = new Utf8RdnReader("b\"QQ==\""u8);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.RdnBinary, reader.TokenType);
        byte[] value = reader.GetRdnBinary();
        Assert.Equal("A"u8.ToArray(), value);
    }

    [Fact]
    public void Reader_InvalidBase64Chars()
    {
        Assert.ThrowsAny<RdnException>(() =>
        {
            var reader = new Utf8RdnReader("b\"not base64!!\""u8);
            reader.Read();
        });
    }

    [Fact]
    public void Reader_InvalidBase64Length()
    {
        // Length not multiple of 4
        Assert.ThrowsAny<RdnException>(() =>
        {
            var reader = new Utf8RdnReader("b\"abc\""u8);
            reader.Read();
        });
    }

    [Fact]
    public void Reader_InvalidHexChars()
    {
        Assert.ThrowsAny<RdnException>(() =>
        {
            var reader = new Utf8RdnReader("x\"GHIJKL\""u8);
            reader.Read();
        });
    }

    [Fact]
    public void Reader_InvalidHexOddLength()
    {
        Assert.ThrowsAny<RdnException>(() =>
        {
            var reader = new Utf8RdnReader("x\"ABC\""u8);
            reader.Read();
        });
    }

    [Fact]
    public void Reader_BinaryInObject()
    {
        byte[] input = """{"data": b"SGVsbG8="}"""u8.ToArray();
        var reader = new Utf8RdnReader(input);
        Assert.True(reader.Read()); // StartObject
        Assert.True(reader.Read()); // PropertyName
        Assert.Equal("data", reader.GetString());
        Assert.True(reader.Read()); // RdnBinary
        Assert.Equal(RdnTokenType.RdnBinary, reader.TokenType);
        Assert.Equal("Hello"u8.ToArray(), reader.GetRdnBinary());
    }

    [Fact]
    public void Reader_BinaryInArray()
    {
        byte[] input = """[b"SGVsbG8=", x"FF00"]"""u8.ToArray();
        var reader = new Utf8RdnReader(input);
        Assert.True(reader.Read()); // StartArray
        Assert.True(reader.Read()); // RdnBinary (base64)
        Assert.Equal(RdnTokenType.RdnBinary, reader.TokenType);
        Assert.Equal("Hello"u8.ToArray(), reader.GetRdnBinary());
        Assert.True(reader.Read()); // RdnBinary (hex)
        Assert.Equal(RdnTokenType.RdnBinary, reader.TokenType);
        Assert.Equal(new byte[] { 0xFF, 0x00 }, reader.GetRdnBinary());
    }

    // --- Writer Tests ---

    [Fact]
    public void Writer_WriteBinaryValue()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8RdnWriter(buffer);
        writer.WriteRdnBinaryValue("Hello"u8);
        writer.Flush();

        string output = System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
        Assert.Equal("b\"SGVsbG8=\"", output);
    }

    [Fact]
    public void Writer_WriteEmptyBinaryValue()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8RdnWriter(buffer);
        writer.WriteRdnBinaryValue(ReadOnlySpan<byte>.Empty);
        writer.Flush();

        string output = System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
        Assert.Equal("b\"\"", output);
    }

    [Fact]
    public void Writer_WriteBinaryProperty()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8RdnWriter(buffer);
        writer.WriteStartObject();
        writer.WriteRdnBinary("data", "Hello"u8);
        writer.WriteEndObject();
        writer.Flush();

        string output = System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
        Assert.Equal("""{"data":b"SGVsbG8="}""", output);
    }

    // --- Document Tests ---

    [Fact]
    public void Document_ParseBase64Binary()
    {
        string rdn = """{"data": b"SGVsbG8="}""";
        using var doc = RdnDocument.Parse(rdn);
        var element = doc.RootElement.GetProperty("data");
        Assert.Equal(RdnValueKind.RdnBinary, element.ValueKind);
        Assert.Equal("Hello"u8.ToArray(), element.GetRdnBinary());
    }

    [Fact]
    public void Document_ParseHexBinary()
    {
        string rdn = """{"data": x"48656C6C6F"}""";
        using var doc = RdnDocument.Parse(rdn);
        var element = doc.RootElement.GetProperty("data");
        Assert.Equal(RdnValueKind.RdnBinary, element.ValueKind);
        Assert.Equal("Hello"u8.ToArray(), element.GetRdnBinary());
    }

    [Fact]
    public void Document_ParseEmptyBinary()
    {
        string rdn = """{"data": b""}""";
        using var doc = RdnDocument.Parse(rdn);
        var element = doc.RootElement.GetProperty("data");
        Assert.Equal(RdnValueKind.RdnBinary, element.ValueKind);
        Assert.Empty(element.GetRdnBinary());
    }

    [Fact]
    public void Document_TryGetBinaryReturnsTrue()
    {
        string rdn = """b"SGVsbG8=" """;
        using var doc = RdnDocument.Parse(rdn);
        Assert.True(doc.RootElement.TryGetRdnBinary(out byte[]? value));
        Assert.Equal("Hello"u8.ToArray(), value);
    }

    // --- Document Roundtrip Tests ---

    [Fact]
    public void Document_Base64Roundtrip()
    {
        string rdn = """{"data":b"SGVsbG8="}""";
        using var doc = RdnDocument.Parse(rdn);

        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8RdnWriter(buffer);
        doc.WriteTo(writer);
        writer.Flush();

        string output = System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
        Assert.Equal("""{"data":b"SGVsbG8="}""", output);
    }

    [Fact]
    public void Document_HexConvertsToBase64OnWrite()
    {
        // Hex input should be written as canonical base64 output
        string rdn = """{"data":x"48656C6C6F"}""";
        using var doc = RdnDocument.Parse(rdn);

        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8RdnWriter(buffer);
        doc.WriteTo(writer);
        writer.Flush();

        string output = System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
        Assert.Equal("""{"data":b"SGVsbG8="}""", output);
    }

    // --- Serialization Tests ---

    public class BinaryModel
    {
        public byte[]? Data { get; set; }
    }

    [Fact]
    public void Serializer_SerializeByteArray()
    {
        var model = new BinaryModel { Data = "Hello"u8.ToArray() };
        string rdn = RdnSerializer.Serialize(model);
        Assert.Contains("b\"SGVsbG8=\"", rdn);
    }

    [Fact]
    public void Serializer_DeserializeBase64Binary()
    {
        string rdn = """{"Data": b"SGVsbG8="}""";
        var model = RdnSerializer.Deserialize<BinaryModel>(rdn);
        Assert.NotNull(model);
        Assert.Equal("Hello"u8.ToArray(), model!.Data);
    }

    [Fact]
    public void Serializer_DeserializeHexBinary()
    {
        string rdn = """{"Data": x"48656C6C6F"}""";
        var model = RdnSerializer.Deserialize<BinaryModel>(rdn);
        Assert.NotNull(model);
        Assert.Equal("Hello"u8.ToArray(), model!.Data);
    }

    [Fact]
    public void Serializer_DeserializeNullBinary()
    {
        string rdn = """{"Data": null}""";
        var model = RdnSerializer.Deserialize<BinaryModel>(rdn);
        Assert.NotNull(model);
        Assert.Null(model!.Data);
    }

    [Fact]
    public void Serializer_RoundtripByteArray()
    {
        var original = new BinaryModel { Data = new byte[] { 0x01, 0x02, 0xFF } };
        string rdn = RdnSerializer.Serialize(original);
        var deserialized = RdnSerializer.Deserialize<BinaryModel>(rdn);
        Assert.NotNull(deserialized);
        Assert.Equal(original.Data, deserialized!.Data);
    }

    [Fact]
    public void Serializer_BackwardsCompatibleStringBase64()
    {
        // Existing JSON-style base64 strings should still work
        string rdn = """{"Data": "SGVsbG8="}""";
        var model = RdnSerializer.Deserialize<BinaryModel>(rdn);
        Assert.NotNull(model);
        Assert.Equal("Hello"u8.ToArray(), model!.Data);
    }

    // --- Memory/ReadOnlyMemory Serialization ---

    public class MemoryModel
    {
        public Memory<byte> Data { get; set; }
    }

    public class ReadOnlyMemoryModel
    {
        public ReadOnlyMemory<byte> Data { get; set; }
    }

    [Fact]
    public void Serializer_MemoryByteRoundtrip()
    {
        var original = new MemoryModel { Data = new byte[] { 0x01, 0x02, 0xFF } };
        string rdn = RdnSerializer.Serialize(original);
        Assert.Contains("b\"", rdn);
        var deserialized = RdnSerializer.Deserialize<MemoryModel>(rdn);
        Assert.NotNull(deserialized);
        Assert.Equal(original.Data.ToArray(), deserialized!.Data.ToArray());
    }

    [Fact]
    public void Serializer_ReadOnlyMemoryByteRoundtrip()
    {
        var original = new ReadOnlyMemoryModel { Data = new byte[] { 0x01, 0x02, 0xFF } };
        string rdn = RdnSerializer.Serialize(original);
        Assert.Contains("b\"", rdn);
        var deserialized = RdnSerializer.Deserialize<ReadOnlyMemoryModel>(rdn);
        Assert.NotNull(deserialized);
        Assert.Equal(original.Data.ToArray(), deserialized!.Data.ToArray());
    }
}
