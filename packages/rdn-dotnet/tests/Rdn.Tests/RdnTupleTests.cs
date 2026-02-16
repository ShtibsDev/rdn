using Rdn;
using Rdn.Serialization;
using Xunit;

namespace Rdn.Tests;

public class RdnTupleTests
{
    #region 1. Utf8RdnReader — Basic tuple parsing

    [Fact]
    public void Reader_EmptyTuple()
    {
        var bytes = "()"u8.ToArray();
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartArray, reader.TokenType);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndArray, reader.TokenType);
        Assert.False(reader.Read());
    }

    [Fact]
    public void Reader_SingleElement()
    {
        var bytes = "(1)"u8.ToArray();
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartArray, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.Number, reader.TokenType);
        Assert.Equal(1, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndArray, reader.TokenType);
        Assert.False(reader.Read());
    }

    [Fact]
    public void Reader_MultipleElements()
    {
        var bytes = "(1, \"two\", true)"u8.ToArray();
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartArray, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.Number, reader.TokenType);
        Assert.Equal(1, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.String, reader.TokenType);
        Assert.Equal("two", reader.GetString());

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.True, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndArray, reader.TokenType);
        Assert.False(reader.Read());
    }

    [Fact]
    public void Reader_NestedTuples()
    {
        var bytes = "((1, 2), (3, 4))"u8.ToArray();
        var reader = new Utf8RdnReader(bytes);

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartArray, reader.TokenType); // outer (

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartArray, reader.TokenType); // inner (1, 2)
        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32());
        Assert.True(reader.Read());
        Assert.Equal(2, reader.GetInt32());
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndArray, reader.TokenType); // inner )

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartArray, reader.TokenType); // inner (3, 4)
        Assert.True(reader.Read());
        Assert.Equal(3, reader.GetInt32());
        Assert.True(reader.Read());
        Assert.Equal(4, reader.GetInt32());
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndArray, reader.TokenType); // inner )

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndArray, reader.TokenType); // outer )
        Assert.False(reader.Read());
    }

    #endregion

    #region 2. Tuple interop with other containers

    [Fact]
    public void Reader_TupleInsideArray()
    {
        var bytes = "[(1, 2)]"u8.ToArray();
        var reader = new Utf8RdnReader(bytes);

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartArray, reader.TokenType); // [

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartArray, reader.TokenType); // (

        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32());
        Assert.True(reader.Read());
        Assert.Equal(2, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndArray, reader.TokenType); // )

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndArray, reader.TokenType); // ]
    }

    [Fact]
    public void Reader_ArrayInsideTuple()
    {
        var bytes = "([1, 2])"u8.ToArray();
        var reader = new Utf8RdnReader(bytes);

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartArray, reader.TokenType); // (

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartArray, reader.TokenType); // [

        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32());
        Assert.True(reader.Read());
        Assert.Equal(2, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndArray, reader.TokenType); // ]

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndArray, reader.TokenType); // )
    }

    [Fact]
    public void Reader_TupleInsideObject()
    {
        var bytes = "{\"t\": (1, 2)}"u8.ToArray();
        var reader = new Utf8RdnReader(bytes);

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartObject, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.PropertyName, reader.TokenType);
        Assert.Equal("t", reader.GetString());

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartArray, reader.TokenType); // (

        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32());
        Assert.True(reader.Read());
        Assert.Equal(2, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndArray, reader.TokenType); // )

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndObject, reader.TokenType);
    }

    [Fact]
    public void Reader_TupleWithMixedTypes()
    {
        var bytes = "(1, \"hello\", true, null, 3.14)"u8.ToArray();
        var reader = new Utf8RdnReader(bytes);

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartArray, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.Number, reader.TokenType);
        Assert.Equal(1, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.String, reader.TokenType);
        Assert.Equal("hello", reader.GetString());

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.True, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.Null, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.Number, reader.TokenType);
        Assert.Equal(3.14, reader.GetDouble());

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndArray, reader.TokenType);
    }

    [Fact]
    public void Reader_TupleWithRdnDateTime()
    {
        var bytes = "(@2024-01-15, @12:30:00)"u8.ToArray();
        var reader = new Utf8RdnReader(bytes);

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartArray, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.RdnDateTime, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.RdnTimeOnly, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndArray, reader.TokenType);
    }

    #endregion

    #region 3. RdnDocument — Tuple parsing

    [Fact]
    public void Document_ParseTuple()
    {
        using var doc = RdnDocument.Parse("(1, \"two\", true)");
        Assert.Equal(RdnValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(3, doc.RootElement.GetArrayLength());
        Assert.Equal(1, doc.RootElement[0].GetInt32());
        Assert.Equal("two", doc.RootElement[1].GetString());
        Assert.True(doc.RootElement[2].GetBoolean());
    }

    [Fact]
    public void Document_ParseEmptyTuple()
    {
        using var doc = RdnDocument.Parse("()");
        Assert.Equal(RdnValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(0, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public void Document_TupleInObject()
    {
        using var doc = RdnDocument.Parse("{\"t\": (1, 2)}");
        var t = doc.RootElement.GetProperty("t");
        Assert.Equal(RdnValueKind.Array, t.ValueKind);
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
        var arr = RdnSerializer.Deserialize<int[]>(rdn);

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
        var list = RdnSerializer.Deserialize<System.Collections.Generic.List<string>>(rdn);

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
        var arr = RdnSerializer.Deserialize<int[]>(rdn);

        Assert.NotNull(arr);
        Assert.Empty(arr!);
    }

    #endregion

    #region 5. Error cases

    [Fact]
    public void Reader_MismatchedParen_CloseBracket_Throws()
    {
        var bytes = "(1, 2]"u8.ToArray();
        Assert.ThrowsAny<RdnException>(() =>
        {
            var reader = new Utf8RdnReader(bytes);
            while (reader.Read()) { }
        });
    }

    [Fact]
    public void Reader_MismatchedParen_CloseBrace_Throws()
    {
        var bytes = "(1, 2}"u8.ToArray();
        Assert.ThrowsAny<RdnException>(() =>
        {
            var reader = new Utf8RdnReader(bytes);
            while (reader.Read()) { }
        });
    }

    [Fact]
    public void Reader_MismatchedBracket_CloseParen_Throws()
    {
        var bytes = "[1, 2)"u8.ToArray();
        Assert.ThrowsAny<RdnException>(() =>
        {
            var reader = new Utf8RdnReader(bytes);
            while (reader.Read()) { }
        });
    }

    [Fact]
    public void Reader_UnclosedTuple_Throws()
    {
        var bytes = "(1, 2"u8.ToArray();
        Assert.ThrowsAny<RdnException>(() =>
        {
            var reader = new Utf8RdnReader(bytes);
            while (reader.Read()) { }
        });
    }

    [Fact]
    public void Reader_TrailingComma_NoOption_Throws()
    {
        var bytes = "(1, 2,)"u8.ToArray();
        Assert.ThrowsAny<RdnException>(() =>
        {
            var reader = new Utf8RdnReader(bytes);
            while (reader.Read()) { }
        });
    }

    [Fact]
    public void Reader_TrailingComma_WithOption_Succeeds()
    {
        var bytes = "(1, 2,)"u8.ToArray();
        var options = new RdnReaderOptions { AllowTrailingCommas = true };
        var reader = new Utf8RdnReader(bytes, options);

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.StartArray, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32());
        Assert.True(reader.Read());
        Assert.Equal(2, reader.GetInt32());

        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.EndArray, reader.TokenType);
        Assert.False(reader.Read());
    }

    [Fact]
    public void Reader_BareParen_Throws()
    {
        var bytes = "("u8.ToArray();
        Assert.ThrowsAny<RdnException>(() =>
        {
            var reader = new Utf8RdnReader(bytes);
            while (reader.Read()) { }
        });
    }

    #endregion

    #region 6. Conformance test suite compatibility

    [Fact]
    public void Conformance_ValidTuple()
    {
        // Matches test-suite/valid/tuple.rdn → tuple.expected.rdn
        var rdn = "(1, \"two\", true)";
        using var doc = RdnDocument.Parse(rdn);
        Assert.Equal(RdnValueKind.Array, doc.RootElement.ValueKind);
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
        using var doc = RdnDocument.Parse(rdn);
        var tuple = doc.RootElement.GetProperty("tuple");
        Assert.Equal(RdnValueKind.Array, tuple.ValueKind);
        Assert.Equal(0, tuple.GetArrayLength());
    }

    #endregion

    #region 7. Serialization — Tuples serialize as ()

    [Fact]
    public void Serialize_ValueTuple2_WritesParens()
    {
        var tuple = (1, 2);
        var rdn = RdnSerializer.Serialize(tuple);
        Assert.Equal("(1,2)", rdn);
    }

    [Fact]
    public void Serialize_ValueTuple3_WritesParens()
    {
        var tuple = (1, "two", true);
        var rdn = RdnSerializer.Serialize(tuple);
        Assert.Equal("(1,\"two\",true)", rdn);
    }

    [Fact]
    public void Serialize_ReferenceTuple2_WritesParens()
    {
        var tuple = Tuple.Create(1, 2);
        var rdn = RdnSerializer.Serialize(tuple);
        Assert.Equal("(1,2)", rdn);
    }

    [Fact]
    public void Serialize_ReferenceTuple3_WritesParens()
    {
        var tuple = Tuple.Create(1, "two", true);
        var rdn = RdnSerializer.Serialize(tuple);
        Assert.Equal("(1,\"two\",true)", rdn);
    }

    [Fact]
    public void Serialize_ValueTuple1_WritesParens()
    {
        var tuple = ValueTuple.Create(42);
        var rdn = RdnSerializer.Serialize(tuple);
        Assert.Equal("(42)", rdn);
    }

    [Fact]
    public void Serialize_ValueTuple7_WritesParens()
    {
        var tuple = (1, 2, 3, 4, 5, 6, 7);
        var rdn = RdnSerializer.Serialize(tuple);
        Assert.Equal("(1,2,3,4,5,6,7)", rdn);
    }

    [Fact]
    public void Serialize_ValueTupleNested_WritesParens()
    {
        var tuple = ((1, 2), (3, 4));
        var rdn = RdnSerializer.Serialize(tuple);
        Assert.Equal("((1,2),(3,4))", rdn);
    }

    [Fact]
    public void Serialize_TupleInObject_WritesParens()
    {
        var obj = new { point = (1, 2) };
        var rdn = RdnSerializer.Serialize(obj);
        Assert.Equal("{\"point\":(1,2)}", rdn);
    }

    [Fact]
    public void Roundtrip_ValueTuple2()
    {
        var original = (42, "hello");
        var rdn = RdnSerializer.Serialize(original);
        var deserialized = RdnSerializer.Deserialize<(int, string)>(rdn);
        Assert.Equal(original, deserialized);
    }

    [Fact]
    public void Roundtrip_ReferenceTuple2()
    {
        var original = Tuple.Create(42, "hello");
        var rdn = RdnSerializer.Serialize(original);
        var deserialized = RdnSerializer.Deserialize<Tuple<int, string>>(rdn);
        Assert.NotNull(deserialized);
        Assert.Equal(original.Item1, deserialized!.Item1);
        Assert.Equal(original.Item2, deserialized.Item2);
    }

    #endregion
}
