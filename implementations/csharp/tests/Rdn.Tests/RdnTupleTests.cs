using Rdn;
using Rdn.Serialization;
using Xunit;

namespace Rdn.Tests;

public class RdnTupleTests
{
    #region 1. Utf8JsonReader — Basic tuple parsing

    [Fact]
    public void Reader_EmptyTuple()
    {
        var bytes = "()"u8.ToArray();
        var reader = new Utf8JsonReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartArray, reader.TokenType);
        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.EndArray, reader.TokenType);
        Assert.False(reader.Read());
    }

    [Fact]
    public void Reader_SingleElement()
    {
        var bytes = "(1)"u8.ToArray();
        var reader = new Utf8JsonReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartArray, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.Number, reader.TokenType);
        Assert.Equal(1, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.EndArray, reader.TokenType);
        Assert.False(reader.Read());
    }

    [Fact]
    public void Reader_MultipleElements()
    {
        var bytes = "(1, \"two\", true)"u8.ToArray();
        var reader = new Utf8JsonReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartArray, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.Number, reader.TokenType);
        Assert.Equal(1, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.String, reader.TokenType);
        Assert.Equal("two", reader.GetString());

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.True, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.EndArray, reader.TokenType);
        Assert.False(reader.Read());
    }

    [Fact]
    public void Reader_NestedTuples()
    {
        var bytes = "((1, 2), (3, 4))"u8.ToArray();
        var reader = new Utf8JsonReader(bytes);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartArray, reader.TokenType); // outer (

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartArray, reader.TokenType); // inner (1, 2)
        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32());
        Assert.True(reader.Read());
        Assert.Equal(2, reader.GetInt32());
        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.EndArray, reader.TokenType); // inner )

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartArray, reader.TokenType); // inner (3, 4)
        Assert.True(reader.Read());
        Assert.Equal(3, reader.GetInt32());
        Assert.True(reader.Read());
        Assert.Equal(4, reader.GetInt32());
        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.EndArray, reader.TokenType); // inner )

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.EndArray, reader.TokenType); // outer )
        Assert.False(reader.Read());
    }

    #endregion

    #region 2. Tuple interop with other containers

    [Fact]
    public void Reader_TupleInsideArray()
    {
        var bytes = "[(1, 2)]"u8.ToArray();
        var reader = new Utf8JsonReader(bytes);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartArray, reader.TokenType); // [

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartArray, reader.TokenType); // (

        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32());
        Assert.True(reader.Read());
        Assert.Equal(2, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.EndArray, reader.TokenType); // )

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.EndArray, reader.TokenType); // ]
    }

    [Fact]
    public void Reader_ArrayInsideTuple()
    {
        var bytes = "([1, 2])"u8.ToArray();
        var reader = new Utf8JsonReader(bytes);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartArray, reader.TokenType); // (

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartArray, reader.TokenType); // [

        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32());
        Assert.True(reader.Read());
        Assert.Equal(2, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.EndArray, reader.TokenType); // ]

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.EndArray, reader.TokenType); // )
    }

    [Fact]
    public void Reader_TupleInsideObject()
    {
        var bytes = "{\"t\": (1, 2)}"u8.ToArray();
        var reader = new Utf8JsonReader(bytes);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.PropertyName, reader.TokenType);
        Assert.Equal("t", reader.GetString());

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartArray, reader.TokenType); // (

        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32());
        Assert.True(reader.Read());
        Assert.Equal(2, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.EndArray, reader.TokenType); // )

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.EndObject, reader.TokenType);
    }

    [Fact]
    public void Reader_TupleWithMixedTypes()
    {
        var bytes = "(1, \"hello\", true, null, 3.14)"u8.ToArray();
        var reader = new Utf8JsonReader(bytes);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartArray, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.Number, reader.TokenType);
        Assert.Equal(1, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.String, reader.TokenType);
        Assert.Equal("hello", reader.GetString());

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.True, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.Null, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.Number, reader.TokenType);
        Assert.Equal(3.14, reader.GetDouble());

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.EndArray, reader.TokenType);
    }

    [Fact]
    public void Reader_TupleWithRdnDateTime()
    {
        var bytes = "(@2024-01-15, @12:30:00)"u8.ToArray();
        var reader = new Utf8JsonReader(bytes);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartArray, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.RdnDateTime, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.RdnTimeOnly, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.EndArray, reader.TokenType);
    }

    #endregion

    #region 3. JsonDocument — Tuple parsing

    [Fact]
    public void Document_ParseTuple()
    {
        using var doc = JsonDocument.Parse("(1, \"two\", true)");
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(3, doc.RootElement.GetArrayLength());
        Assert.Equal(1, doc.RootElement[0].GetInt32());
        Assert.Equal("two", doc.RootElement[1].GetString());
        Assert.True(doc.RootElement[2].GetBoolean());
    }

    [Fact]
    public void Document_ParseEmptyTuple()
    {
        using var doc = JsonDocument.Parse("()");
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(0, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public void Document_TupleInObject()
    {
        using var doc = JsonDocument.Parse("{\"t\": (1, 2)}");
        var t = doc.RootElement.GetProperty("t");
        Assert.Equal(JsonValueKind.Array, t.ValueKind);
        Assert.Equal(2, t.GetArrayLength());
        Assert.Equal(1, t[0].GetInt32());
        Assert.Equal(2, t[1].GetInt32());
    }

    #endregion

    #region 4. Serialization — Tuples deserialize as arrays

    [Fact]
    public void Deserialize_TupleToIntArray()
    {
        var rdn = "(1, 2, 3)";
        var arr = JsonSerializer.Deserialize<int[]>(rdn);

        Assert.NotNull(arr);
        Assert.Equal(3, arr!.Length);
        Assert.Equal(1, arr[0]);
        Assert.Equal(2, arr[1]);
        Assert.Equal(3, arr[2]);
    }

    [Fact]
    public void Deserialize_TupleToList()
    {
        var rdn = "(\"a\", \"b\", \"c\")";
        var list = JsonSerializer.Deserialize<System.Collections.Generic.List<string>>(rdn);

        Assert.NotNull(list);
        Assert.Equal(3, list!.Count);
        Assert.Equal("a", list[0]);
        Assert.Equal("b", list[1]);
        Assert.Equal("c", list[2]);
    }

    [Fact]
    public void Deserialize_EmptyTupleToArray()
    {
        var rdn = "()";
        var arr = JsonSerializer.Deserialize<int[]>(rdn);

        Assert.NotNull(arr);
        Assert.Empty(arr!);
    }

    #endregion

    #region 5. Error cases

    [Fact]
    public void Reader_MismatchedParen_CloseBracket_Throws()
    {
        var bytes = "(1, 2]"u8.ToArray();
        Assert.ThrowsAny<JsonException>(() =>
        {
            var reader = new Utf8JsonReader(bytes);
            while (reader.Read()) { }
        });
    }

    [Fact]
    public void Reader_MismatchedParen_CloseBrace_Throws()
    {
        var bytes = "(1, 2}"u8.ToArray();
        Assert.ThrowsAny<JsonException>(() =>
        {
            var reader = new Utf8JsonReader(bytes);
            while (reader.Read()) { }
        });
    }

    [Fact]
    public void Reader_MismatchedBracket_CloseParen_Throws()
    {
        var bytes = "[1, 2)"u8.ToArray();
        Assert.ThrowsAny<JsonException>(() =>
        {
            var reader = new Utf8JsonReader(bytes);
            while (reader.Read()) { }
        });
    }

    [Fact]
    public void Reader_UnclosedTuple_Throws()
    {
        var bytes = "(1, 2"u8.ToArray();
        Assert.ThrowsAny<JsonException>(() =>
        {
            var reader = new Utf8JsonReader(bytes);
            while (reader.Read()) { }
        });
    }

    [Fact]
    public void Reader_TrailingComma_NoOption_Throws()
    {
        var bytes = "(1, 2,)"u8.ToArray();
        Assert.ThrowsAny<JsonException>(() =>
        {
            var reader = new Utf8JsonReader(bytes);
            while (reader.Read()) { }
        });
    }

    [Fact]
    public void Reader_TrailingComma_WithOption_Succeeds()
    {
        var bytes = "(1, 2,)"u8.ToArray();
        var options = new JsonReaderOptions { AllowTrailingCommas = true };
        var reader = new Utf8JsonReader(bytes, options);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartArray, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32());
        Assert.True(reader.Read());
        Assert.Equal(2, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.EndArray, reader.TokenType);
        Assert.False(reader.Read());
    }

    [Fact]
    public void Reader_BareParen_Throws()
    {
        var bytes = "("u8.ToArray();
        Assert.ThrowsAny<JsonException>(() =>
        {
            var reader = new Utf8JsonReader(bytes);
            while (reader.Read()) { }
        });
    }

    #endregion

    #region 6. Conformance test suite compatibility

    [Fact]
    public void Conformance_ValidTuple()
    {
        // Matches test-suite/valid/tuple.rdn → tuple.expected.json
        var rdn = "(1, \"two\", true)";
        using var doc = JsonDocument.Parse(rdn);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(3, doc.RootElement.GetArrayLength());
        Assert.Equal(1, doc.RootElement[0].GetInt32());
        Assert.Equal("two", doc.RootElement[1].GetString());
        Assert.True(doc.RootElement[2].GetBoolean());
    }

    [Fact]
    public void Conformance_EmptyContainersTuple()
    {
        // Matches test-suite/roundtrip/empty-containers.rdn "tuple": ()
        var rdn = "{\"tuple\": ()}";
        using var doc = JsonDocument.Parse(rdn);
        var tuple = doc.RootElement.GetProperty("tuple");
        Assert.Equal(JsonValueKind.Array, tuple.ValueKind);
        Assert.Equal(0, tuple.GetArrayLength());
    }

    #endregion

    #region 7. Serialization — Tuples serialize as ()

    [Fact]
    public void Serialize_ValueTuple2_WritesParens()
    {
        var tuple = (1, 2);
        var rdn = JsonSerializer.Serialize(tuple);
        Assert.Equal("(1,2)", rdn);
    }

    [Fact]
    public void Serialize_ValueTuple3_WritesParens()
    {
        var tuple = (1, "two", true);
        var rdn = JsonSerializer.Serialize(tuple);
        Assert.Equal("(1,\"two\",true)", rdn);
    }

    [Fact]
    public void Serialize_ReferenceTuple2_WritesParens()
    {
        var tuple = Tuple.Create(1, 2);
        var rdn = JsonSerializer.Serialize(tuple);
        Assert.Equal("(1,2)", rdn);
    }

    [Fact]
    public void Serialize_ReferenceTuple3_WritesParens()
    {
        var tuple = Tuple.Create(1, "two", true);
        var rdn = JsonSerializer.Serialize(tuple);
        Assert.Equal("(1,\"two\",true)", rdn);
    }

    [Fact]
    public void Serialize_ValueTuple1_WritesParens()
    {
        var tuple = ValueTuple.Create(42);
        var rdn = JsonSerializer.Serialize(tuple);
        Assert.Equal("(42)", rdn);
    }

    [Fact]
    public void Serialize_ValueTuple7_WritesParens()
    {
        var tuple = (1, 2, 3, 4, 5, 6, 7);
        var rdn = JsonSerializer.Serialize(tuple);
        Assert.Equal("(1,2,3,4,5,6,7)", rdn);
    }

    [Fact]
    public void Serialize_ValueTupleNested_WritesParens()
    {
        var tuple = ((1, 2), (3, 4));
        var rdn = JsonSerializer.Serialize(tuple);
        Assert.Equal("((1,2),(3,4))", rdn);
    }

    [Fact]
    public void Serialize_TupleInObject_WritesParens()
    {
        var obj = new { point = (1, 2) };
        var rdn = JsonSerializer.Serialize(obj);
        Assert.Equal("{\"point\":(1,2)}", rdn);
    }

    [Fact]
    public void Roundtrip_ValueTuple2()
    {
        var original = (42, "hello");
        var rdn = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<(int, string)>(rdn);
        Assert.Equal(original, deserialized);
    }

    [Fact]
    public void Roundtrip_ReferenceTuple2()
    {
        var original = Tuple.Create(42, "hello");
        var rdn = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<Tuple<int, string>>(rdn);
        Assert.NotNull(deserialized);
        Assert.Equal(original.Item1, deserialized!.Item1);
        Assert.Equal(original.Item2, deserialized.Item2);
    }

    #endregion
}
