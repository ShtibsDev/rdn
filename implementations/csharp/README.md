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

## RDN Regex Support

RDN regex literals use `/pattern/flags` syntax — parsed natively as `JsonTokenType.RdnRegExp`, with automatic `System.Text.RegularExpressions.Regex` serialization.

| Syntax | Example |
|--------|---------|
| `/pattern/flags` | `/^[a-z]+$/gi` |
| No flags | `/hello/` |
| Escaped slash | `/a\/b/` |

Valid flags: `d`, `g`, `i`, `m`, `s`, `u`, `v`, `y` (only `i`, `m`, `s` map to C# `RegexOptions`).

```rdn
{
  "pattern": /^[a-z]+$/i,
  "global": /test/gi
}
```

### Serialize / Deserialize

```csharp
using Rdn;
using System.Text.RegularExpressions;

// Regex serializes as /pattern/flags
string rdn = JsonSerializer.Serialize(new Regex("test", RegexOptions.IgnoreCase));
// => /test/i

var regex = JsonSerializer.Deserialize<Regex>("/^[a-z]+$/i");
// regex.ToString() == "^[a-z]+$"
// regex.Options.HasFlag(RegexOptions.IgnoreCase) == true
```

### Document API

```csharp
using var doc = JsonDocument.Parse("{\"re\": /^[a-z]+$/i}");
var re = doc.RootElement.GetProperty("re");
re.ValueKind  // JsonValueKind.RdnRegExp
string source = re.GetRdnRegExpSource();  // "^[a-z]+$"
string flags = re.GetRdnRegExpFlags();    // "i"
```

### Reader

```csharp
var reader = new Utf8JsonReader(data);
while (reader.Read())
{
    if (reader.TokenType == JsonTokenType.RdnRegExp)
    {
        string source = reader.GetRdnRegExpSource();
        string flags = reader.GetRdnRegExpFlags();
    }
}
```

### Writer

```csharp
writer.WriteStartObject();
writer.WritePropertyName("pattern");
writer.WriteRdnRegExpValue("^[a-z]+$", "gi");
writer.WriteEndObject();
// => {"pattern":/^[a-z]+$/gi}
```

## RDN Binary Support

RDN binary literals use `b"..."` (base64) or `x"..."` (hex) syntax — parsed natively as `RdnTokenType.RdnBinary`, with automatic `byte[]`, `Memory<byte>`, and `ReadOnlyMemory<byte>` serialization.

| Syntax | Example | Description |
|--------|---------|-------------|
| `b"<base64>"` | `b"SGVsbG8="` | Base64-encoded binary |
| `x"<hex>"` | `x"48656C6C6F"` | Hex-encoded binary |
| `b""` / `x""` | `b""` | Empty binary |

Default output is base64 (`b"..."`). Use `BinaryFormat = RdnBinaryFormat.Hex` to output hex (`x"..."`) instead. Both formats are always accepted during parsing.

```rdn
{
  "icon": b"iVBORw0KGgo=",
  "hash": x"DEADBEEF"
}
```

### Serialize / Deserialize

```csharp
using Rdn.Serialization;

// byte[] serializes as b"..."
string rdn = RdnSerializer.Serialize(new { Data = new byte[] { 0x48, 0x65, 0x6C } });
// => {"Data":b"SGVs"}

// Deserialize from base64 or hex binary literals
var model = RdnSerializer.Deserialize<MyModel>("""{"Data": b"SGVsbG8="}""");
// model.Data == "Hello"u8.ToArray()

// Also works with hex syntax
var model2 = RdnSerializer.Deserialize<MyModel>("""{"Data": x"48656C6C6F"}""");
// model2.Data == "Hello"u8.ToArray()

// Backwards-compatible: plain base64 strings still deserialize to byte[]
var model3 = RdnSerializer.Deserialize<MyModel>("""{"Data": "SGVsbG8="}""");
```

### Document API

```csharp
using var doc = RdnDocument.Parse("""{"data": b"SGVsbG8="}""");
var el = doc.RootElement.GetProperty("data");
el.ValueKind  // RdnValueKind.RdnBinary
byte[] bytes = el.GetRdnBinary();  // "Hello"u8.ToArray()
```

### Reader

```csharp
var reader = new Utf8RdnReader(data);
while (reader.Read())
{
    if (reader.TokenType == RdnTokenType.RdnBinary)
    {
        byte[] value = reader.GetRdnBinary();
    }
}
```

### Writer

```csharp
writer.WriteStartObject();
writer.WriteRdnBinary("icon", iconBytes);
writer.WriteEndObject();
// => {"icon":b"iVBORw0KGgo="}
```

## Serialization Options

### DateTimeFormat

Controls how `DateTime` and `DateTimeOffset` values are serialized. Default is `RdnDateTimeFormat.Iso`.

| Value | Output |
|-------|--------|
| `Iso` (default) | `@2024-01-15T10:30:00.000Z` |
| `UnixMilliseconds` | `@1705312200000` |

```csharp
var options = new RdnSerializerOptions { DateTimeFormat = RdnDateTimeFormat.UnixMilliseconds };
string rdn = RdnSerializer.Serialize(new { ts = DateTime.UtcNow }, options);
// => {"ts":@1739577600000}
```

Both formats are always accepted during deserialization regardless of this setting.

### BinaryFormat

Controls how `byte[]`, `Memory<byte>`, and `ReadOnlyMemory<byte>` values are serialized. Default is `RdnBinaryFormat.Base64`.

| Value | Output |
|-------|--------|
| `Base64` (default) | `b"SGVsbG8="` |
| `Hex` | `x"48656C6C6F"` |

```csharp
var options = new RdnSerializerOptions { BinaryFormat = RdnBinaryFormat.Hex };
string rdn = RdnSerializer.Serialize(new { data = new byte[] { 0x48, 0x65, 0x6C } }, options);
// => {"data":x"48656C"}
```

Both `b"..."` and `x"..."` formats are always accepted during deserialization regardless of this setting.

## Differences from System.Text.Json

This implementation is a fork of `System.Text.Json` with the following redundant JSON workarounds removed:

- **`AllowNamedFloatingPointLiterals` removed** — RDN has native `NaN`, `Infinity`, and `-Infinity` bare number literals, making the JSON workaround (which wrote them as quoted strings like `"NaN"`) redundant. These values are now always read/written as bare literals through the standard code path.

- **DateTime string fallback removed from serialization converters** — RDN has native `@`-prefixed date/time syntax. The `DateTimeConverter` and `DateTimeOffsetConverter` no longer fall back to parsing ISO strings from `RdnTokenType.String` tokens. Dates in RDN should always arrive as `RdnTokenType.RdnDateTime`. The reader's public `GetDateTime()` / `TryGetDateTime()` methods remain available for manual string parsing.

## Roadmap

- [x] DateTime (`@2024-01-15T10:30:00.000Z`)
- [x] TimeOnly (`@14:30:00`)
- [x] Duration (`@PT4H30M`)
- [x] NaN / Infinity / -Infinity
- [ ] BigInteger
- [x] Regex (`/pattern/flags`)
- [x] Binary (`byte[]`)
- [x] Map
- [x] Set
- [ ] Tuple
- [ ] Conform to the shared test suite in `test-suite/`
