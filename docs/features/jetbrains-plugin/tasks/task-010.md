# Task 010: Write Parser Tests

## References
- [Tech Design](../tech-design.md) — Section 10.3
- [Discovery](../discovery.md)

## Description
Create PSI tree assertion tests using IntelliJ's `ParsingTestCase` framework. Each test compares the produced PSI tree against a golden `.txt` file. Tests must cover all value types, brace disambiguation (object vs map vs set), nested structures, and error recovery behavior.

## Files to Create/Modify
- `tools/jetbrains-plugin/src/test/kotlin/com/rdn/intellij/parser/RdnParserTest.kt` — Parser tests
- `tools/jetbrains-plugin/src/test/resources/parser/` — Directory for `.rdn` input files and `.txt` golden files

## Implementation Details

### `RdnParserTest.kt`

```kotlin
package com.rdn.intellij.parser

import com.intellij.testFramework.ParsingTestCase

class RdnParserTest : ParsingTestCase("parser", "rdn", RdnParserDefinition()) {
    override fun getTestDataPath(): String = "src/test/resources"

    // Each doTest(true) call:
    //   1. Reads <testName>.rdn from src/test/resources/parser/
    //   2. Parses it
    //   3. Compares the PSI tree dump to <testName>.txt
    //   On first run with no .txt file, generates the golden file.

    fun testObject() = doTest(true)
    fun testArray() = doTest(true)
    fun testTuple() = doTest(true)
    fun testMapExplicit() = doTest(true)
    fun testMapImplicit() = doTest(true)
    fun testSetExplicit() = doTest(true)
    fun testSetImplicit() = doTest(true)
    fun testSetSingleElement() = doTest(true)
    fun testEmptyBrace() = doTest(true)
    fun testBraceDisambiguation() = doTest(true)
    fun testStringLiteral() = doTest(true)
    fun testNumberLiteral() = doTest(true)
    fun testBigIntLiteral() = doTest(true)
    fun testBooleanLiteral() = doTest(true)
    fun testNullLiteral() = doTest(true)
    fun testNanLiteral() = doTest(true)
    fun testInfinityLiteral() = doTest(true)
    fun testDateTimeLiteral() = doTest(true)
    fun testTimeOnlyLiteral() = doTest(true)
    fun testDurationLiteral() = doTest(true)
    fun testBinaryLiteral() = doTest(true)
    fun testRegExpLiteral() = doTest(true)
    fun testNestedObject() = doTest(true)
    fun testAllTypes() = doTest(true)
    fun testErrorRecovery() = doTest(true)
}
```

### Test data files

Create `.rdn` input files in `src/test/resources/parser/`:

**`testObject.rdn`**
```json
{"name": "Alice", "age": 30}
```

**`testMapImplicit.rdn`**
```
{"a" => 1, "b" => 2}
```

**`testMapExplicit.rdn`**
```
Map{"a" => 1}
```

**`testSetImplicit.rdn`**
```
{"a", "b", "c"}
```

**`testSetSingleElement.rdn`**
```
{"only"}
```

**`testEmptyBrace.rdn`**
```
{}
```

**`testBraceDisambiguation.rdn`**
```
[
  {"key": "value"},
  {"k" => "v"},
  {"a", "b"}
]
```

**`testDateTimeLiteral.rdn`**
```
@2024-01-15T10:30:00.000Z
```

**`testTimeOnlyLiteral.rdn`**
```
@14:30:00
```

**`testDurationLiteral.rdn`**
```
@P1Y2M3DT4H5M6S
```

**`testBinaryLiteral.rdn`**
```
b"SGVsbG8="
```

**`testRegExpLiteral.rdn`**
```
/^[a-z]+\d{2,4}$/gi
```

**`testAllTypes.rdn`**
```json
{
  "str": "hello",
  "num": 42,
  "float": 3.14,
  "bigint": 99n,
  "bool": true,
  "nil": null,
  "nan": NaN,
  "inf": Infinity,
  "date": @2024-01-15,
  "time": @14:30:00,
  "dur": @P1D,
  "bin": b"AA==",
  "re": /\w+/,
  "arr": [1, 2, 3],
  "tup": (1, 2),
  "map": Map{"k" => "v"},
  "set": Set{"x"}
}
```

**`testErrorRecovery.rdn`**
```
{
  "good": 1,
  bad: 2,
  "also_good": 3
}
```
(Parser should create an error element for `bad:` but continue parsing `"also_good": 3`.)

### Golden files

On the first run, generate golden `.txt` files by running the tests with the system property `idea.test.record` set:
```bash
./gradlew test --tests "*RdnParserTest*" -Didea.test.record=true
```

Verify the generated golden files match the expected PSI structure manually before committing.

### Expected PSI structure for `testObject.rdn`

```
RDN File
  RdnObject
    RdnObjectProperty
      RdnObjectKey
        STRING_OPEN ('"')
        STRING_CONTENT ('name')
        STRING_CLOSE ('"')
      COLON (':')
      RdnStringLiteral
        STRING_OPEN ('"')
        STRING_CONTENT ('Alice')
        STRING_CLOSE ('"')
    COMMA (',')
    RdnObjectProperty
      RdnObjectKey
        STRING_OPEN ('"')
        STRING_CONTENT ('age')
        STRING_CLOSE ('"')
      COLON (':')
      RdnNumberLiteral
        INTEGER ('30')
```

## Acceptance Criteria
- [ ] All parser tests pass with `./gradlew test --tests "*RdnParserTest*"`
- [ ] `testBraceDisambiguation` PSI tree contains `RdnObject`, `RdnMap`, and `RdnSet` nodes as siblings in the array
- [ ] `testEmptyBrace` PSI tree shows `RdnObject` (not Map or Set)
- [ ] `testSetSingleElement` PSI tree shows `RdnSet` (a single-element set `{"only"}` is still a Set)
- [ ] `testErrorRecovery` produces an error element for `bad:` but successfully parses `"also_good": 3`
- [ ] All golden `.txt` files are committed to the repo
- [ ] `doTest(true)` uses `checkAllPsiRoots = true` which verifies the full tree structure

## Dependencies
- Depends on: task-008, task-009
- Blocks: None (tests are standalone)
