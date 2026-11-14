# YAML 1.2

The `ShapeShift.Yaml` package maps PolyType contracts to [YAML 1.2](https://yaml.org/spec/1.2.2/).
The reader accepts the YAML 1.2 syntax. The writer emits the same block style it always has, so existing documents stay stable.

Install the `ShapeShift.Yaml` package and annotate serialized root types with PolyType's `GenerateShapeAttribute`:

[!code-csharp[YamlSerialization](../../samples/cs/YamlSerialization.cs#YamlSerialization)]

That sample writes:

```yaml
  Name: Ada
  Interests:
    - mathematics
    - programming
```

`YamlSerializer` supports:

- Serialization to one YAML document, and deserialization of the first document in a stream.
- `DeserializeAll` for every document in a stream.
- All shared ShapeShift converters and policies, including naming policies, generated surrogates, attributed unions, default-value omission, and strict duplicate/required-member validation.
- Trimmed and NativeAOT applications. The parser is part of this package and does not reflect.

## Reading YAML 1.2

`YamlDecoder` composes the stream before the first token is read. It accepts:

- Block and flow sequences and mappings, including a block sequence nested directly under a key at that key's column.
- Plain, single-quoted, double-quoted, literal (`|`), and folded (`>`) scalars, including chomping (`+`, `-`, and the default clip).
- Anchors and aliases, including an alias used as a mapping key. Recursive aliases are rejected.
- Tags. `!!str`, `!!null`, `!!bool`, `!!int`, `!!float`, and `!!binary` select the core type. Other tags are preserved as text.
- `%YAML` and `%TAG` directives, and `---` / `...` document markers.

Untagged plain scalars use the YAML 1.2 core schema:

- `null`, `Null`, `NULL`, `~`, and an empty plain scalar are null.
- `true` and `false`, in any case, are Booleans. `yes`, `no`, `on`, and `off` stay strings.
- Integers may be decimal, `0x` hexadecimal, or `0o` octal, and may be larger than 64 bits.
- Floats require a decimal point or an exponent. `.nan`, `.inf`, and `-.inf` are the non-finite forms.
- Quoted and block scalars stay strings unless a tag says otherwise. Timestamps stay strings, so `ReadDateTime` and `ReadTimeSpan` still parse text.

A mapping key may be any node. A scalar key is a property name; a collection key is a nested value.

After each document, `NextTokenType` is `EndDocument`. `TryAdvanceDocument` moves to the next document and returns false at the end of the stream. `DocumentCount` is zero for an empty or comment-only stream. A caller that reads one value from that stream still sees a null, which is what `Deserialize` returns. `DeserializeAll` returns an empty list.

[!code-csharp[YamlDocuments](../../samples/cs/YamlSerialization.cs#YamlDocuments)]

## Load options

`YamlLoadOptions` is immutable and lives on `YamlSerializer.LoadOptions`. The `YamlDecoder` constructor takes the same record.

- `AllowDuplicateKeys` defaults to false, so a repeated mapping key is an error. YAML 1.2 requires unique keys. Set it to true to keep every entry in source order. For example, this document is rejected unless duplicates are allowed:

  ```yaml
  name: Ada
  name: Grace
  ```
- `MaxDepth` defaults to 256. Block scalars do not count toward the limit.
- `MaxAliasExpansions` defaults to 4096. This bounds how many times aliases may be substituted in one document.

[!code-csharp[YamlLoadOptions](../../samples/cs/YamlSerialization.cs#YamlLoadOptions)]

## What the writer emits

The writer produces indented block mappings and sequences. It quotes scalars that would otherwise change meaning on the way back in, including nulls, Booleans, numbers, and strings that start with an indicator. It does not emit flow collections, anchors, aliases, tags, or directives. Binary values are rejected on write; `!!binary` is accepted on read.

## Invalid input

The reader rejects a document that breaks YAML 1.2 structure: a bad indent, a tab used as block indentation, a directive with no document, an unknown tag handle, a recursive alias, or content after the document. Collection limits and duplicate-key checks throw `DecoderException` as well.
