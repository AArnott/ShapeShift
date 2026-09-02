# TOML

The `ShapeShift.Toml` package maps PolyType contracts to [TOML](https://toml.io/en/) (Tom's Obvious, Minimal Language).

Install the `ShapeShift.Toml` package and annotate serialized root types with PolyType's `GenerateShapeAttribute`:

[!code-csharp[TomlSerialization](../../samples/cs/TomlSerialization.cs#TomlSerialization)]

`TomlSerializer` supports:

- Serialization to and from TOML strings.
- All shared ShapeShift converters and policies, including naming policies, generated surrogates, attributed unions, default-value omission, and strict duplicate/required-member validation.
- The format-neutral `ShapeShiftValue` tree for untyped TOML.

## Wire representations

TOML tables are encoded as maps with nested keys. Arrays are encoded as vectors. Scalar types are written in their native TOML representations:

- **Booleans**: `true` or `false` (lowercase).
- **Integers**: Decimal representation with optional `+` or `-` prefix.
- **Floats**: Decimal representation with optional exponent (`e` or `E`). Supports `nan`, `inf`, and `-inf` for non-finite values.
- **Strings**: Basic strings with standard escaping (`\"`, `\\`, `\n`, `\t`, etc.) or literal strings for raw content.
- **Dates**: TOML offset or local date-time syntax in round-trip precision.
- **Durations**: Quoted invariant .NET duration strings because TOML has no duration scalar.
- **Nulls**: The unquoted `null` extension because TOML has no null scalar.

## TOML limitations

TOML is a text-based format with explicit type distinctions. Unlike JSON or MessagePack, TOML:

- Has no native binary type. `TomlEncoder` and `TomlDecoder` reject binary values.
- Requires table keys at the root level. Bare scalars or arrays at the document root are not valid TOML.
- Distinguishes strings from numbers and booleans by quoting. Unquoted `true`, `false`, or numeric-looking strings will be parsed as their respective types.

## Reader security

ShapeShift rejects duplicate table keys and missing required constructor arguments by default. The shared `StartingContext` controls maximum depth and other security limits.
