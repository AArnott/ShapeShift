# TOML 1.0

The `ShapeShift.Toml` package maps PolyType contracts to [TOML 1.0](https://toml.io/en/v1.0.0) (Tom's Obvious, Minimal Language).
It parses the complete TOML 1.0 syntax through Tomlyn 0.19's validated syntax tree and emits canonical TOML 1.0.

Install the `ShapeShift.Toml` package and annotate serialized root types with PolyType's `GenerateShapeAttribute`:

[!code-csharp[TomlSerialization](../../samples/cs/TomlSerialization.cs#TomlSerialization)]

`TomlSerializer` supports:

- Serialization to and from TOML strings.
- Standard `[table]` sections, `[[array-of-tables]]` sections, dotted and quoted keys, inline tables, and heterogeneous arrays.
- Comments, multiline basic and literal strings, numeric bases, special floating-point values, and all TOML date/time forms when reading.
- All shared ShapeShift converters and policies, including naming policies, generated surrogates, attributed unions, default-value omission, and strict duplicate/required-member validation.
- Trimmed and NativeAOT applications without reflection-based TOML object mapping.

For example, nested objects and lists of objects produce Cargo-style sections:

```toml
[package]
name = "shape-shift"
version = "1.0.0"

[dependencies]
serde = { version = "1", features = ["derive"] }

[[bin]]
name = "shape-shift"
path = "src/main.rs"
```

## Wire representations

TOML tables are encoded as maps with nested keys. Arrays are encoded as vectors. Scalar types are written in their native TOML representations:

- **Booleans**: `true` or `false` (lowercase).
- **Integers**: Signed 64-bit values. The reader accepts decimal, hexadecimal, octal, and binary TOML forms; the writer emits decimal.
- **Floats**: IEEE 754 binary64 values. The reader accepts TOML decimal and exponent forms; the writer also emits `nan`, `inf`, and `-inf` when appropriate.
- **Strings**: The reader accepts basic, literal, and multiline strings. The writer emits escaped basic strings.
- **Dates and times**: The reader accepts offset date-time, local date-time, local date, and local time values. `DateTime` values are written with round-trip precision.

## TOML limitations

Unlike JSON or MessagePack, TOML:

- Has no null, binary, or duration scalar. Null object properties are omitted; null array elements and other unsupported values are rejected.
- Always has a table at the document root. Bare scalar or array documents are rejected.
- Limits integers to signed 64-bit values and floating-point values to binary64.
- Distinguishes strings from numbers and booleans by quoting. Unquoted `true`, `false`, or numeric-looking strings will be parsed as their respective types.
- Cannot represent every `ShapeShiftValue`, because that type includes null and binary values.

## Reader security

Tomlyn validates TOML 1.0 syntax, duplicate keys, and table redefinitions before ShapeShift begins conversion. ShapeShift then enforces missing required constructor arguments and the shared `StartingContext` depth and security limits.
