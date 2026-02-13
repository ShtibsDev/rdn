# RDN C# Implementation

A fork of `System.Text.Json` from [dotnet/runtime](https://github.com/dotnet/runtime), renamed to `Rdn` as the foundation for adding RDN (Rich Data Notation) support.

## Project Structure

```
implementations/csharp/
├── Rdn.sln
├── THIRD_PARTY_NOTICE.md
├── src/
│   ├── Rdn/           # Forked System.Text.Json (renamed namespace)
│   └── Rdn.Encodings.Web/  # Forked System.Text.Encodings.Web (renamed namespace)
├── tests/
│   └── Rdn.Tests/     # Smoke tests
└── benchmarks/
    └── Rdn.Benchmarks/          # BenchmarkDotNet: Rdn vs System.Text.Json
```

## Build & Test

```bash
cd implementations/csharp
dotnet build Rdn.sln
dotnet test
```

## Benchmarks

```bash
cd implementations/csharp
dotnet run -c Release --project benchmarks/Rdn.Benchmarks
```

## API

Mirrors `System.Text.Json` exactly under the `Rdn` namespace:

```csharp
using Rdn;

// Serialize / Deserialize
string json = JsonSerializer.Serialize(new { Name = "Alice", Age = 30 });
var obj = JsonSerializer.Deserialize<Person>(json);

// Document API
using var doc = JsonDocument.Parse(json);

// Writer / Reader
var writer = new Utf8JsonWriter(stream);
var reader = new Utf8JsonReader(data);
```

## Roadmap

- Extend parser/serializer with RDN types: `DateTime`, `BigInteger`, `Regex`, `byte[]`, `Map`, `Set`, `Tuple`, `TimeOnly`, `Duration`
- Conform to the shared test suite in `test-suite/`
