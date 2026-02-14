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

## RDN Date/Time Support

RDN extends JSON with native `@`-prefixed literals for date/time values — no quotes, no string parsing at the application layer.

| Type | Syntax | Example |
|------|--------|---------|
| DateTime | `@YYYY-MM-DDTHH:mm:ss.sssZ` | `@2024-01-15T10:30:00.123Z` |
| Unix timestamp | `@<epoch seconds>` | `@1705312200` |
| TimeOnly | `@HH:MM:SS[.mmm]` | `@14:30:00` |
| Duration | `@P...` (ISO 8601) | `@PT4H30M`, `@P1Y2M3D` |

```rdn
{
  "created": @2024-01-15T10:30:00.000Z,
  "start": @14:30:00,
  "timeout": @PT30M
}
```

## API

Mirrors `System.Text.Json` exactly under the `Rdn` namespace, with additional RDN date/time support:

### Serialize / Deserialize

```csharp
using Rdn;

// DateTime and TimeOnly automatically serialize as @-prefixed literals
string rdn = JsonSerializer.Serialize(new { Name = "Alice", CreatedAt = DateTime.UtcNow });
// => {"Name":"Alice","CreatedAt":@2024-01-15T10:30:00.000Z}

var obj = JsonSerializer.Deserialize<Person>(rdn);
```

### Document API

```csharp
using var doc = JsonDocument.Parse("{\"d\":@2024-01-15T10:30:00.000Z}");
DateTime dt = doc.RootElement.GetProperty("d").GetRdnDateTime();
```

### Reader

```csharp
var reader = new Utf8JsonReader(data);
while (reader.Read())
{
    if (reader.TokenType == JsonTokenType.RdnDateTime)
    {
        DateTime dt = reader.GetRdnDateTime();
    }
    else if (reader.TokenType == JsonTokenType.RdnTimeOnly)
    {
        TimeOnly t = reader.GetRdnTimeOnly();
    }
    else if (reader.TokenType == JsonTokenType.RdnDuration)
    {
        RdnDuration dur = reader.GetRdnDuration();
        if (dur.TryToTimeSpan(out TimeSpan ts)) { /* use ts */ }
    }
}
```

### Writer

```csharp
var writer = new Utf8JsonWriter(stream);
writer.WriteStartObject();

writer.WritePropertyName("created");
writer.WriteRdnDateTimeValue(DateTime.UtcNow);          // @2024-01-15T10:30:00.000Z

writer.WritePropertyName("start");
writer.WriteRdnTimeOnlyValue(new TimeOnly(14, 30, 0));  // @14:30:00

writer.WritePropertyName("timeout");
writer.WriteRdnDurationValue(new RdnDuration("PT30M")); // @PT30M

writer.WriteEndObject();
```

## RDN Set Support

RDN Sets are unordered collections, delimited by braces. Both explicit (`Set{...}`) and implicit (`{...}`) syntax are supported during parsing. Serialization always outputs explicit `Set{...}` syntax.

| Form | Syntax | Example |
|------|--------|---------|
| Explicit empty | `Set{}` | `Set{}` |
| Explicit | `Set{v, ...}` | `Set{1, 2, 3}` |
| Implicit single | `{v}` | `{"only"}` |
| Implicit multi | `{v, v, ...}` | `{"a", "b", "c"}` |

### Brace Disambiguation

When parsing `{`, the reader looks ahead to distinguish Object from Set:

| After first value | Result |
|---|---|
| `:` | Object |
| `,` or `}` | Set |
| Empty `{}` | Object |

### Serialize / Deserialize

```csharp
using Rdn;

// HashSet<T> serializes as Set{...}
string rdn = JsonSerializer.Serialize(new HashSet<int> { 1, 2, 3 });
// => Set{1,2,3}

var set = JsonSerializer.Deserialize<HashSet<int>>("Set{1, 2, 3}");
// Implicit syntax also works:
var set2 = JsonSerializer.Deserialize<HashSet<int>>("{1, 2, 3}");
```

### Document API

```csharp
using var doc = JsonDocument.Parse("Set{10, 20, 30}");
doc.RootElement.ValueKind  // JsonValueKind.Set
doc.RootElement.GetArrayLength()  // 3

foreach (var element in doc.RootElement.EnumerateSet())
{
    Console.WriteLine(element.GetInt32());
}
```

### Mutable DOM (JsonSet)

```csharp
using Rdn.Nodes;

var set = new JsonSet(JsonValue.Create(1), JsonValue.Create(2));
set.Add(JsonValue.Create(3));
set.Count  // 3

// Parse from RDN
var parsed = JsonNode.Parse("Set{1, 2, 3}") as JsonSet;
```

### Writer

```csharp
writer.WriteStartSet();
writer.WriteNumberValue(1);
writer.WriteNumberValue(2);
writer.WriteEndSet();
// => Set{1,2}

// Named property
writer.WriteStartObject();
writer.WriteStartSet("tags");
writer.WriteStringValue("a");
writer.WriteEndSet();
writer.WriteEndObject();
// => {"tags":Set{"a"}}
```

## Special Numeric Values (NaN / Infinity)

RDN treats `NaN`, `Infinity`, and `-Infinity` as bare number literals (like `true`/`false`/`null`), not quoted strings.

| Value | Literal |
|-------|---------|
| Not a Number | `NaN` |
| Positive Infinity | `Infinity` |
| Negative Infinity | `-Infinity` |

```rdn
{
  "nan": NaN,
  "inf": Infinity,
  "negInf": -Infinity
}
```

### Serialize / Deserialize

```csharp
using Rdn;

// Non-finite doubles serialize as bare literals
string rdn = JsonSerializer.Serialize(double.NaN);       // => NaN
string rdn2 = JsonSerializer.Serialize(double.PositiveInfinity); // => Infinity

double nan = JsonSerializer.Deserialize<double>("NaN");   // double.NaN
double inf = JsonSerializer.Deserialize<double>("Infinity"); // double.PositiveInfinity
```

### Document API

```csharp
using var doc = JsonDocument.Parse("{\"nan\": NaN, \"inf\": Infinity}");
double nan = doc.RootElement.GetProperty("nan").GetDouble();   // double.NaN
double inf = doc.RootElement.GetProperty("inf").GetDouble();   // double.PositiveInfinity
```

### Writer

```csharp
writer.WriteStartObject();
writer.WriteNumber("nan", double.NaN);              // NaN
writer.WriteNumber("inf", double.PositiveInfinity);  // Infinity
writer.WriteNumber("negInf", double.NegativeInfinity); // -Infinity
writer.WriteEndObject();
// => {"nan":NaN,"inf":Infinity,"negInf":-Infinity}
```

### RdnDuration

`TimeSpan` cannot represent years or months. `RdnDuration` preserves the original ISO 8601 string and offers a best-effort conversion:

```csharp
var dur = new RdnDuration("P1Y2M3DT4H5M6S");
dur.Iso       // "P1Y2M3DT4H5M6S"
dur.ToString() // "P1Y2M3DT4H5M6S"

// Converts when no year/month components are present
var simple = new RdnDuration("PT4H30M");
simple.TryToTimeSpan(out TimeSpan ts); // true, ts = 04:30:00
```

## Roadmap

- [x] DateTime (`@2024-01-15T10:30:00.000Z`)
- [x] TimeOnly (`@14:30:00`)
- [x] Duration (`@PT4H30M`)
- [x] NaN / Infinity / -Infinity
- [ ] BigInteger
- [ ] Regex
- [ ] Binary (`byte[]`)
- [x] Map
- [x] Set
- [ ] Tuple
- [ ] Conform to the shared test suite in `test-suite/`
