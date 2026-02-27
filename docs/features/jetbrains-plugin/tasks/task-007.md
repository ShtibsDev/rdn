# Task 007: Create Color Settings Page

## References
- [Tech Design](../tech-design.md) — Sections 3.13, 6.2
- [Discovery](../discovery.md)

## Description
Implement `RdnColorSettingsPage.kt` to provide the **Settings > Editor > Color Scheme > RDN** page. This page contains a demo text that shows all token types in context, and an `AttributesDescriptor` array that lets users customize each token category's color, boldness, italic, and underline. Register in `plugin.xml`.

## Files to Create/Modify
- `tools/jetbrains-plugin/src/main/kotlin/com/rdn/intellij/highlighting/RdnColorSettingsPage.kt` — Color settings page
- `tools/jetbrains-plugin/src/main/resources/META-INF/plugin.xml` — Register extension

## Implementation Details

### `RdnColorSettingsPage.kt`

```kotlin
package com.rdn.intellij.highlighting

import com.intellij.openapi.editor.colors.TextAttributesKey
import com.intellij.openapi.fileTypes.SyntaxHighlighter
import com.intellij.openapi.options.colors.AttributesDescriptor
import com.intellij.openapi.options.colors.ColorDescriptor
import com.intellij.openapi.options.colors.ColorSettingsPage
import com.rdn.intellij.RdnIcons
import javax.swing.Icon

class RdnColorSettingsPage : ColorSettingsPage {
    companion object {
        private val DESCRIPTORS = arrayOf(
            AttributesDescriptor("Keywords//null, true, false", RdnColors.KEYWORD),
            AttributesDescriptor("Numbers//Integer and float", RdnColors.NUMBER),
            AttributesDescriptor("Numbers//BigInt", RdnColors.BIGINT),
            AttributesDescriptor("Strings//String content", RdnColors.STRING),
            AttributesDescriptor("Strings//Valid escape sequence", RdnColors.STRING_ESCAPE),
            AttributesDescriptor("Strings//Invalid escape sequence", RdnColors.STRING_INVALID_ESCAPE),
            AttributesDescriptor("Object//Key", RdnColors.OBJECT_KEY),
            AttributesDescriptor("Date and Time//@ symbol and date part", RdnColors.AT_SIGN),
            AttributesDescriptor("Date and Time//Time components", RdnColors.TIME_PART),
            AttributesDescriptor("Date and Time//Milliseconds", RdnColors.MILLIS_PART),
            AttributesDescriptor("Date and Time//Timezone", RdnColors.TIMEZONE),
            AttributesDescriptor("Date and Time//Unix timestamp", RdnColors.UNIX_TIMESTAMP),
            AttributesDescriptor("Duration//P designator", RdnColors.DURATION_P),
            AttributesDescriptor("Duration//Numbers", RdnColors.DURATION_NUMBER),
            AttributesDescriptor("Duration//Unit letters", RdnColors.DURATION_UNIT),
            AttributesDescriptor("Duration//T separator", RdnColors.DURATION_T),
            AttributesDescriptor("Binary//Prefix (b, x)", RdnColors.BINARY_PREFIX),
            AttributesDescriptor("Binary//Content", RdnColors.BINARY_CONTENT),
            AttributesDescriptor("Binary//Invalid character", RdnColors.BINARY_INVALID_CHAR),
            AttributesDescriptor("Map and Set//Map keyword", RdnColors.MAP_KEYWORD),
            AttributesDescriptor("Map and Set//Set keyword", RdnColors.SET_KEYWORD),
            AttributesDescriptor("RegExp//Body content", RdnColors.REGEXP_BODY),
            AttributesDescriptor("RegExp//Flags", RdnColors.REGEXP_FLAGS),
            AttributesDescriptor("RegExp//Escape sequence", RdnColors.REGEXP_ESCAPE),
            AttributesDescriptor("RegExp//Character class escape (\\d, \\w)", RdnColors.REGEXP_CHAR_CLASS_ESCAPE),
            AttributesDescriptor("RegExp//Quantifier (+, *, ?, {n,m})", RdnColors.REGEXP_QUANTIFIER),
            AttributesDescriptor("RegExp//Anchor (^, $)", RdnColors.REGEXP_ANCHOR),
            AttributesDescriptor("RegExp//Alternation (|)", RdnColors.REGEXP_ALTERNATION),
            AttributesDescriptor("RegExp//Dot (.)", RdnColors.REGEXP_DOT),
            AttributesDescriptor("RegExp//Group parentheses", RdnColors.REGEXP_GROUP),
            AttributesDescriptor("RegExp//Special (lookaround, named group, backreference)", RdnColors.REGEXP_SPECIAL),
            AttributesDescriptor("RegExp//Character class brackets [...]", RdnColors.REGEXP_CHAR_CLASS),
            AttributesDescriptor("Braces and Operators//Braces {}", RdnColors.BRACES),
            AttributesDescriptor("Braces and Operators//Brackets []", RdnColors.BRACKETS),
            AttributesDescriptor("Braces and Operators//Parentheses ()", RdnColors.PARENS),
            AttributesDescriptor("Braces and Operators//Comma", RdnColors.COMMA),
            AttributesDescriptor("Braces and Operators//Colon", RdnColors.COLON),
            AttributesDescriptor("Braces and Operators//Arrow =>", RdnColors.ARROW),
            AttributesDescriptor("Bad character", RdnColors.BAD_CHARACTER),
        )
    }

    override fun getIcon(): Icon = RdnIcons.FILE
    override fun getHighlighter(): SyntaxHighlighter = RdnSyntaxHighlighter()
    override fun getDisplayName(): String = "RDN"
    override fun getAttributeDescriptors(): Array<AttributesDescriptor> = DESCRIPTORS
    override fun getColorDescriptors(): Array<ColorDescriptor> = ColorDescriptor.EMPTY_ARRAY
    override fun getAdditionalHighlightingTagToDescriptorMap(): Map<String, TextAttributesKey>? = null

    override fun getDemoText(): String = """
        {
          "name": "Rich Data Notation",
          "version": 1,
          "bigint": 9007199254740993n,
          "enabled": true,
          "nothing": null,
          "special": NaN,
          "inf": Infinity,
          "neg": -Infinity,
          "ratio": 3.14,
          "created": @2024-01-15T10:30:00.000Z,
          "date": @2024-01-15,
          "time": @14:30:00,
          "duration": @P1Y2M3DT4H5M6S,
          "unix": @1705276800,
          "binary": b"SGVsbG8gV29ybGQ=",
          "hex": x"48656C6C6F",
          "regex": /^[a-z]+\d{2,4}$/gi,
          "tuple": (1, 2, 3),
          "tags": ["rdn", "json"],
          "map": Map{
            "key" => "value"
          },
          "set": Set{"a", "b", "c"}
        }
    """.trimIndent()
}
```

### `plugin.xml` additions

```xml
<colorSettingsPage implementation="com.rdn.intellij.highlighting.RdnColorSettingsPage"/>
```

## Acceptance Criteria
- [ ] **Settings > Editor > Color Scheme > RDN** page appears in the IDE settings
- [ ] The demo text renders with colored syntax in the preview pane
- [ ] Every `AttributesDescriptor` in `DESCRIPTORS` has a corresponding `RdnColors` key
- [ ] Changing a color in the settings page immediately updates the preview
- [ ] All 39+ descriptors are present (verify count matches `DESCRIPTORS.size`)
- [ ] The page icon is the RDN file icon
- [ ] Display name is "RDN"

## Dependencies
- Depends on: task-002, task-006
- Blocks: None
