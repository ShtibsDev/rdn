using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

BenchmarkRunner.Run<JsonBenchmarks>();

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
