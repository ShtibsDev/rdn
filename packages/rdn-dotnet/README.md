# Rdn for .NET

A .NET implementation of [RDN (Rich Data Notation)](../../spec/rdn-spec.md) — a strict superset of JSON that adds native representations for dates, durations, regular expressions, binary data, BigIntegers, Maps, Sets, and special numeric values.

Built as a fork of `System.Text.Json`, the API is intentionally familiar: if you know `System.Text.Json`, you already know how to use `Rdn`.

## Why RDN?

JSON lacks native types for dates, binary data, BigIntegers, and other common programming constructs. This forces workarounds — ISO strings for dates, base64 strings for bytes, numbers-as-strings for BigInts — that lose type information and require manual parsing on both ends.

RDN closes these gaps while remaining a strict JSON superset. Every valid JSON document is already valid RDN. The new types have unambiguous literal syntax that requires no schema, no string conventions, and no guessing.

**JSON:**

```json
{
	"created": "2024-01-15T10:30:00.000Z",
	"duration": "PT2H30M",
	"thumbnail": "SGVsbG8gV29ybGQ=",
	"tags": ["alpha", "beta"],
	"score": "Infinity",
	"totalSupply": "99999999999999999999999999"
}
```

**RDN:**

```
{
  "created": @2024-01-15T10:30:00.000Z,
  "duration": @PT2H30M,
  "thumbnail": b"SGVsbG8gV29ybGQ=",
  "tags": Set{"alpha", "beta"},
  "score": Infinity,
  "totalSupply": 99999999999999999999999999n
}
```

## Requirements

- .NET 9.0+

## Installation

```bash
dotnet add package Rdn
dotnet add package Rdn.AspNetCore  # for ASP.NET Core
```

## Project Structure

```
packages/rdn-dotnet/
├── Rdn.sln
├── Directory.Build.props     # Centralized build properties & NuGet metadata
├── icon.png                  # 128x128 package icon for NuGet gallery
├── global.json               # Pins .NET SDK version (9.0, latestFeature roll-forward)
├── .editorconfig              # Code style & naming conventions
├── src/
│   ├── Rdn/                  # Core serialization library
│   ├── Rdn.Encodings.Web/    # Web-safe text encoding
│   └── Rdn.AspNetCore/       # ASP.NET Core formatters
├── tests/
│   └── Rdn.Tests/            # xUnit test suite
└── benchmarks/
    └── Rdn.Benchmarks/       # BenchmarkDotNet performance benchmarks
```

## Quick Start

### Serialize & Deserialize

```csharp
using Rdn;

var person = new Person("Alice", 30, DateTime.UtcNow);
string rdn = RdnSerializer.Serialize(person);
// {"Name":"Alice","Age":30,"Created":@2024-01-15T10:30:00.000Z}

var deserialized = RdnSerializer.Deserialize<Person>(rdn);

record Person(string Name, int Age, DateTime Created);
```

### Configure Options

```csharp
var options = new RdnSerializerOptions
{
    PropertyNamingPolicy = RdnNamingPolicy.CamelCase,
    WriteIndented = true,
    DateTimeFormat = RdnDateTimeFormat.UnixMilliseconds,
    BinaryFormat = RdnBinaryFormat.Hex
};

string rdn = RdnSerializer.Serialize(person, options);
```

### Preset Configurations

```csharp
// General-purpose defaults
RdnSerializerOptions.Default

// Web-optimized: camelCase, case-insensitive property matching, AllowReadingFromString
RdnSerializerOptions.Web

// Strict: disallow unmapped members, disallow duplicate properties
RdnSerializerOptions.Strict
```

## RDN Types

Every type below is parsed and serialized natively — no string wrapping, no schema required.

### DateTime

```csharp
// ISO 8601 (default)
// RDN: {"Created":@2024-01-15T10:30:00.000Z}
var record = new { Created = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc) };
RdnSerializer.Serialize(record);

// Unix milliseconds
var options = new RdnSerializerOptions { DateTimeFormat = RdnDateTimeFormat.UnixMilliseconds };
// RDN: {"Created":@1705312200000}
RdnSerializer.Serialize(record, options);
```

