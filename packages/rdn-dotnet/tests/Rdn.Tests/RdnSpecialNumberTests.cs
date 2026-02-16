using Rdn;
using Rdn.Nodes;
using Rdn.Serialization;
using Xunit;

namespace Rdn.Tests;

public class RdnSpecialNumberTests
{
    #region 1. Utf8RdnReader — standalone parsing

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public void Reader_SpecialNumber_Standalone(string input)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.Number, reader.TokenType);
        Assert.False(reader.Read());
    }

    [Fact]
    public void Reader_NaN_GetDouble()
    {
        var reader = new Utf8RdnReader("NaN"u8.ToArray());
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.Number, reader.TokenType);
        Assert.True(double.IsNaN(reader.GetDouble()));
    }

    [Fact]
    public void Reader_Infinity_GetDouble()
    {
        var reader = new Utf8RdnReader("Infinity"u8.ToArray());
        Assert.True(reader.Read());
        Assert.True(double.IsPositiveInfinity(reader.GetDouble()));
    }

    [Fact]
    public void Reader_NegativeInfinity_GetDouble()
    {
        var reader = new Utf8RdnReader("-Infinity"u8.ToArray());
        Assert.True(reader.Read());
        Assert.True(double.IsNegativeInfinity(reader.GetDouble()));
    }

    [Fact]
    public void Reader_NaN_GetSingle()
    {
        var reader = new Utf8RdnReader("NaN"u8.ToArray());
        Assert.True(reader.Read());
        Assert.True(float.IsNaN(reader.GetSingle()));
    }

    [Fact]
    public void Reader_Infinity_GetSingle()
    {
        var reader = new Utf8RdnReader("Infinity"u8.ToArray());
        Assert.True(reader.Read());
        Assert.True(float.IsPositiveInfinity(reader.GetSingle()));
    }

    [Fact]
    public void Reader_NegativeInfinity_GetSingle()
    {
        var reader = new Utf8RdnReader("-Infinity"u8.ToArray());
        Assert.True(reader.Read());
        Assert.True(float.IsNegativeInfinity(reader.GetSingle()));
    }

    #endregion

    #region 2. Utf8RdnReader — in objects and arrays

    [Fact]
    public void Reader_SpecialNumbers_InObject()
    {
        var bytes = """{"nan": NaN, "inf": Infinity, "negInf": -Infinity}"""u8.ToArray();
        var reader = new Utf8RdnReader(bytes);

        Assert.True(reader.Read()); // StartObject
        Assert.Equal(RdnTokenType.StartObject, reader.TokenType);

        Assert.True(reader.Read()); // "nan"
        Assert.Equal("nan", reader.GetString());
        Assert.True(reader.Read()); // NaN
        Assert.Equal(RdnTokenType.Number, reader.TokenType);
        Assert.True(double.IsNaN(reader.GetDouble()));

        Assert.True(reader.Read()); // "inf"
        Assert.Equal("inf", reader.GetString());
        Assert.True(reader.Read()); // Infinity
        Assert.True(double.IsPositiveInfinity(reader.GetDouble()));

        Assert.True(reader.Read()); // "negInf"
        Assert.Equal("negInf", reader.GetString());
        Assert.True(reader.Read()); // -Infinity
        Assert.True(double.IsNegativeInfinity(reader.GetDouble()));

        Assert.True(reader.Read()); // EndObject
        Assert.Equal(RdnTokenType.EndObject, reader.TokenType);
    }

    [Fact]
    public void Reader_SpecialNumbers_InArray()
    {
        var bytes = "[NaN, Infinity, -Infinity, 42]"u8.ToArray();
        var reader = new Utf8RdnReader(bytes);

        Assert.True(reader.Read()); // StartArray
        Assert.True(reader.Read());
        Assert.True(double.IsNaN(reader.GetDouble()));
        Assert.True(reader.Read());
        Assert.True(double.IsPositiveInfinity(reader.GetDouble()));
        Assert.True(reader.Read());
        Assert.True(double.IsNegativeInfinity(reader.GetDouble()));
        Assert.True(reader.Read());
        Assert.Equal(42, reader.GetInt32());
        Assert.True(reader.Read()); // EndArray
    }

    #endregion

    #region 3. Utf8RdnWriter — bare literal output

    [Fact]
    public void Writer_Double_NaN()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8RdnWriter(stream))
        {
            writer.WriteNumberValue(double.NaN);
        }
        Assert.Equal("NaN", System.Text.Encoding.UTF8.GetString(stream.ToArray()));
    }

    [Fact]
    public void Writer_Double_Infinity()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8RdnWriter(stream))
        {
            writer.WriteNumberValue(double.PositiveInfinity);
        }
        Assert.Equal("Infinity", System.Text.Encoding.UTF8.GetString(stream.ToArray()));
    }

    [Fact]
    public void Writer_Double_NegativeInfinity()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8RdnWriter(stream))
        {
            writer.WriteNumberValue(double.NegativeInfinity);
        }
        Assert.Equal("-Infinity", System.Text.Encoding.UTF8.GetString(stream.ToArray()));
    }

    [Fact]
    public void Writer_Float_NaN()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8RdnWriter(stream))
        {
            writer.WriteNumberValue(float.NaN);
        }
        Assert.Equal("NaN", System.Text.Encoding.UTF8.GetString(stream.ToArray()));
    }

    [Fact]
    public void Writer_Float_Infinity()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8RdnWriter(stream))
        {
            writer.WriteNumberValue(float.PositiveInfinity);
        }
        Assert.Equal("Infinity", System.Text.Encoding.UTF8.GetString(stream.ToArray()));
    }

    [Fact]
    public void Writer_Float_NegativeInfinity()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8RdnWriter(stream))
        {
            writer.WriteNumberValue(float.NegativeInfinity);
        }
        Assert.Equal("-Infinity", System.Text.Encoding.UTF8.GetString(stream.ToArray()));
    }

    [Fact]
    public void Writer_SpecialNumbers_InObject()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8RdnWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("nan", double.NaN);
            writer.WriteNumber("inf", double.PositiveInfinity);
            writer.WriteNumber("negInf", double.NegativeInfinity);
            writer.WriteEndObject();
        }
        var output = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Equal("""{"nan":NaN,"inf":Infinity,"negInf":-Infinity}""", output);
    }

    #endregion

    #region 4. RdnDocument — parse + GetDouble, WriteTo roundtrip

    [Fact]
    public void Document_SpecialNumbers_GetDouble()
    {
        using var doc = RdnDocument.Parse("""{"nan": NaN, "inf": Infinity, "negInf": -Infinity}""");
        var root = doc.RootElement;

        Assert.True(double.IsNaN(root.GetProperty("nan").GetDouble()));
        Assert.True(double.IsPositiveInfinity(root.GetProperty("inf").GetDouble()));
        Assert.True(double.IsNegativeInfinity(root.GetProperty("negInf").GetDouble()));
    }

    [Fact]
    public void Document_SpecialNumbers_WriteTo_Roundtrip()
    {
        var input = """{"nan":NaN,"inf":Infinity,"negInf":-Infinity}""";
        using var doc = RdnDocument.Parse(input);

        using var stream = new MemoryStream();
        using (var writer = new Utf8RdnWriter(stream))
        {
            doc.WriteTo(writer);
        }

        var output = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Equal(input, output);
    }

    #endregion

    #region 5. Brace disambiguation — NaN/Infinity in Sets and Maps

    [Fact]
    public void Reader_NaN_InImplicitSet()
    {
        var bytes = "{NaN, 1}"u8.ToArray();
        var reader = new Utf8RdnReader(bytes);

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartSet, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.Number, reader.TokenType);
        Assert.True(double.IsNaN(reader.GetDouble()));

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.Number, reader.TokenType);
        Assert.Equal(1, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndSet, reader.TokenType);
    }

    [Fact]
    public void Reader_NaN_InImplicitMap()
    {
        var bytes = "{NaN => 1}"u8.ToArray();
        var reader = new Utf8RdnReader(bytes);

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartMap, reader.TokenType);

        Assert.True(reader.Read()); // NaN (key)
        Assert.Equal(RdnTokenType.Number, reader.TokenType);
        Assert.True(double.IsNaN(reader.GetDouble()));

        Assert.True(reader.Read()); // 1 (value)
        Assert.Equal(RdnTokenType.Number, reader.TokenType);
        Assert.Equal(1, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndMap, reader.TokenType);
    }

    [Fact]
    public void Reader_NegativeInfinity_InImplicitSet()
    {
        var bytes = "{-Infinity, 1}"u8.ToArray();
        var reader = new Utf8RdnReader(bytes);

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartSet, reader.TokenType);

        Assert.True(reader.Read());
        Assert.True(double.IsNegativeInfinity(reader.GetDouble()));

        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndSet, reader.TokenType);
    }

    [Fact]
    public void Reader_Infinity_InImplicitSet()
    {
        var bytes = "{Infinity, 1}"u8.ToArray();
        var reader = new Utf8RdnReader(bytes);

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartSet, reader.TokenType);

        Assert.True(reader.Read());
        Assert.True(double.IsPositiveInfinity(reader.GetDouble()));

        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndSet, reader.TokenType);
    }

    #endregion

    #region 6. Error cases — invalid variants must throw

    [Theory]
    [InlineData("nan")]
    [InlineData("INFINITY")]
    [InlineData("+Infinity")]
    [InlineData("-NaN")]
    [InlineData("Inf")]
    [InlineData("NAN")]
    [InlineData("infinity")]
    public void Reader_InvalidVariants_Throw(string input)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        Assert.ThrowsAny<RdnException>(() =>
        {
            var reader = new Utf8RdnReader(bytes);
            reader.Read();
        });
    }

    #endregion

    #region 7. RdnSerializer — serialize/deserialize

    [Fact]
    public void Serializer_Double_NaN_Roundtrip()
    {
        string rdn = RdnSerializer.Serialize(double.NaN);
        Assert.Equal("NaN", rdn);
        double result = RdnSerializer.Deserialize<double>(rdn);
        Assert.True(double.IsNaN(result));
    }

    [Fact]
    public void Serializer_Double_Infinity_Roundtrip()
    {
        string rdn = RdnSerializer.Serialize(double.PositiveInfinity);
        Assert.Equal("Infinity", rdn);
        double result = RdnSerializer.Deserialize<double>(rdn);
        Assert.True(double.IsPositiveInfinity(result));
    }

    [Fact]
    public void Serializer_Double_NegativeInfinity_Roundtrip()
    {
        string rdn = RdnSerializer.Serialize(double.NegativeInfinity);
        Assert.Equal("-Infinity", rdn);
        double result = RdnSerializer.Deserialize<double>(rdn);
        Assert.True(double.IsNegativeInfinity(result));
    }

    [Fact]
    public void Serializer_Float_NaN_Roundtrip()
    {
        string rdn = RdnSerializer.Serialize(float.NaN);
        Assert.Equal("NaN", rdn);
        float result = RdnSerializer.Deserialize<float>(rdn);
        Assert.True(float.IsNaN(result));
    }

    [Fact]
    public void Serializer_ObjectWithSpecialNumbers()
    {
        var input = new { nan = double.NaN, inf = double.PositiveInfinity, negInf = double.NegativeInfinity };
        string rdn = RdnSerializer.Serialize(input);
        Assert.Contains("NaN", rdn);
        Assert.Contains("Infinity", rdn);
        Assert.Contains("-Infinity", rdn);
    }

    #endregion

    #region 8. Parse → Serialize → Parse roundtrip identity

    [Fact]
    public void Roundtrip_ParseSerializeParse()
    {
        var input = """{"nan":NaN,"inf":Infinity,"negInf":-Infinity}""";

        // Parse
        using var doc1 = RdnDocument.Parse(input);
        Assert.True(double.IsNaN(doc1.RootElement.GetProperty("nan").GetDouble()));

        // Serialize
        using var stream = new MemoryStream();
        using (var writer = new Utf8RdnWriter(stream))
        {
            doc1.WriteTo(writer);
        }
        var serialized = System.Text.Encoding.UTF8.GetString(stream.ToArray());

        // Parse again
        using var doc2 = RdnDocument.Parse(serialized);
        Assert.True(double.IsNaN(doc2.RootElement.GetProperty("nan").GetDouble()));
        Assert.True(double.IsPositiveInfinity(doc2.RootElement.GetProperty("inf").GetDouble()));
        Assert.True(double.IsNegativeInfinity(doc2.RootElement.GetProperty("negInf").GetDouble()));
    }

    #endregion
}
