# JSON

The `ShapeShift.Json` package maps PolyType contracts to UTF-8 JSON without
delegating object mapping to `System.Text.Json.JsonSerializer`. It is
NativeAOT-compatible when used with source-generated PolyType shapes.

Install the `ShapeShift.Json` package and annotate serialized root types with
PolyType's `GenerateShapeAttribute`:

[!code-csharp[JsonSerialization](../../samples/cs/JsonSerialization.cs#JsonSerialization)]

`JsonSerializer` supports:

- UTF-8 input and output.
- JSON strings.
- Caller-owned `IBufferWriter<byte>` destinations.
- Asynchronous `Stream` convenience methods.
- Optional indentation.
- Configurable comment and trailing-comma handling.
- All shared ShapeShift converters and policies, including naming policies,
  generated surrogates, attributed unions, default-value omission, and strict
  duplicate/required-member validation.

## Wire representations

JSON objects require string property names. Dictionaries with string keys are
therefore encoded as JSON objects. Dictionaries with any other key type are
encoded as arrays of two-element `[key, value]` arrays; this preserves key types
without culture-sensitive or lossy string conversion.

Enums are strings by default and honor PolyType enum aliases. Set
`SerializeEnumValuesByName` to `false` to write their underlying numeric values.
Dates use the ISO 8601 representation produced by `Utf8JsonWriter`, and
`TimeSpan` values use the invariant constant (`c`) format.

`Int128`, `UInt128`, and `BigInteger` values are written as JSON numbers. A
consumer whose number model is limited to IEEE 754 may lose precision.

## Reader security

ShapeShift rejects duplicate object properties and missing required constructor
arguments by default. The shared `StartingContext` controls maximum depth and
collection length. Comments and trailing commas remain disabled unless
explicitly enabled.

The current stream convenience methods buffer one complete JSON document.
Incremental stream and pipeline APIs are tracked separately.
