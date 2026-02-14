using System.Collections.Generic;
using Rdn;
using Rdn.Nodes;
using Rdn.Serialization;
using Xunit;

namespace Rdn.Tests;

public class RdnMapTests
{
    #region 1. Utf8RdnReader — Explicit Map parsing

    [Fact]
    public void Reader_ExplicitEmptyMap()
    {
        var bytes = "Map{}"u8.ToArray();
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartMap, reader.TokenType);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndMap, reader.TokenType);
        Assert.False(reader.Read());
    }

    [Fact]
    public void Reader_ExplicitMapStringKeys()
    {
        var bytes = "Map{\"a\"=>1,\"b\"=>2}"u8.ToArray();
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartMap, reader.TokenType);

        // Key "a"
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.String, reader.TokenType);
        Assert.Equal("a", reader.GetString());

        // Value 1
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.Number, reader.TokenType);
        Assert.Equal(1, reader.GetInt32());

        // Key "b"
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.String, reader.TokenType);
        Assert.Equal("b", reader.GetString());

        // Value 2
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.Number, reader.TokenType);
        Assert.Equal(2, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndMap, reader.TokenType);
        Assert.False(reader.Read());
    }

    [Fact]
    public void Reader_ExplicitMapNumberKeys()
    {
        var bytes = "Map{1=>\"a\",2=>\"b\"}"u8.ToArray();
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartMap, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.Number, reader.TokenType);
        Assert.Equal(1, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.String, reader.TokenType);
        Assert.Equal("a", reader.GetString());

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.Number, reader.TokenType);
        Assert.Equal(2, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.String, reader.TokenType);
        Assert.Equal("b", reader.GetString());

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndMap, reader.TokenType);
    }

    [Fact]
    public void Reader_ExplicitMapWithSpaces()
    {
        var bytes = "Map{ \"x\" => 10, \"y\" => 20 }"u8.ToArray();
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartMap, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal("x", reader.GetString());
        Assert.True(reader.Read());
        Assert.Equal(10, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal("y", reader.GetString());
        Assert.True(reader.Read());
        Assert.Equal(20, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndMap, reader.TokenType);
    }

    [Fact]
    public void Reader_ExplicitMapNested()
    {
        var bytes = "Map{\"inner\"=>Map{1=>2}}"u8.ToArray();
        var reader = new Utf8RdnReader(bytes);

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartMap, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal("inner", reader.GetString());

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartMap, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(2, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndMap, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndMap, reader.TokenType);
    }

    #endregion

    #region 2. Utf8RdnReader — Implicit Map parsing (brace disambiguation)

    [Fact]
    public void Reader_ImplicitMapStringKeys()
    {
        // { "a" => 1 } — string followed by => → Map
        var bytes = "{\"a\"=>1}"u8.ToArray();
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartMap, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal("a", reader.GetString());

        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndMap, reader.TokenType);
    }

    [Fact]
    public void Reader_ImplicitMapNumberKeys()
    {
        // { 1 => "a" } — number followed by => → Map
        var bytes = "{1=>\"a\"}"u8.ToArray();
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartMap, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal("a", reader.GetString());

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndMap, reader.TokenType);
    }

    [Fact]
    public void Reader_ImplicitMapBooleanKey()
    {
        var bytes = "{true=>1}"u8.ToArray();
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartMap, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.True, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndMap, reader.TokenType);
    }

    [Fact]
    public void Reader_ImplicitMapNullKey()
    {
        var bytes = "{null=>1}"u8.ToArray();
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartMap, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.Null, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndMap, reader.TokenType);
    }

    [Fact]
    public void Reader_ImplicitMapMultipleEntries()
    {
        var bytes = "{\"a\"=>1,\"b\"=>2,\"c\"=>3}"u8.ToArray();
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartMap, reader.TokenType);

        for (int i = 0; i < 3; i++)
        {
            Assert.True(reader.Read());
            Assert.Equal(RdnTokenType.String, reader.TokenType);
            Assert.True(reader.Read());
            Assert.Equal(RdnTokenType.Number, reader.TokenType);
        }

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndMap, reader.TokenType);
    }

    #endregion

    #region 3. Brace disambiguation

    [Fact]
    public void Reader_BraceDisambiguation_ColonIsObject()
    {
        var bytes = "{\"a\":1}"u8.ToArray();
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartObject, reader.TokenType);
    }

    [Fact]
    public void Reader_BraceDisambiguation_ArrowIsMap()
    {
        var bytes = "{\"a\"=>1}"u8.ToArray();
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartMap, reader.TokenType);
    }

    [Fact]
    public void Reader_BraceDisambiguation_CommaIsSet()
    {
        var bytes = "{\"a\",\"b\"}"u8.ToArray();
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartSet, reader.TokenType);
    }

    [Fact]
    public void Reader_BraceDisambiguation_CloseBraceIsSet()
    {
        var bytes = "{\"a\"}"u8.ToArray();
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartSet, reader.TokenType);
    }

    [Fact]
    public void Reader_BraceDisambiguation_EmptyIsObject()
    {
        var bytes = "{}"u8.ToArray();
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartObject, reader.TokenType);
    }

    [Fact]
    public void Reader_BraceDisambiguation_NumberArrowIsMap()
    {
        var bytes = "{1=>\"x\"}"u8.ToArray();
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartMap, reader.TokenType);
    }

    [Fact]
    public void Reader_BraceDisambiguation_NumberCommaIsSet()
    {
        var bytes = "{1,2}"u8.ToArray();
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartSet, reader.TokenType);
    }

    #endregion

    #region 4. Utf8RdnWriter — Map output

    [Fact]
    public void Writer_EmptyMap()
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using var writer = new Utf8RdnWriter(buffer);
        writer.WriteStartMap(forceTypeName: true);
        writer.WriteEndMap();
        writer.Flush();

        Assert.Equal("Map{}", System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan));
    }

    [Fact]
    public void Writer_NonEmptyMap_OmitsPrefix()
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using var writer = new Utf8RdnWriter(buffer);
        writer.WriteStartMap();
        writer.WriteStringValue("a");
        writer.WriteMapArrow();
        writer.WriteNumberValue(1);
        writer.WriteEndMap();
        writer.Flush();

        Assert.Equal("{\"a\"=>1}", System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan));
    }

    [Fact]
    public void Writer_MapWithEntries()
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using var writer = new Utf8RdnWriter(buffer);
        writer.WriteStartMap();
        writer.WriteStringValue("a");
        writer.WriteMapArrow();
        writer.WriteNumberValue(1);
        writer.WriteStringValue("b");
        writer.WriteMapArrow();
        writer.WriteNumberValue(2);
        writer.WriteEndMap();
        writer.Flush();

        Assert.Equal("{\"a\"=>1,\"b\"=>2}", System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan));
    }

    [Fact]
    public void Writer_NamedPropertyMap()
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using var writer = new Utf8RdnWriter(buffer);
        writer.WriteStartObject();
        writer.WriteStartMap("data");
        writer.WriteStringValue("x");
        writer.WriteMapArrow();
        writer.WriteNumberValue(1);
        writer.WriteEndMap();
        writer.WriteEndObject();
        writer.Flush();

        Assert.Equal("{\"data\":{\"x\"=>1}}", System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan));
    }

    [Fact]
    public void Writer_IndentedMap()
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using var writer = new Utf8RdnWriter(buffer, new RdnWriterOptions { Indented = true });
        writer.WriteStartMap();
        writer.WriteStringValue("a");
        writer.WriteMapArrow();
        writer.WriteNumberValue(1);
        writer.WriteStringValue("b");
        writer.WriteMapArrow();
        writer.WriteNumberValue(2);
        writer.WriteEndMap();
        writer.Flush();

        var output = System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
        var expected = "{\n  \"a\" => 1,\n  \"b\" => 2\n}";
        Assert.Equal(expected, output);
    }

    [Fact]
    public void Writer_AlwaysWriteCollectionTypeNames_Map()
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using var writer = new Utf8RdnWriter(buffer, new RdnWriterOptions { AlwaysWriteCollectionTypeNames = true });
        writer.WriteStartMap();
        writer.WriteStringValue("a");
        writer.WriteMapArrow();
        writer.WriteNumberValue(1);
        writer.WriteEndMap();
        writer.Flush();

        Assert.Equal("Map{\"a\"=>1}", System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan));
    }

    #endregion

    #region 5. RdnDocument — Map parsing

    [Fact]
    public void Document_ParseExplicitMap()
    {
        using var doc = RdnDocument.Parse("Map{\"a\"=>1,\"b\"=>2}");
        Assert.Equal(RdnValueKind.Map, doc.RootElement.ValueKind);
        // Map stores items flat: key, value, key, value = 4 items
        Assert.Equal(4, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public void Document_ParseImplicitMap()
    {
        using var doc = RdnDocument.Parse("{\"x\"=>10}");
        Assert.Equal(RdnValueKind.Map, doc.RootElement.ValueKind);
        Assert.Equal(2, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public void Document_ParseEmptyMap()
    {
        using var doc = RdnDocument.Parse("Map{}");
        Assert.Equal(RdnValueKind.Map, doc.RootElement.ValueKind);
        Assert.Equal(0, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public void Document_EnumerateMap()
    {
        using var doc = RdnDocument.Parse("Map{\"a\"=>1,\"b\"=>2}");
        var items = new List<string>();
        foreach (var element in doc.RootElement.EnumerateMap())
        {
            items.Add(element.ToString()!);
        }
        // Items are flat: "a", "1", "b", "2"
        Assert.Equal(4, items.Count);
        Assert.Equal("a", items[0]);
        Assert.Equal("b", items[2]);
    }

    [Fact]
    public void Document_MapWriteToRoundtrip()
    {
        using var doc = RdnDocument.Parse("Map{\"a\"=>1,\"b\"=>2}");
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using var writer = new Utf8RdnWriter(buffer);
        doc.RootElement.WriteTo(writer);
        writer.Flush();

        var output = System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
        Assert.Equal("{\"a\"=>1,\"b\"=>2}", output);
    }

    [Fact]
    public void Document_EmptyMapRoundtrip()
    {
        using var doc = RdnDocument.Parse("Map{}");
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using var writer = new Utf8RdnWriter(buffer);
        doc.RootElement.WriteTo(writer);
        writer.Flush();

        var output = System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
        Assert.Equal("Map{}", output);
    }

    [Fact]
    public void Document_MapIndexer()
    {
        using var doc = RdnDocument.Parse("Map{\"a\"=>1,\"b\"=>2}");
        Assert.Equal("a", doc.RootElement[0].GetString());
        Assert.Equal(1, doc.RootElement[1].GetInt32());
        Assert.Equal("b", doc.RootElement[2].GetString());
        Assert.Equal(2, doc.RootElement[3].GetInt32());
    }

    [Fact]
    public void Document_MapWithNumberKeys()
    {
        using var doc = RdnDocument.Parse("Map{1=>\"a\",2=>\"b\"}");
        Assert.Equal(RdnValueKind.Map, doc.RootElement.ValueKind);
        Assert.Equal(1, doc.RootElement[0].GetInt32());
        Assert.Equal("a", doc.RootElement[1].GetString());
    }

    [Fact]
    public void Document_NestedMapRoundtrip()
    {
        using var doc = RdnDocument.Parse("Map{\"inner\"=>Map{1=>2}}");
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using var writer = new Utf8RdnWriter(buffer);
        doc.RootElement.WriteTo(writer);
        writer.Flush();

        var output = System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
        Assert.Equal("{\"inner\"=>{1=>2}}", output);
    }

    #endregion

    #region 6. RdnMap DOM (mutable)

    [Fact]
    public void RdnMap_CreateAndAdd()
    {
        var map = new RdnMap();
        map.Add(RdnValue.Create("key"), RdnValue.Create(42));
        Assert.Equal(1, map.Count);
    }

    [Fact]
    public void RdnMap_Clear()
    {
        var map = new RdnMap();
        map.Add(RdnValue.Create("a"), RdnValue.Create(1));
        map.Add(RdnValue.Create("b"), RdnValue.Create(2));
        Assert.Equal(2, map.Count);
        map.Clear();
        Assert.Equal(0, map.Count);
    }

    [Fact]
    public void RdnMap_WriteTo()
    {
        var map = new RdnMap();
        map.Add(RdnValue.Create("x"), RdnValue.Create(10));
        map.Add(RdnValue.Create("y"), RdnValue.Create(20));
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using var writer = new Utf8RdnWriter(buffer);
        map.WriteTo(writer);
        writer.Flush();

        var output = System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
        Assert.Equal("{\"x\"=>10,\"y\"=>20}", output);
    }

    [Fact]
    public void RdnMap_ParseFromRdn()
    {
        var node = RdnNode.Parse("Map{\"a\"=>1,\"b\"=>2}");
        Assert.IsType<RdnMap>(node);
        var map = (RdnMap)node!;
        Assert.Equal(2, map.Count);
    }

    [Fact]
    public void RdnMap_GetValueKind()
    {
        var map = new RdnMap();
        Assert.Equal(RdnValueKind.Map, map.GetValueKind());
    }

    [Fact]
    public void RdnMap_Enumerate()
    {
        var map = new RdnMap();
        map.Add(RdnValue.Create("a"), RdnValue.Create(1));
        map.Add(RdnValue.Create("b"), RdnValue.Create(2));

        var keys = new List<string>();
        var values = new List<int>();
        foreach (var entry in map)
        {
            keys.Add(entry.Key!.GetValue<string>());
            values.Add(entry.Value!.GetValue<int>());
        }

        Assert.Equal(new List<string> { "a", "b" }, keys);
        Assert.Equal(new List<int> { 1, 2 }, values);
    }

    [Fact]
    public void RdnMap_ParseImplicitMap()
    {
        var node = RdnNode.Parse("{\"x\"=>10}");
        Assert.IsType<RdnMap>(node);
        var map = (RdnMap)node!;
        Assert.Equal(1, map.Count);
    }

    #endregion

    #region 7. Edge cases

    [Fact]
    public void Reader_MapWithNestedArray()
    {
        var bytes = "Map{\"arr\"=>[1,2,3]}"u8.ToArray();
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartMap, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal("arr", reader.GetString());

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartArray, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32());
        Assert.True(reader.Read());
        Assert.Equal(2, reader.GetInt32());
        Assert.True(reader.Read());
        Assert.Equal(3, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndArray, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndMap, reader.TokenType);
    }

    [Fact]
    public void Reader_ObjectContainingMap()
    {
        var bytes = "{\"data\":Map{\"a\"=>1}}"u8.ToArray();
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartObject, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.PropertyName, reader.TokenType);
        Assert.Equal("data", reader.GetString());

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartMap, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal("a", reader.GetString());

        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndMap, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndObject, reader.TokenType);
    }

    [Fact]
    public void Reader_ArrayContainingMap()
    {
        var bytes = "[Map{\"a\"=>1},Map{\"b\"=>2}]"u8.ToArray();
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartArray, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartMap, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal("a", reader.GetString());
        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32());
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndMap, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartMap, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal("b", reader.GetString());
        Assert.True(reader.Read());
        Assert.Equal(2, reader.GetInt32());
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndMap, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndArray, reader.TokenType);
    }

    [Fact]
    public void Reader_MapInSet()
    {
        var bytes = "Set{Map{1=>2}}"u8.ToArray();
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartSet, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartMap, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(2, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndMap, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndSet, reader.TokenType);
    }

    [Fact]
    public void Reader_MapWithArrayKey()
    {
        // Map key is an array
        var bytes = "Map{[1,2]=>\"pair\"}"u8.ToArray();
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartMap, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartArray, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32());
        Assert.True(reader.Read());
        Assert.Equal(2, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndArray, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal("pair", reader.GetString());

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndMap, reader.TokenType);
    }

    [Fact]
    public void Document_MapInObjectRoundtrip()
    {
        using var doc = RdnDocument.Parse("{\"data\":Map{\"a\"=>1}}");
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using var writer = new Utf8RdnWriter(buffer);
        doc.RootElement.WriteTo(writer);
        writer.Flush();

        var output = System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
        Assert.Equal("{\"data\":{\"a\"=>1}}", output);
    }

    [Fact]
    public void Document_MapWithArrayValueRoundtrip()
    {
        using var doc = RdnDocument.Parse("Map{\"arr\"=>[1,2,3]}");
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using var writer = new Utf8RdnWriter(buffer);
        doc.RootElement.WriteTo(writer);
        writer.Flush();

        var output = System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
        Assert.Equal("{\"arr\"=>[1,2,3]}", output);
    }

    #endregion

    #region 8. Error cases

    [Fact]
    public void Reader_UnclosedMap_Throws()
    {
        var bytes = "Map{\"a\"=>1"u8.ToArray();
        Assert.ThrowsAny<RdnException>(() =>
        {
            var reader = new Utf8RdnReader(bytes);
            while (reader.Read()) { }
        });
    }

    [Fact]
    public void Reader_MapClosedWithBracket_Throws()
    {
        var bytes = "Map{\"a\"=>1]"u8.ToArray();
        Assert.ThrowsAny<RdnException>(() =>
        {
            var reader = new Utf8RdnReader(bytes);
            while (reader.Read()) { }
        });
    }

    #endregion

    #region 9. Dictionary<TKey, TValue> serialization as RDN Maps

    [Fact]
    public void Serialize_DictionaryStringInt_ProducesMapSyntax()
    {
        var dict = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
        string rdn = RdnSerializer.Serialize(dict);
        Assert.Contains("=>", rdn);
        Assert.StartsWith("{", rdn);
        Assert.EndsWith("}", rdn);
        // Non-empty maps should NOT have Map{ prefix
        Assert.DoesNotContain("Map{", rdn);
    }

    [Fact]
    public void Roundtrip_DictionaryStringInt()
    {
        var original = new Dictionary<string, int> { ["hello"] = 1, ["world"] = 2 };
        string rdn = RdnSerializer.Serialize(original);
        Assert.Contains("=>", rdn);

        var deserialized = RdnSerializer.Deserialize<Dictionary<string, int>>(rdn);
        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.Count);
        Assert.Equal(1, deserialized["hello"]);
        Assert.Equal(2, deserialized["world"]);
    }

    [Fact]
    public void Roundtrip_DictionaryIntString()
    {
        var original = new Dictionary<int, string> { [1] = "one", [2] = "two" };
        string rdn = RdnSerializer.Serialize(original);
        Assert.Contains("=>", rdn);

        var deserialized = RdnSerializer.Deserialize<Dictionary<int, string>>(rdn);
        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.Count);
        Assert.Equal("one", deserialized[1]);
        Assert.Equal("two", deserialized[2]);
    }

    [Fact]
    public void Roundtrip_DictionaryDateTimeString()
    {
        var dt = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var original = new Dictionary<DateTime, string> { [dt] = "event" };
        string rdn = RdnSerializer.Serialize(original);
        Assert.Contains("=>", rdn);
        Assert.Contains("@2024-01-15T10:30:00.000Z", rdn);

        var deserialized = RdnSerializer.Deserialize<Dictionary<DateTime, string>>(rdn);
        Assert.NotNull(deserialized);
        Assert.Single(deserialized);
        Assert.Equal("event", deserialized[dt]);
    }

    [Fact]
    public void Roundtrip_DictionaryDateOnlyInt()
    {
        var date = new DateOnly(2024, 6, 15);
        var original = new Dictionary<DateOnly, int> { [date] = 42 };
        string rdn = RdnSerializer.Serialize(original);
        Assert.Contains("=>", rdn);
        Assert.Contains("@2024-06-15", rdn);

        var deserialized = RdnSerializer.Deserialize<Dictionary<DateOnly, int>>(rdn);
        Assert.NotNull(deserialized);
        Assert.Single(deserialized);
        Assert.Equal(42, deserialized[date]);
    }

    [Fact]
    public void Roundtrip_EmptyDictionary()
    {
        var original = new Dictionary<string, int>();
        string rdn = RdnSerializer.Serialize(original);
        Assert.Equal("Map{}", rdn);

        var deserialized = RdnSerializer.Deserialize<Dictionary<string, int>>(rdn);
        Assert.NotNull(deserialized);
        Assert.Empty(deserialized);
    }

    [Fact]
    public void Roundtrip_NestedDictionary()
    {
        var original = new Dictionary<string, Dictionary<int, string>>
        {
            ["outer"] = new Dictionary<int, string> { [1] = "inner" }
        };
        string rdn = RdnSerializer.Serialize(original);
        Assert.Contains("=>", rdn);

        var deserialized = RdnSerializer.Deserialize<Dictionary<string, Dictionary<int, string>>>(rdn);
        Assert.NotNull(deserialized);
        Assert.Single(deserialized);
        Assert.Equal("inner", deserialized["outer"][1]);
    }

    [Fact]
    public void Deserialize_MapSyntax_IntoDictionary()
    {
        // Manually written RDN Map syntax should deserialize correctly
        string rdn = """Map{"x"=>10,"y"=>20}""";
        var dict = RdnSerializer.Deserialize<Dictionary<string, int>>(rdn);
        Assert.NotNull(dict);
        Assert.Equal(2, dict.Count);
        Assert.Equal(10, dict["x"]);
        Assert.Equal(20, dict["y"]);
    }

    [Fact]
    public void Deserialize_ImplicitMapSyntax_IntoDictionary()
    {
        // Implicit brace disambiguation: string => value → Map
        string rdn = """{"x"=>10,"y"=>20}""";
        var dict = RdnSerializer.Deserialize<Dictionary<string, int>>(rdn);
        Assert.NotNull(dict);
        Assert.Equal(2, dict.Count);
        Assert.Equal(10, dict["x"]);
        Assert.Equal(20, dict["y"]);
    }

    [Fact]
    public void Deserialize_ObjectSyntax_IntoDictionary()
    {
        // Standard RDN object syntax should still work for backward compatibility
        string rdn = """{"x":10,"y":20}""";
        var dict = RdnSerializer.Deserialize<Dictionary<string, int>>(rdn);
        Assert.NotNull(dict);
        Assert.Equal(2, dict.Count);
        Assert.Equal(10, dict["x"]);
        Assert.Equal(20, dict["y"]);
    }

    [Fact]
    public void Serialize_DictionaryInRecord_ProducesMapSyntax()
    {
        var record = new RecordWithDict("test", new Dictionary<string, int> { ["a"] = 1 });
        string rdn = RdnSerializer.Serialize(record);
        Assert.Contains("=>", rdn);
        // Non-empty maps use implicit syntax (no Map{ prefix)
        Assert.DoesNotContain("Map{", rdn);
    }

    private record RecordWithDict(string Name, Dictionary<string, int> Data);

    #endregion
}