Both formats are always accepted during deserialization regardless of the `DateTimeFormat` setting.

Supported C# types: `DateTime`, `DateTimeOffset`, `DateOnly`.

### TimeOnly

```csharp
// RDN: {"Start":@14:30:00.500}
var record = new { Start = new TimeOnly(14, 30, 0, 500) };
RdnSerializer.Serialize(record);
```

### Duration

```csharp
// RDN: {"Length":@P1Y2M3DT4H5M6S}
var record = new { Length = new RdnDuration("P1Y2M3DT4H5M6S") };
RdnSerializer.Serialize(record);

// TimeSpan maps to duration automatically
// RDN: {"Elapsed":@P2DT3H4M5S}
var record2 = new { Elapsed = new TimeSpan(2, 3, 4, 5) };
RdnSerializer.Serialize(record2);
```

`RdnDuration` preserves the raw ISO 8601 string since `TimeSpan` cannot represent years or months. Use `TryToTimeSpan()` to convert when the duration only contains days/hours/minutes/seconds:

```csharp
var dur = new RdnDuration("P1Y2M3DT4H5M6S");
dur.Iso        // "P1Y2M3DT4H5M6S"
dur.ToString() // "P1Y2M3DT4H5M6S"

var simple = new RdnDuration("PT4H30M");
simple.TryToTimeSpan(out TimeSpan ts); // true, ts = 04:30:00

var complex = new RdnDuration("P1Y2M");
complex.TryToTimeSpan(out _); // false — years/months can't map to TimeSpan
```

### Regular Expressions

```csharp
using System.Text.RegularExpressions;

// Regex serializes as /pattern/flags
string rdn = RdnSerializer.Serialize(new Regex("test", RegexOptions.IgnoreCase));
// => /test/i

var regex = RdnSerializer.Deserialize<Regex>("/^[a-z]+$/i");
// regex.ToString() == "^[a-z]+$"
// regex.Options.HasFlag(RegexOptions.IgnoreCase) == true
```

