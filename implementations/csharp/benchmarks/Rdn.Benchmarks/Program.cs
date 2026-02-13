using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

BenchmarkRunner.Run<JsonBenchmarks>();
BenchmarkRunner.Run<RdnDateTimeBenchmarks>();
BenchmarkRunner.Run<RdnSetBenchmarks>();

[MemoryDiagnoser]
public class JsonBenchmarks
{
    public record Person(string Name, int Age, string Email, string[] Tags);

    private static readonly Person TestPerson = new("Alice Smith", 30, "alice@example.com", ["developer", "reader", "hiker"]);
    private static readonly string TestJson = System.Text.Json.JsonSerializer.Serialize(TestPerson);
    private static readonly byte[] TestJsonUtf8 = System.Text.Encoding.UTF8.GetBytes(TestJson);

    // --- Serialization ---

    [Benchmark(Baseline = true)]
    public string SystemTextJson_Serialize()
        => System.Text.Json.JsonSerializer.Serialize(TestPerson);

    [Benchmark]
    public string RdnTextJson_Serialize()
        => Rdn.JsonSerializer.Serialize(TestPerson);

    // --- Deserialization ---

    [Benchmark]
    public Person? SystemTextJson_Deserialize()
        => System.Text.Json.JsonSerializer.Deserialize<Person>(TestJson);

    [Benchmark]
    public Person? RdnTextJson_Deserialize()
        => Rdn.JsonSerializer.Deserialize<Person>(TestJson);

    // --- Document Parsing ---

    [Benchmark]
    public int SystemTextJson_ParseDocument()
    {
        using var doc = System.Text.Json.JsonDocument.Parse(TestJson);
        return doc.RootElement.GetProperty("Age").GetInt32();
    }

    [Benchmark]
    public int RdnTextJson_ParseDocument()
    {
        using var doc = Rdn.JsonDocument.Parse(TestJson);
        return doc.RootElement.GetProperty("Age").GetInt32();
    }
}

[MemoryDiagnoser]
public class RdnDateTimeBenchmarks
{
    public record EventRecord(string Name, DateTime CreatedAt, DateTime UpdatedAt);

    private static readonly byte[] FullIsoBytes = System.Text.Encoding.UTF8.GetBytes("{\"d\":@2024-01-15T10:30:00.123Z}");
    private static readonly byte[] UnixTimestampBytes = System.Text.Encoding.UTF8.GetBytes("{\"d\":@1705312200}");
    private static readonly byte[] TimeOnlyBytes = System.Text.Encoding.UTF8.GetBytes("{\"t\":@14:30:00}");

    private static readonly DateTime TestDateTime = new(2024, 1, 15, 10, 30, 0, 123, DateTimeKind.Utc);
    private static readonly TimeOnly TestTimeOnly = new(14, 30, 0);
    private static readonly EventRecord TestEvent = new("deploy", new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc), new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc));

    // --- Reader ---

    [Benchmark]
    public void ParseRdnDateTime_FullIso()
    {
        var reader = new Rdn.Utf8JsonReader(FullIsoBytes);
        while (reader.Read()) { }
    }

    [Benchmark]
    public void ParseRdnDateTime_UnixTimestamp()
    {
        var reader = new Rdn.Utf8JsonReader(UnixTimestampBytes);
        while (reader.Read()) { }
    }

    [Benchmark]
    public void ParseRdnTimeOnly()
    {
        var reader = new Rdn.Utf8JsonReader(TimeOnlyBytes);
        while (reader.Read()) { }
    }

    // --- Writer ---

    [Benchmark]
    public byte[] WriteRdnDateTime()
    {
        var buffer = new System.IO.MemoryStream();
        using (var writer = new Rdn.Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("d");
            writer.WriteRdnDateTimeValue(TestDateTime);
            writer.WriteEndObject();
        }
        return buffer.ToArray();
    }

    [Benchmark]
    public byte[] WriteJsonStringDateTime()
    {
        var buffer = new System.IO.MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("d", TestDateTime);
            writer.WriteEndObject();
        }
        return buffer.ToArray();
    }

    // --- Serializer ---

    [Benchmark]
    public string SerializeObjectWithDates()
        => Rdn.JsonSerializer.Serialize(TestEvent);
}

[MemoryDiagnoser]
public class RdnSetBenchmarks
{
    private static readonly byte[] ExplicitSetBytes = System.Text.Encoding.UTF8.GetBytes("Set{1,2,3,4,5}");
    private static readonly byte[] ImplicitSetNonStringBytes = System.Text.Encoding.UTF8.GetBytes("{1,2,3,4,5}");
    private static readonly byte[] ImplicitSetStringBytes = System.Text.Encoding.UTF8.GetBytes("{\"a\",\"b\",\"c\"}");
    private static readonly byte[] ObjectBytes = System.Text.Encoding.UTF8.GetBytes("{\"key\":1}");
    private static readonly HashSet<int> TestHashSet = new() { 1, 2, 3, 4, 5 };
    private static readonly string SerializedHashSet = Rdn.JsonSerializer.Serialize(TestHashSet);

    [Benchmark]
    public void ParseExplicitSet()
    {
        var reader = new Rdn.Utf8JsonReader(ExplicitSetBytes);
        while (reader.Read()) { }
    }

    [Benchmark]
    public void ParseImplicitSet_NonString()
    {
        var reader = new Rdn.Utf8JsonReader(ImplicitSetNonStringBytes);
        while (reader.Read()) { }
    }

    [Benchmark]
    public void ParseImplicitSet_String()
    {
        var reader = new Rdn.Utf8JsonReader(ImplicitSetStringBytes);
        while (reader.Read()) { }
    }

    [Benchmark]
    public void ParseBraceDisambiguation_Object()
    {
        var reader = new Rdn.Utf8JsonReader(ObjectBytes);
        while (reader.Read()) { }
    }

    [Benchmark]
    public string SerializeHashSet()
        => Rdn.JsonSerializer.Serialize(TestHashSet);

    [Benchmark]
    public HashSet<int>? DeserializeHashSet()
        => Rdn.JsonSerializer.Deserialize<HashSet<int>>(SerializedHashSet);
}
