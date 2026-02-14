using System.Collections.Generic;
using Rdn;
using Rdn.Nodes;
using Rdn.Serialization;
using Xunit;

namespace Rdn.Tests;

public class RdnSetTests
{
    #region 1. Utf8JsonReader — Explicit Set parsing

    [Fact]
    public void Reader_ExplicitEmptySet()
    {
        var bytes = "Set{}"u8.ToArray();
        var reader = new Utf8JsonReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartSet, reader.TokenType);
        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.EndSet, reader.TokenType);
        Assert.False(reader.Read());
    }

    [Fact]
    public void Reader_ExplicitSetWithNumbers()
    {
        var bytes = "Set{1, 2, 3}"u8.ToArray();
        var reader = new Utf8JsonReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartSet, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.Number, reader.TokenType);
        Assert.Equal(1, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.Number, reader.TokenType);
        Assert.Equal(2, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.Number, reader.TokenType);
        Assert.Equal(3, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.EndSet, reader.TokenType);
        Assert.False(reader.Read());
    }

    [Fact]
    public void Reader_ExplicitSetWithStrings()
    {
        var bytes = "Set{\"a\", \"b\", \"c\"}"u8.ToArray();
        var reader = new Utf8JsonReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartSet, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.String, reader.TokenType);
        Assert.Equal("a", reader.GetString());

        Assert.True(reader.Read());
        Assert.Equal("b", reader.GetString());

        Assert.True(reader.Read());
        Assert.Equal("c", reader.GetString());

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.EndSet, reader.TokenType);
    }

    [Fact]
    public void Reader_ExplicitSetNested()
    {
        var bytes = "Set{Set{1, 2}, Set{3, 4}}"u8.ToArray();
        var reader = new Utf8JsonReader(bytes);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartSet, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartSet, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32());
        Assert.True(reader.Read());
        Assert.Equal(2, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.EndSet, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartSet, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(3, reader.GetInt32());
        Assert.True(reader.Read());
        Assert.Equal(4, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.EndSet, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.EndSet, reader.TokenType);
    }

    #endregion

    #region 2. Utf8JsonReader — Implicit Set parsing (brace disambiguation)

    [Fact]
    public void Reader_ImplicitSetSingleNumber()
    {
        var bytes = "{42}"u8.ToArray();
        var reader = new Utf8JsonReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartSet, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.Number, reader.TokenType);
        Assert.Equal(42, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.EndSet, reader.TokenType);
    }

    [Fact]
    public void Reader_ImplicitSetMultipleNumbers()
    {
        var bytes = "{1, 2, 3}"u8.ToArray();
        var reader = new Utf8JsonReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartSet, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32());
        Assert.True(reader.Read());
        Assert.Equal(2, reader.GetInt32());
        Assert.True(reader.Read());
        Assert.Equal(3, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.EndSet, reader.TokenType);
    }

    [Fact]
    public void Reader_ImplicitSetSingleString()
    {
        // { "only" } — single string followed by } → Set
        var bytes = "{\"only\"}"u8.ToArray();
        var reader = new Utf8JsonReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartSet, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.String, reader.TokenType);
        Assert.Equal("only", reader.GetString());

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.EndSet, reader.TokenType);
    }

    [Fact]
    public void Reader_ImplicitSetMultipleStrings()
    {
        // { "a", "b", "c" } → Set (comma after first string)
        var bytes = "{\"a\", \"b\", \"c\"}"u8.ToArray();
        var reader = new Utf8JsonReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartSet, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal("a", reader.GetString());
        Assert.True(reader.Read());
        Assert.Equal("b", reader.GetString());
        Assert.True(reader.Read());
        Assert.Equal("c", reader.GetString());

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.EndSet, reader.TokenType);
    }

    [Fact]
    public void Reader_BraceDisambiguation_EmptyBraces_IsObject()
    {
        var bytes = "{}"u8.ToArray();
        var reader = new Utf8JsonReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartObject, reader.TokenType);
        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.EndObject, reader.TokenType);
    }

    [Fact]
    public void Reader_BraceDisambiguation_KeyColonValue_IsObject()
    {
        var bytes = "{\"key\": 1}"u8.ToArray();
        var reader = new Utf8JsonReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.PropertyName, reader.TokenType);
    }

    [Fact]
    public void Reader_ImplicitSetWithBoolean()
    {
        var bytes = "{true, false}"u8.ToArray();
        var reader = new Utf8JsonReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartSet, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.True, reader.TokenType);
        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.False, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.EndSet, reader.TokenType);
    }

    [Fact]
    public void Reader_ImplicitSetWithNull()
    {
        var bytes = "{null}"u8.ToArray();
        var reader = new Utf8JsonReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartSet, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.Null, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.EndSet, reader.TokenType);
    }

    [Fact]
    public void Reader_ImplicitSetWithNestedArray()
    {
        var bytes = "{[1, 2]}"u8.ToArray();
        var reader = new Utf8JsonReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartSet, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartArray, reader.TokenType);
    }

    [Fact]
    public void Reader_ImplicitSetWithRdnDateTime()
    {
        var bytes = "{@2024-01-15}"u8.ToArray();
        var reader = new Utf8JsonReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartSet, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.RdnDateTime, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.EndSet, reader.TokenType);
    }

    #endregion

    #region 3. Utf8JsonWriter — Set output

    [Fact]
    public void Writer_EmptySet()
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        writer.WriteStartSet(forceTypeName: true);
        writer.WriteEndSet();
        writer.Flush();

        Assert.Equal("Set{}", System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan));
    }

    [Fact]
    public void Writer_NonEmptySet_OmitsPrefix()
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        writer.WriteStartSet();
        writer.WriteNumberValue(42);
        writer.WriteEndSet();
        writer.Flush();

        Assert.Equal("{42}", System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan));
    }

    [Fact]
    public void Writer_SetWithNumbers()
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        writer.WriteStartSet();
        writer.WriteNumberValue(1);
        writer.WriteNumberValue(2);
        writer.WriteNumberValue(3);
        writer.WriteEndSet();
        writer.Flush();

        Assert.Equal("{1,2,3}", System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan));
    }

    [Fact]
    public void Writer_SetWithStrings()
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        writer.WriteStartSet();
        writer.WriteStringValue("a");
        writer.WriteStringValue("b");
        writer.WriteEndSet();
        writer.Flush();

        Assert.Equal("{\"a\",\"b\"}", System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan));
    }

    [Fact]
    public void Writer_NamedPropertySet()
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        writer.WriteStartObject();
        writer.WriteStartSet("tags");
        writer.WriteStringValue("a");
        writer.WriteEndSet();
        writer.WriteEndObject();
        writer.Flush();

        Assert.Equal("{\"tags\":{\"a\"}}", System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan));
    }

    [Fact]
    public void Writer_IndentedSet()
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true });
        writer.WriteStartSet();
        writer.WriteNumberValue(1);
        writer.WriteNumberValue(2);
        writer.WriteEndSet();
        writer.Flush();

        var output = System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
        // Non-empty set should NOT have Set{ prefix
        Assert.DoesNotContain("Set{", output);
        Assert.StartsWith("{", output);
        Assert.Contains("1", output);
        Assert.Contains("2", output);
    }

    [Fact]
    public void Writer_AlwaysWriteCollectionTypeNames_Set()
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { AlwaysWriteCollectionTypeNames = true });
        writer.WriteStartSet();
        writer.WriteNumberValue(1);
        writer.WriteEndSet();
        writer.Flush();

        Assert.Equal("Set{1}", System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan));
    }

    #endregion

    #region 4. Serialization roundtrip — HashSet<T>

    [Fact]
    public void Serialize_HashSetInt()
    {
        var set = new HashSet<int> { 1, 2, 3 };
        var rdn = JsonSerializer.Serialize(set);

        // Non-empty sets use implicit syntax (no Set{ prefix)
        Assert.StartsWith("{", rdn);
        Assert.EndsWith("}", rdn);
        Assert.DoesNotContain("Set{", rdn);
        Assert.Contains("1", rdn);
        Assert.Contains("2", rdn);
        Assert.Contains("3", rdn);
    }

    [Fact]
    public void Serialize_HashSetString()
    {
        var set = new HashSet<string> { "hello", "world" };
        var rdn = JsonSerializer.Serialize(set);

        Assert.StartsWith("{", rdn);
        Assert.DoesNotContain("Set{", rdn);
        Assert.Contains("\"hello\"", rdn);
        Assert.Contains("\"world\"", rdn);
    }

    [Fact]
    public void Deserialize_ExplicitSetToHashSet()
    {
        var rdn = "Set{1, 2, 3}";
        var set = JsonSerializer.Deserialize<HashSet<int>>(rdn);

        Assert.NotNull(set);
        Assert.Equal(3, set!.Count);
        Assert.Contains(1, set);
        Assert.Contains(2, set);
        Assert.Contains(3, set);
    }

    [Fact]
    public void Deserialize_ImplicitSetToHashSet()
    {
        var rdn = "{1, 2, 3}";
        var set = JsonSerializer.Deserialize<HashSet<int>>(rdn);

        Assert.NotNull(set);
        Assert.Equal(3, set!.Count);
        Assert.Contains(1, set);
        Assert.Contains(2, set);
        Assert.Contains(3, set);
    }

    [Fact]
    public void Deserialize_ArrayToHashSet()
    {
        // HashSet should still deserialize from array syntax for JSON compat
        var json = "[1, 2, 3]";
        var set = JsonSerializer.Deserialize<HashSet<int>>(json);

        Assert.NotNull(set);
        Assert.Equal(3, set!.Count);
        Assert.Contains(1, set);
    }

    [Fact]
    public void Roundtrip_HashSetInt()
    {
        var original = new HashSet<int> { 10, 20, 30 };
        var rdn = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<HashSet<int>>(rdn);

        Assert.NotNull(deserialized);
        Assert.Equal(original, deserialized);
    }

    [Fact]
    public void Roundtrip_HashSetString()
    {
        var original = new HashSet<string> { "alpha", "beta" };
        var rdn = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<HashSet<string>>(rdn);

        Assert.NotNull(deserialized);
        Assert.Equal(original, deserialized);
    }

    [Fact]
    public void Serialize_EmptyHashSet()
    {
        var set = new HashSet<int>();
        var rdn = JsonSerializer.Serialize(set);
        Assert.Equal("Set{}", rdn);
    }

    #endregion

    #region 5. JsonDocument — Set parsing

    [Fact]
    public void Document_ParseExplicitSet()
    {
        using var doc = JsonDocument.Parse("Set{1, 2, 3}");
        Assert.Equal(JsonValueKind.Set, doc.RootElement.ValueKind);
        Assert.Equal(3, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public void Document_ParseImplicitSet()
    {
        using var doc = JsonDocument.Parse("{42}");
        Assert.Equal(JsonValueKind.Set, doc.RootElement.ValueKind);
        Assert.Equal(1, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public void Document_EnumerateSet()
    {
        using var doc = JsonDocument.Parse("Set{10, 20, 30}");
        var values = new List<int>();
        foreach (var element in doc.RootElement.EnumerateSet())
        {
            values.Add(element.GetInt32());
        }
        Assert.Equal(new List<int> { 10, 20, 30 }, values);
    }

    [Fact]
    public void Document_SetWriteToRoundtrip()
    {
        using var doc = JsonDocument.Parse("Set{1, 2, 3}");
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        doc.RootElement.WriteTo(writer);
        writer.Flush();

        var output = System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
        Assert.Equal("{1,2,3}", output);
    }

    [Fact]
    public void Document_SetIndexer()
    {
        using var doc = JsonDocument.Parse("Set{10, 20, 30}");
        Assert.Equal(10, doc.RootElement[0].GetInt32());
        Assert.Equal(20, doc.RootElement[1].GetInt32());
        Assert.Equal(30, doc.RootElement[2].GetInt32());
    }

    [Fact]
    public void Document_EmptyExplicitSet()
    {
        using var doc = JsonDocument.Parse("Set{}");
        Assert.Equal(JsonValueKind.Set, doc.RootElement.ValueKind);
        Assert.Equal(0, doc.RootElement.GetArrayLength());
    }

    #endregion

    #region 6. JsonSet DOM (mutable)

    [Fact]
    public void JsonSet_CreateAndAdd()
    {
        var set = new JsonSet();
        set.Add(JsonValue.Create(1));
        set.Add(JsonValue.Create(2));
        Assert.Equal(2, set.Count);
    }

    [Fact]
    public void JsonSet_Remove()
    {
        var node = JsonValue.Create(42);
        var set = new JsonSet(node);
        Assert.Equal(1, set.Count);
        Assert.True(set.Remove(node));
        Assert.Equal(0, set.Count);
    }

    [Fact]
    public void JsonSet_Clear()
    {
        var set = new JsonSet(JsonValue.Create(1), JsonValue.Create(2));
        Assert.Equal(2, set.Count);
        set.Clear();
        Assert.Equal(0, set.Count);
    }

    [Fact]
    public void JsonSet_Contains()
    {
        var node = JsonValue.Create("hello");
        var set = new JsonSet(node);
        Assert.True(set.Contains(node));
        Assert.False(set.Contains(JsonValue.Create("world")));
    }

    [Fact]
    public void JsonSet_WriteTo()
    {
        var set = new JsonSet(JsonValue.Create(1), JsonValue.Create(2));
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        set.WriteTo(writer);
        writer.Flush();

        var output = System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
        Assert.Equal("{1,2}", output);
    }

    [Fact]
    public void JsonSet_ParseFromRdn()
    {
        var node = JsonNode.Parse("Set{1, 2, 3}");
        Assert.IsType<JsonSet>(node);
        var set = (JsonSet)node!;
        Assert.Equal(3, set.Count);
    }

    [Fact]
    public void JsonSet_GetValueKind()
    {
        var set = new JsonSet(JsonValue.Create(1));
        Assert.Equal(JsonValueKind.Set, set.GetValueKind());
    }

    #endregion

    #region 7. Edge cases

    [Fact]
    public void Reader_SetWithNestedObject()
    {
        var bytes = "Set{{\"a\": 1}, {\"b\": 2}}"u8.ToArray();
        var reader = new Utf8JsonReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartSet, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartObject, reader.TokenType);
    }

    [Fact]
    public void Reader_ObjectContainingSet()
    {
        var bytes = "{\"tags\": Set{\"a\", \"b\"}}"u8.ToArray();
        var reader = new Utf8JsonReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.PropertyName, reader.TokenType);
        Assert.Equal("tags", reader.GetString());

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartSet, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal("a", reader.GetString());
        Assert.True(reader.Read());
        Assert.Equal("b", reader.GetString());

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.EndSet, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.EndObject, reader.TokenType);
    }

    [Fact]
    public void Reader_ArrayContainingSet()
    {
        var bytes = "[Set{1}, Set{2}]"u8.ToArray();
        var reader = new Utf8JsonReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartArray, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartSet, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.EndSet, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartSet, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(2, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.EndSet, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.EndArray, reader.TokenType);
    }

    [Fact]
    public void Serialize_ObjectWithHashSetField()
    {
        var obj = new ObjectWithSet { Name = "test", Tags = new HashSet<string> { "a", "b" } };
        var rdn = JsonSerializer.Serialize(obj);
        Assert.Contains("\"Name\":\"test\"", rdn);
        // Non-empty set uses implicit syntax
        Assert.Contains("\"Tags\":{", rdn);
        Assert.DoesNotContain("\"Tags\":Set{", rdn);
    }

    [Fact]
    public void Deserialize_ObjectWithHashSetField()
    {
        var rdn = "{\"Name\":\"test\",\"Tags\":Set{\"a\",\"b\"}}";
        var obj = JsonSerializer.Deserialize<ObjectWithSet>(rdn);
        Assert.NotNull(obj);
        Assert.Equal("test", obj!.Name);
        Assert.NotNull(obj.Tags);
        Assert.Equal(2, obj.Tags!.Count);
        Assert.Contains("a", obj.Tags);
        Assert.Contains("b", obj.Tags);
    }

    private class ObjectWithSet
    {
        public string? Name { get; set; }
        public HashSet<string>? Tags { get; set; }
    }

    #endregion

    #region 8. Error cases

    [Fact]
    public void Reader_MismatchedBraces_ArrayClosedWithBrace_Throws()
    {
        var bytes = "[1, 2}"u8.ToArray();
        Assert.ThrowsAny<JsonException>(() =>
        {
            var reader = new Utf8JsonReader(bytes);
            while (reader.Read()) { }
        });
    }

    [Fact]
    public void Reader_MismatchedBraces_SetClosedWithBracket_Throws()
    {
        var bytes = "Set{1, 2]"u8.ToArray();
        Assert.ThrowsAny<JsonException>(() =>
        {
            var reader = new Utf8JsonReader(bytes);
            while (reader.Read()) { }
        });
    }

    #endregion
}
