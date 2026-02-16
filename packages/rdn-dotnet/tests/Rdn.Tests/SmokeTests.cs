using Rdn;
using Rdn.Nodes;
using Rdn.Serialization;
using Xunit;

namespace Rdn.Tests;

public class SmokeTests
{
    private record Person(string Name, int Age, string[] Hobbies);

    [Fact]
    public void SerializeAndDeserialize_SimpleObject()
    {
        var person = new Person("Alice", 30, ["Reading", "Hiking"]);
        string rdn = RdnSerializer.Serialize(person);
        var deserialized = RdnSerializer.Deserialize<Person>(rdn);

        Assert.NotNull(deserialized);
        Assert.Equal("Alice", deserialized.Name);
        Assert.Equal(30, deserialized.Age);
        Assert.Equal(["Reading", "Hiking"], deserialized.Hobbies);
    }

    [Fact]
    public void SerializeAndDeserialize_WithOptions()
    {
        var options = new RdnSerializerOptions { PropertyNamingPolicy = RdnNamingPolicy.CamelCase, WriteIndented = true };

        var person = new Person("Bob", 25, ["Gaming"]);
        string rdn = RdnSerializer.Serialize(person, options);

        Assert.Contains("\"name\":", rdn);
        Assert.Contains("\"age\":", rdn);

        var deserialized = RdnSerializer.Deserialize<Person>(rdn, options);
        Assert.NotNull(deserialized);
        Assert.Equal("Bob", deserialized.Name);
    }

    [Fact]
    public void Parse_RdnDocument()
    {
        string rdn = """{"key": "value", "number": 42, "array": [1, 2, 3]}""";
        using var doc = RdnDocument.Parse(rdn);

        Assert.Equal("value", doc.RootElement.GetProperty("key").GetString());
        Assert.Equal(42, doc.RootElement.GetProperty("number").GetInt32());
        Assert.Equal(3, doc.RootElement.GetProperty("array").GetArrayLength());
    }

    [Fact]
    public void RdnNode_Manipulation()
    {
        var obj = new RdnObject { ["name"] = "Charlie", ["age"] = 35 };
        obj["city"] = "NYC";

        Assert.Equal("Charlie", obj["name"]!.GetValue<string>());
        Assert.Equal(35, obj["age"]!.GetValue<int>());
        Assert.Equal("NYC", obj["city"]!.GetValue<string>());
    }

    [Fact]
    public void Utf8RdnWriter_And_Reader()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8RdnWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("hello", "world");
            writer.WriteNumber("count", 123);
            writer.WriteEndObject();
        }

        var reader = new Utf8RdnReader(stream.ToArray());

        Assert.True(reader.Read()); // StartObject
        Assert.Equal(RdnTokenType.StartObject, reader.TokenType);
        Assert.True(reader.Read()); // PropertyName "hello"
        Assert.Equal("hello", reader.GetString());
        Assert.True(reader.Read()); // String "world"
        Assert.Equal("world", reader.GetString());
        Assert.True(reader.Read()); // PropertyName "count"
        Assert.Equal("count", reader.GetString());
        Assert.True(reader.Read()); // Number 123
        Assert.Equal(123, reader.GetInt32());
        Assert.True(reader.Read()); // EndObject
        Assert.Equal(RdnTokenType.EndObject, reader.TokenType);
    }

    [Fact]
    public void Serialize_NestedComplexObject()
    {
        var data = new Dictionary<string, object> { ["list"] = new[] { 1, 2, 3 }, ["nested"] = new { x = 1, y = 2 }, ["null_val"] = null! };

        string rdn = RdnSerializer.Serialize(data);
        Assert.Contains("\"list\" => ", rdn);
        Assert.Contains("\"nested\" => ", rdn);
        Assert.Contains("\"null_val\" => null", rdn);
    }

    [Fact]
    public void Deserialize_NumberTypes()
    {
        string rdn = """{"i": 42, "d": 3.14, "l": 9999999999}""";
        using var doc = RdnDocument.Parse(rdn);

        Assert.Equal(42, doc.RootElement.GetProperty("i").GetInt32());
        Assert.Equal(3.14, doc.RootElement.GetProperty("d").GetDouble());
        Assert.Equal(9999999999L, doc.RootElement.GetProperty("l").GetInt64());
    }
}