Valid flags: `d`, `g`, `i`, `m`, `s`, `u`, `v`, `y` (only `i`, `m`, `s` map to C# `RegexOptions`).

### Binary Data

```csharp
// Base64 (default): b"SGVsbG8="
// Hex:              x"48656C6C6F"
var model = new { Data = new byte[] { 0x01, 0x02, 0xFF } };
RdnSerializer.Serialize(model);
// {"Data":b"AQID/w=="}

// Switch to hex output
var options = new RdnSerializerOptions { BinaryFormat = RdnBinaryFormat.Hex };
RdnSerializer.Serialize(model, options);
// {"Data":x"0102FF"}
```

Both formats are always accepted during deserialization. Works with `byte[]`, `Memory<byte>`, and `ReadOnlyMemory<byte>`. Backwards-compatible: plain base64 strings (`"SGVsbG8="`) still deserialize into `byte[]`.

### BigInteger

Arbitrary-precision integers use the `n` suffix — matching JavaScript BigInt syntax.

```csharp
using System.Numerics;

// Serialize: BigInteger values get the 'n' suffix
var model = new { TotalSupply = BigInteger.Parse("99999999999999999999999999999999999999") };
RdnSerializer.Serialize(model);
// {"TotalSupply":99999999999999999999999999999999999999n}

// Deserialize: both BigInteger literals and regular numbers work
var result = RdnSerializer.Deserialize<BigIntegerModel>("""{"Value": 42n}""");
var result2 = RdnSerializer.Deserialize<BigIntegerModel>("""{"Value": 42}""");

// Negative BigIntegers
RdnSerializer.Serialize(new { V = new BigInteger(-42) });
// {"V":-42n}
```

Only integers are valid — `42.5n` is not valid RDN. Negative values like `-42n` are supported.

### Sets

```csharp
// HashSet<T> serializes as Set{...}
string rdn = RdnSerializer.Serialize(new HashSet<int> { 1, 2, 3 });
// => Set{1,2,3}

var set = RdnSerializer.Deserialize<HashSet<int>>("Set{1, 2, 3}");
// Implicit syntax also works:
var set2 = RdnSerializer.Deserialize<HashSet<int>>("{1, 2, 3}");
```

### Maps

Unlike JSON objects (which require string keys), RDN Maps support keys of any type — integers, dates, booleans, even arrays.

```csharp
// String keys
var stringMap = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
RdnSerializer.Serialize(stringMap);
// {"a" => 1,"b" => 2}

// Integer keys
var intMap = new Dictionary<int, string> { [1] = "one", [2] = "two" };
RdnSerializer.Serialize(intMap);
// {1 => "one",2 => "two"}

// DateTime keys
var dt = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
var dateMap = new Dictionary<DateTime, string> { [dt] = "event" };
RdnSerializer.Serialize(dateMap);
// {@2024-01-15T10:30:00.000Z => "event"}

// DateOnly keys
var dateOnlyMap = new Dictionary<DateOnly, int> { [new DateOnly(2024, 6, 15)] = 42 };
RdnSerializer.Serialize(dateOnlyMap);
// {@2024-06-15 => 42}

// Empty dictionaries use the explicit Map{} prefix
RdnSerializer.Serialize(new Dictionary<string, int>());
// Map{}

// Nested maps
var nested = new Dictionary<string, Dictionary<int, string>>
{
    ["outer"] = new Dictionary<int, string> { [1] = "inner" }
};
RdnSerializer.Serialize(nested);
// {"outer" => {1 => "inner"}}
```

All dictionary types roundtrip correctly. Both explicit (`Map{...}`) and implicit (`{...=>...}`) syntax are accepted during deserialization, and JSON object syntax (`{...:...}`) remains backwards-compatible for string-keyed dictionaries.

### Special Numeric Values

```csharp
// NaN, Infinity, -Infinity are native literals — no workarounds needed
var data = new { Score = double.PositiveInfinity, Delta = double.NaN };
RdnSerializer.Serialize(data);
// {"Score":Infinity,"Delta":NaN}

double nan = RdnSerializer.Deserialize<double>("NaN");        // double.NaN
double inf = RdnSerializer.Deserialize<double>("Infinity");   // double.PositiveInfinity
```

### Tuples

```csharp
// RDN: (1, "hello", true)
// Parsed as arrays
```

## API Layers

### High-Level: RdnSerializer

Drop-in replacement for `JsonSerializer`. Supports generics, runtime `Type`, stream/pipe async, and source generation.

```csharp
// String
string rdn = RdnSerializer.Serialize(value);
var obj = RdnSerializer.Deserialize<T>(rdn);

// UTF-8 bytes
byte[] bytes = RdnSerializer.SerializeToUtf8Bytes(value);
var obj = RdnSerializer.Deserialize<T>(utf8Bytes);

// Stream (async)
await RdnSerializer.SerializeAsync(stream, value);
var obj = await RdnSerializer.DeserializeAsync<T>(stream);

// DOM
RdnDocument doc = RdnSerializer.SerializeToDocument(value);
RdnElement elem = RdnSerializer.SerializeToElement(value);
RdnNode node = RdnSerializer.SerializeToNode(value);
```

### Read-Only DOM: RdnDocument / RdnElement

Lightweight, pooled-memory document model for read-only access.

```csharp
using var doc = RdnDocument.Parse("""{"dt": @2024-01-15T10:30:00.000Z, "tags": Set{1, 2}}""");

RdnElement root = doc.RootElement;
DateTime dt = root.GetProperty("dt").GetDateTime();
RdnValueKind kind = root.GetProperty("tags").ValueKind; // RdnValueKind.Set

// Enumerate
foreach (RdnElement item in root.GetProperty("tags").EnumerateSet())
    Console.WriteLine(item.GetInt32());

// All value kinds
root.GetProperty("dt").ValueKind     // RdnValueKind.RdnDateTime
root.GetProperty("t").ValueKind      // RdnValueKind.RdnTimeOnly
root.GetProperty("d").ValueKind      // RdnValueKind.RdnDuration
root.GetProperty("re").ValueKind     // RdnValueKind.RdnRegExp
root.GetProperty("bin").ValueKind    // RdnValueKind.RdnBinary
root.GetProperty("count").ValueKind  // RdnValueKind.RdnBigInteger
root.GetProperty("tags").ValueKind   // RdnValueKind.Set
root.GetProperty("map").ValueKind    // RdnValueKind.Map

// Type-specific extraction
root.GetProperty("dt").GetDateTime();
root.GetProperty("dt").GetDateTimeOffset();
root.GetProperty("t").GetRdnTimeOnly();
root.GetProperty("d").GetRdnDuration();
root.GetProperty("re").GetRdnRegExpSource();
root.GetProperty("re").GetRdnRegExpFlags();
root.GetProperty("bin").GetRdnBinary();
root.GetProperty("count").GetBigInteger();
```

### Mutable DOM: RdnNode

For building or modifying RDN structures in memory.

```csharp
using Rdn.Nodes;

var obj = new RdnObject
{
    ["name"] = "Charlie",
    ["age"] = 35,
    ["city"] = "NYC"
};
obj["city"] = "LA"; // mutate in place

string name = obj["name"]!.GetValue<string>(); // "Charlie"

// Other node types
var arr = new RdnArray(1, 2, 3);
var set = new RdnSet(RdnValue.Create(1), RdnValue.Create(2));
var map = new RdnMap { { RdnValue.Create("key"), RdnValue.Create("value") } };
```

### Low-Level: Utf8RdnReader / Utf8RdnWriter

Zero-allocation, forward-only reader and writer for maximum performance.

```csharp
// Writing
using var stream = new MemoryStream();
using (var writer = new Utf8RdnWriter(stream))
{
    writer.WriteStartObject();
    writer.WritePropertyName("created");
    writer.WriteRdnDateTimeValue(DateTime.UtcNow);
    writer.WritePropertyName("start");
    writer.WriteRdnTimeOnlyValue(new TimeOnly(14, 30, 0));
    writer.WritePropertyName("timeout");
    writer.WriteRdnDurationValue(new RdnDuration("PT30M"));
    writer.WritePropertyName("pattern");
    writer.WriteRdnRegExpValue("^[a-z]+$", "gi");
    writer.WritePropertyName("data");
    writer.WriteRdnBinaryValue(new byte[] { 0xFF, 0x00 });
    writer.WritePropertyName("count");
    writer.WriteBigIntegerValue(new BigInteger(42));
    writer.WriteStartSet("tags");
    writer.WriteStringValue("alpha");
    writer.WriteEndSet();
    writer.WriteEndObject();
}

// Reading
var reader = new Utf8RdnReader(utf8Bytes);
while (reader.Read())
{
    switch (reader.TokenType)
    {
        case RdnTokenType.RdnDateTime:
            DateTime dt = reader.GetRdnDateTime();
            break;
        case RdnTokenType.RdnTimeOnly:
            TimeOnly time = reader.GetRdnTimeOnly();
            break;
        case RdnTokenType.RdnDuration:
            RdnDuration dur = reader.GetRdnDuration();
            break;
        case RdnTokenType.RdnRegExp:
            string pattern = reader.GetRdnRegExpSource();
            string flags = reader.GetRdnRegExpFlags();
            break;
        case RdnTokenType.RdnBinary:
            byte[] data = reader.GetRdnBinary();
            break;
        case RdnTokenType.RdnBigInteger:
            BigInteger bigInt = reader.GetBigInteger();
            break;
        case RdnTokenType.StartSet: // ...
        case RdnTokenType.StartMap: // ...
            break;
    }
}
```

## RdnSerializerOptions

### RDN-Specific Options

| Property                 | Type                | Default  | Description                                                                                                         |
| ------------------------ | ------------------- | -------- | ------------------------------------------------------------------------------------------------------------------- |
| `DateTimeFormat`         | `RdnDateTimeFormat` | `Iso`    | `Iso` writes `@2024-01-15T10:30:00.000Z`, `UnixMilliseconds` writes `@1705312200000`. Both always accepted on read. |
| `BinaryFormat`           | `RdnBinaryFormat`   | `Base64` | `Base64` writes `b"..."`, `Hex` writes `x"..."`. Both always accepted on read.                                      |
| `AlwaysWriteMapTypeName` | `bool`              | `false`  | Always emit `Map{` prefix for non-empty Maps. Empty Maps always emit `Map{}` regardless.                            |
| `AlwaysWriteSetTypeName` | `bool`              | `false`  | Always emit `Set{` prefix for non-empty Sets. Empty Sets always emit `Set{}` regardless.                            |

### Standard Options

All familiar `System.Text.Json` options carry over:

| Property                               | Type                        | Default                 |
| -------------------------------------- | --------------------------- | ----------------------- |
| `PropertyNamingPolicy`                 | `RdnNamingPolicy?`          | `null` (preserve names) |
| `PropertyNameCaseInsensitive`          | `bool`                      | `false`                 |
| `WriteIndented`                        | `bool`                      | `false`                 |
| `IndentCharacter`                      | `char`                      | ` ` (space)             |
| `IndentSize`                           | `int`                       | `2`                     |
| `MaxDepth`                             | `int`                       | `0` (means 64)          |
| `NumberHandling`                       | `RdnNumberHandling`         | `Strict`                |
| `DefaultIgnoreCondition`               | `RdnIgnoreCondition`        | `Never`                 |
| `IgnoreReadOnlyProperties`             | `bool`                      | `false`                 |
| `IgnoreReadOnlyFields`                 | `bool`                      | `false`                 |
| `IncludeFields`                        | `bool`                      | `false`                 |
| `ReferenceHandler`                     | `ReferenceHandler?`         | `null`                  |
| `UnmappedMemberHandling`               | `RdnUnmappedMemberHandling` | `Skip`                  |
| `PreferredObjectCreationHandling`      | `RdnObjectCreationHandling` | `Replace`               |
| `AllowTrailingCommas`                  | `bool`                      | `false`                 |
| `AllowDuplicateProperties`             | `bool`                      | `true`                  |
| `DefaultBufferSize`                    | `int`                       | `16384`                 |
| `Encoder`                              | `JavaScriptEncoder?`        | `null`                  |
| `NewLine`                              | `string`                    | `Environment.NewLine`   |
| `RespectNullableAnnotations`           | `bool`                      | `false`                 |
| `RespectRequiredConstructorParameters` | `bool`                      | `false`                 |

## Attributes

```csharp
using Rdn.Serialization;

public class Event
{
    [RdnPropertyName("event_name")]
    public string Name { get; set; }

    [RdnPropertyOrder(0)]
    public int Id { get; set; }

    [RdnIgnore]
    public string Internal { get; set; }

    [RdnRequired]
    public DateTime Created { get; set; }

    [RdnConverter(typeof(MyCustomConverter))]
    public CustomType Data { get; set; }

    [RdnInclude]
    private string Secret { get; set; }

    [RdnExtensionData]
    public Dictionary<string, RdnElement> Extras { get; set; }

    [RdnNumberHandling(RdnNumberHandling.AllowReadingFromString)]
    public int Count { get; set; }

    [RdnObjectCreationHandling(RdnObjectCreationHandling.Populate)]
    public List<string> Tags { get; set; }
}

// Polymorphism
[RdnDerivedType(typeof(Circle), "circle")]
[RdnDerivedType(typeof(Square), "square")]
[RdnPolymorphic]
public abstract class Shape { }
```

## Source Generation (AOT)

For ahead-of-time compilation and trimming scenarios:

```csharp
[RdnSourceGenerationOptions(
    PropertyNamingPolicy = RdnKnownNamingPolicy.CamelCase,
    DateTimeFormat = RdnDateTimeFormat.Iso,
    BinaryFormat = RdnBinaryFormat.Base64)]
public partial class AppRdnContext : RdnSerializerContext
{
}
```

## ASP.NET Core Integration

The `Rdn.AspNetCore` package provides input/output formatters for the `application/rdn` media type.

### Setup

```csharp
// Register with defaults
builder.Services.AddControllers().AddRdnFormatters();

// Register with custom options
builder.Services.AddControllers().AddRdnFormatters(options =>
{
    options.PropertyNamingPolicy = RdnNamingPolicy.CamelCase;
    options.DateTimeFormat = RdnDateTimeFormat.UnixMilliseconds;
    options.BinaryFormat = RdnBinaryFormat.Hex;
    options.WriteIndented = true;
});
```

### Usage

Controllers work as usual. Clients send `Content-Type: application/rdn` for requests and `Accept: application/rdn` for responses:

```csharp
[ApiController]
[Route("api/events")]
public class EventsController : ControllerBase
{
    [HttpPost]
    public IActionResult Create([FromBody] Event ev)
    {
        // ev deserialized from RDN when Content-Type: application/rdn
        return Ok(ev);
        // Response serialized as RDN when Accept: application/rdn
    }
}
```

## Brace Disambiguation

`{` can start an Object, Map, or Set. The parser looks ahead after the first value:

| After first value | Interpretation |
| ----------------- | -------------- |
| `:`               | Object         |
| `=>`              | Map            |
| `,` or `}`        | Set            |
| Empty `{}`        | Object         |

## Differences from System.Text.Json

This implementation is a fork of `System.Text.Json` with the following changes:

- **All `Json*` identifiers renamed to `Rdn*`** — `JsonSerializer` → `RdnSerializer`, `Utf8JsonReader` → `Utf8RdnReader`, `JsonDocument` → `RdnDocument`, etc.
- **`AllowNamedFloatingPointLiterals` removed** — RDN has native `NaN`, `Infinity`, and `-Infinity` bare number literals. These are always read/written through the standard code path.
- **DateTime string fallback removed** — RDN has native `@`-prefixed date/time syntax. The converters no longer fall back to parsing ISO strings from `RdnTokenType.String` tokens.
- **New token types** — `RdnDateTime`, `RdnTimeOnly`, `RdnDuration`, `RdnRegExp`, `RdnBinary`, `RdnBigInteger`, `StartSet`/`EndSet`, `StartMap`/`EndMap`.
- **New serialization options** — `DateTimeFormat`, `BinaryFormat`, `AlwaysWriteMapTypeName`, and `AlwaysWriteSetTypeName` for controlling output format of RDN-specific types.

## Build & Test

```bash
cd packages/rdn-dotnet

# Build everything
dotnet build Rdn.sln

# Run tests
dotnet test

# Run benchmarks
dotnet run --project benchmarks/Rdn.Benchmarks -c Release
```

## Roadmap

- [x] DateTime (`@2024-01-15T10:30:00.000Z`)
- [x] DateOnly (`@2024-01-15`)
- [x] TimeOnly (`@14:30:00`)
- [x] Duration / TimeSpan (`@PT4H30M`)
- [x] NaN / Infinity / -Infinity
- [x] Regex (`/pattern/flags`)
- [x] Binary (`b"..."` / `x"..."`)
- [x] Map (`{key => value}`)
- [x] Set (`Set{1, 2, 3}`)
- [x] ASP.NET Core formatters (`application/rdn`)
- [x] BigInteger (`42n`)
- [ ] Tuple (`(1, 2, 3)`)
- [x] Conformance with the shared test suite in `test-suite/`

See [CHANGELOG.md](CHANGELOG.md) for version history.
