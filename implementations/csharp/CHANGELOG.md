# Changelog

All notable changes to the RDN C# implementation will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0] - 2026-02-16

Initial release of the RDN (Rich Data Notation) library for .NET, built as a fork of `System.Text.Json`.

> **Known limitations:** BigInteger (`42n`) is not yet implemented. The shared conformance test suite (`test-suite/`) is not yet integrated.

### Added

- **Core library** based on `System.Text.Json` with all `Json*` identifiers renamed to `Rdn*` (`RdnSerializer`, `Utf8RdnReader`, `RdnDocument`, etc.)
- **DateTime / DateTimeOffset / DateOnly** support via `@`-prefixed ISO 8601 literals (`@2024-01-15T10:30:00.000Z`, `@2024-01-15`)
- **TimeOnly** support (`@14:30:00.500`)
- **Duration** support with `RdnDuration` type and `TimeSpan` mapping (`@P1Y2M3DT4H5M6S`, `@PT4H30M`)
- **NaN / Infinity / -Infinity** as native bare number literals (no string wrapping)
- **Regular expressions** with `/pattern/flags` syntax and bidirectional `Regex` conversion
- **Binary data** with base64 (`b"..."`) and hex (`x"..."`) literal syntax for `byte[]`, `Memory<byte>`, and `ReadOnlyMemory<byte>`
- **Map** support with arbitrary key types (`{key => value}`) including implicit and explicit (`Map{...}`) syntax
- **Set** support (`Set{1, 2, 3}`) with implicit (`{1, 2, 3}`) and explicit syntax for `HashSet<T>` and related types
- **Tuple** serialization with `(1, 2, 3)` syntax for `Tuple<>` and `ValueTuple<>` (parsed as arrays)
- **Serialization options:**
  - `DateTimeFormat` (`Iso` / `UnixMilliseconds`) for controlling date output format
  - `BinaryFormat` (`Base64` / `Hex`) for controlling binary output format
  - `AlwaysWriteMapTypeName` to force `Map{` prefix on non-empty maps
  - `AlwaysWriteSetTypeName` to force `Set{` prefix on non-empty sets
- **Preset configurations:** `RdnSerializerOptions.Default`, `.Web` (camelCase, case-insensitive), and `.Strict` (disallow unmapped/duplicate members)
- **Read-only DOM** (`RdnDocument` / `RdnElement`) with pooled-memory access and RDN-specific value kinds
- **Mutable DOM** (`RdnNode`, `RdnObject`, `RdnArray`, `RdnSet`, `RdnMap`) for building and modifying RDN structures
- **Low-level reader/writer** (`Utf8RdnReader` / `Utf8RdnWriter`) for zero-allocation, forward-only processing
- **ASP.NET Core integration** (`Rdn.AspNetCore` package) with input/output formatters for the `application/rdn` media type
- **Source generation** support via `RdnSerializerContext` for AOT compilation and trimming
- **Web text encoding** (`Rdn.Encodings.Web` package) for web-safe text encoding
- Full attribute support: `[RdnPropertyName]`, `[RdnIgnore]`, `[RdnRequired]`, `[RdnConverter]`, `[RdnInclude]`, `[RdnExtensionData]`, `[RdnDerivedType]`, `[RdnPolymorphic]`, and more
- Security hardening for input validation and parser bounds checking
