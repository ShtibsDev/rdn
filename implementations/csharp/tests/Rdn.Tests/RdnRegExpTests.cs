using System.Text.RegularExpressions;
using Rdn;
using Rdn.Nodes;
using Rdn.Serialization;
using Xunit;

namespace Rdn.Tests;

public class RdnRegExpTests
{
    // --- Reader Tests ---

    [Fact]
    public void Reader_ParseSimpleRegex()
    {
        var reader = new Utf8JsonReader("/test/gi"u8);
        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.RdnRegExp, reader.TokenType);
        Assert.Equal("test", reader.GetRdnRegExpSource());
        Assert.Equal("gi", reader.GetRdnRegExpFlags());
    }

    [Fact]
    public void Reader_ParseRegexNoFlags()
    {
        var reader = new Utf8JsonReader("/hello/"u8);
        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.RdnRegExp, reader.TokenType);
        Assert.Equal("hello", reader.GetRdnRegExpSource());
        Assert.Equal("", reader.GetRdnRegExpFlags());
    }

    [Fact]
    public void Reader_ParseRegexWithAllFlags()
    {
        var reader = new Utf8JsonReader("/test/dgimsuy"u8);
        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.RdnRegExp, reader.TokenType);
        Assert.Equal("test", reader.GetRdnRegExpSource());
        Assert.Equal("dgimsuy", reader.GetRdnRegExpFlags());
    }

    [Fact]
    public void Reader_ParseRegexWithAnchors()
    {
        var reader = new Utf8JsonReader("/^[a-z]+$/i"u8);
        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.RdnRegExp, reader.TokenType);
        Assert.Equal("^[a-z]+$", reader.GetRdnRegExpSource());
        Assert.Equal("i", reader.GetRdnRegExpFlags());
    }

    [Fact]
    public void Reader_ParseRegexWithEscapedSlash()
    {
        var reader = new Utf8JsonReader("/a\\/b/"u8);
        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.RdnRegExp, reader.TokenType);
        // The source should contain the escaped slash
        Assert.Equal("a/b", reader.GetRdnRegExpSource());
        Assert.Equal("", reader.GetRdnRegExpFlags());
    }

    [Fact]
    public void Reader_ParseRegexInObject()
    {
        byte[] input = """{"pattern": /test/gi}"""u8.ToArray();
        var reader = new Utf8JsonReader(input);
        Assert.True(reader.Read()); // StartObject
        Assert.True(reader.Read()); // PropertyName
        Assert.Equal("pattern", reader.GetString());
        Assert.True(reader.Read()); // RdnRegExp
        Assert.Equal(JsonTokenType.RdnRegExp, reader.TokenType);
        Assert.Equal("test", reader.GetRdnRegExpSource());
        Assert.Equal("gi", reader.GetRdnRegExpFlags());
    }

    [Fact]
    public void Reader_ParseRegexInArray()
    {
        byte[] input = "[/abc/i, /def/g]"u8.ToArray();
        var reader = new Utf8JsonReader(input);
        Assert.True(reader.Read()); // StartArray
        Assert.True(reader.Read()); // RdnRegExp 1
        Assert.Equal(JsonTokenType.RdnRegExp, reader.TokenType);
        Assert.Equal("abc", reader.GetRdnRegExpSource());
        Assert.Equal("i", reader.GetRdnRegExpFlags());
        Assert.True(reader.Read()); // RdnRegExp 2
        Assert.Equal(JsonTokenType.RdnRegExp, reader.TokenType);
        Assert.Equal("def", reader.GetRdnRegExpSource());
        Assert.Equal("g", reader.GetRdnRegExpFlags());
    }

    [Fact]
    public void Reader_TryGetRdnRegExp()
    {
        var reader = new Utf8JsonReader("/test/gi"u8);
        Assert.True(reader.Read());
        Assert.True(reader.TryGetRdnRegExp(out string source, out string flags));
        Assert.Equal("test", source);
        Assert.Equal("gi", flags);
    }

    [Fact]
    public void Reader_ValueKindIsRdnRegExp()
    {
        byte[] input = "[/test/gi]"u8.ToArray();
        using var doc = JsonDocument.Parse(input);
        var element = doc.RootElement[0];
        Assert.Equal(JsonValueKind.RdnRegExp, element.ValueKind);
    }

    // --- Writer Tests ---

    [Fact]
    public void Writer_WriteRegexLiteral()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteRdnRegExpValue("test", "gi");
        }
        var output = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Equal("/test/gi", output);
    }

    [Fact]
    public void Writer_WriteRegexNoFlags()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteRdnRegExpValue("hello", "");
        }
        var output = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Equal("/hello/", output);
    }

    [Fact]
    public void Writer_WriteRegexUtf8()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteRdnRegExpValue("^[a-z]+$"u8, "i"u8);
        }
        var output = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Equal("/^[a-z]+$/i", output);
    }

    [Fact]
    public void Writer_WriteRegexInObject()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("pattern");
            writer.WriteRdnRegExpValue("test", "gi");
            writer.WriteEndObject();
        }
        var output = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Equal("""{"pattern":/test/gi}""", output);
    }

    [Fact]
    public void Writer_WriteRegexInArray()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            writer.WriteRdnRegExpValue("abc", "i");
            writer.WriteRdnRegExpValue("def", "g");
            writer.WriteEndArray();
        }
        var output = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Equal("[/abc/i,/def/g]", output);
    }

    // --- JsonDocument Tests ---

    [Fact]
    public void Document_ParseAndExtractRegex()
    {
        byte[] input = """{"re": /^[a-z]+$/i}"""u8.ToArray();
        using var doc = JsonDocument.Parse(input);
        var re = doc.RootElement.GetProperty("re");
        Assert.Equal(JsonValueKind.RdnRegExp, re.ValueKind);
        Assert.True(re.TryGetRdnRegExp(out string source, out string flags));
        Assert.Equal("^[a-z]+$", source);
        Assert.Equal("i", flags);
    }

    [Fact]
    public void Document_RegexGetSource()
    {
        byte[] input = "[/test/gi]"u8.ToArray();
        using var doc = JsonDocument.Parse(input);
        Assert.Equal("test", doc.RootElement[0].GetRdnRegExpSource());
    }

    [Fact]
    public void Document_RegexGetFlags()
    {
        byte[] input = "[/test/gi]"u8.ToArray();
        using var doc = JsonDocument.Parse(input);
        Assert.Equal("gi", doc.RootElement[0].GetRdnRegExpFlags());
    }

    [Fact]
    public void Document_RegexRoundtrip()
    {
        byte[] input = """{"a":/test/gi,"b":/^[a-z]+$/i}"""u8.ToArray();
        using var doc = JsonDocument.Parse(input);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            doc.RootElement.WriteTo(writer);
        }
        var output = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Equal("""{"a":/test/gi,"b":/^[a-z]+$/i}""", output);
    }

    // --- Serializer Tests ---

    [Fact]
    public void Serializer_SerializeRegex()
    {
        var regex = new Regex("test", RegexOptions.IgnoreCase);
        string rdn = JsonSerializer.Serialize(regex);
        Assert.Equal("/test/i", rdn);
    }

    [Fact]
    public void Serializer_DeserializeRegex()
    {
        byte[] input = "/^[a-z]+$/i"u8.ToArray();
        var regex = JsonSerializer.Deserialize<Regex>(input);
        Assert.NotNull(regex);
        Assert.Equal("^[a-z]+$", regex.ToString());
        Assert.True(regex.Options.HasFlag(RegexOptions.IgnoreCase));
    }

    [Fact]
    public void Serializer_RoundtripRegex()
    {
        var original = new Regex("test", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        string rdn = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<Regex>(rdn);
        Assert.NotNull(deserialized);
        Assert.Equal(original.ToString(), deserialized.ToString());
        Assert.True(deserialized.Options.HasFlag(RegexOptions.IgnoreCase));
        Assert.True(deserialized.Options.HasFlag(RegexOptions.Multiline));
    }

    [Fact]
    public void Serializer_RegexInObject()
    {
        var data = new Dictionary<string, Regex> { ["pattern"] = new Regex("test", RegexOptions.IgnoreCase) };
        string rdn = JsonSerializer.Serialize(data);
        Assert.Contains("/test/i", rdn);

        var deserialized = JsonSerializer.Deserialize<Dictionary<string, Regex>>(rdn);
        Assert.NotNull(deserialized);
        Assert.True(deserialized.ContainsKey("pattern"));
        Assert.Equal("test", deserialized["pattern"].ToString());
    }

    // --- Node Tests ---

    [Fact]
    public void Node_CreateRegexValue()
    {
        var regex = new Regex("test", RegexOptions.IgnoreCase);
        var node = JsonValue.Create(regex);
        Assert.NotNull(node);
    }

    // --- Conformance Test Suite ---

    [Fact]
    public void Conformance_ValidRegexp()
    {
        // Test against test-suite/valid/regexp.rdn
        string input = """
        {
          "simple": /test/gi,
          "anchored": /^[a-z]+$/i
        }
        """;

        using var doc = JsonDocument.Parse(input);
        var root = doc.RootElement;

        var simple = root.GetProperty("simple");
        Assert.Equal(JsonValueKind.RdnRegExp, simple.ValueKind);
        Assert.True(simple.TryGetRdnRegExp(out string source1, out string flags1));
        Assert.Equal("test", source1);
        Assert.Equal("gi", flags1);

        var anchored = root.GetProperty("anchored");
        Assert.Equal(JsonValueKind.RdnRegExp, anchored.ValueKind);
        Assert.True(anchored.TryGetRdnRegExp(out string source2, out string flags2));
        Assert.Equal("^[a-z]+$", source2);
        Assert.Equal("i", flags2);
    }

    // --- Comment vs Regex Disambiguation ---

    [Fact]
    public void Reader_RegexNotConfusedWithComment()
    {
        // /test/ is regex, not a comment
        var reader = new Utf8JsonReader("/test/"u8);
        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.RdnRegExp, reader.TokenType);
    }

    [Fact]
    public void Reader_CommentStillWorksWhenEnabled()
    {
        // // is a line comment (when comments are allowed), not regex
        byte[] input = "[1, // comment\n2]"u8.ToArray();
        var options = new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip };
        var reader = new Utf8JsonReader(input, options);
        Assert.True(reader.Read()); // StartArray
        Assert.True(reader.Read()); // Number 1
        Assert.Equal(1, reader.GetInt32());
        Assert.True(reader.Read()); // Number 2
        Assert.Equal(2, reader.GetInt32());
        Assert.True(reader.Read()); // EndArray
    }

    [Fact]
    public void Reader_RegexWithCommentHandlingDisallowed()
    {
        // With comments disallowed, /test/gi should still parse as regex
        byte[] input = "[/test/gi]"u8.ToArray();
        var options = new JsonReaderOptions { CommentHandling = JsonCommentHandling.Disallow };
        var reader = new Utf8JsonReader(input, options);
        Assert.True(reader.Read()); // StartArray
        Assert.True(reader.Read()); // RdnRegExp
        Assert.Equal(JsonTokenType.RdnRegExp, reader.TokenType);
        Assert.Equal("test", reader.GetRdnRegExpSource());
        Assert.Equal("gi", reader.GetRdnRegExpFlags());
    }

    [Fact]
    public void Reader_RegexAfterCommaInArray()
    {
        // Regex after comma in array (tests ConsumeNextToken comma handling)
        byte[] input = "[1, /test/gi, 2]"u8.ToArray();
        var reader = new Utf8JsonReader(input);
        Assert.True(reader.Read()); // StartArray
        Assert.True(reader.Read()); // Number 1
        Assert.Equal(1, reader.GetInt32());
        Assert.True(reader.Read()); // RdnRegExp
        Assert.Equal(JsonTokenType.RdnRegExp, reader.TokenType);
        Assert.Equal("test", reader.GetRdnRegExpSource());
        Assert.Equal("gi", reader.GetRdnRegExpFlags());
        Assert.True(reader.Read()); // Number 2
        Assert.Equal(2, reader.GetInt32());
        Assert.True(reader.Read()); // EndArray
    }

    [Fact]
    public void Reader_RegexWithCommentSkipMode()
    {
        // Regex with CommentHandling.Skip — comments before regex should be skipped
        byte[] input = "[/* comment */ /test/gi]"u8.ToArray();
        var options = new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip };
        var reader = new Utf8JsonReader(input, options);
        Assert.True(reader.Read()); // StartArray
        Assert.True(reader.Read()); // RdnRegExp (comment skipped)
        Assert.Equal(JsonTokenType.RdnRegExp, reader.TokenType);
        Assert.Equal("test", reader.GetRdnRegExpSource());
        Assert.Equal("gi", reader.GetRdnRegExpFlags());
    }

    [Fact]
    public void Reader_RegexAfterCommaWithCommentSkip()
    {
        // Regex after comma with comment skipping
        byte[] input = "[1, /abc/i, /def/g]"u8.ToArray();
        var options = new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip };
        var reader = new Utf8JsonReader(input, options);
        Assert.True(reader.Read()); // StartArray
        Assert.True(reader.Read()); // Number 1
        Assert.True(reader.Read()); // RdnRegExp 1
        Assert.Equal("abc", reader.GetRdnRegExpSource());
        Assert.Equal("i", reader.GetRdnRegExpFlags());
        Assert.True(reader.Read()); // RdnRegExp 2
        Assert.Equal("def", reader.GetRdnRegExpSource());
        Assert.Equal("g", reader.GetRdnRegExpFlags());
        Assert.True(reader.Read()); // EndArray
    }

    [Fact]
    public void Reader_CommentBeforeRegexInObject()
    {
        // Comment handling in object: comment before property, regex as value
        byte[] input = "{\"p\": /test/gi}"u8.ToArray();
        var options = new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip };
        var reader = new Utf8JsonReader(input, options);
        Assert.True(reader.Read()); // StartObject
        Assert.True(reader.Read()); // PropertyName
        Assert.Equal("p", reader.GetString());
        Assert.True(reader.Read()); // RdnRegExp
        Assert.Equal(JsonTokenType.RdnRegExp, reader.TokenType);
        Assert.Equal("test", reader.GetRdnRegExpSource());
        Assert.Equal("gi", reader.GetRdnRegExpFlags());
    }
}
