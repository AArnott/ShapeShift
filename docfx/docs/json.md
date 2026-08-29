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
- Incremental, non-buffering asynchronous I/O for `Stream`, `PipeWriter`, and
  `PipeReader`.
- Optional indentation.
- Configurable comment and trailing-comma handling.
- Explicit opt-in support for `"NaN"`, `"Infinity"`, and `"-Infinity"`.
- All shared ShapeShift converters and policies, including naming policies,
  generated surrogates, attributed unions, default-value omission, and strict
  duplicate/required-member validation.
- The format-neutral `ShapeShiftValue` tree for untyped JSON.
- NativeAOT-safe `JsonElement`, `JsonDocument`, and `JsonNode` pass-through
  converters.
- Unknown-property capture and round trips through
  `ShapeShiftExtensionDataAttribute`.

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

`ShapeShiftBinary` is written as base64 JSON text. Because JSON has no binary
token, untyped JSON deserialization reads such text as `ShapeShiftString`;
applications that require round-trip type identity should place binary data in
a strongly typed contract.

Non-finite floating-point values are rejected by default because JSON has no
standard representation for them. Set `AllowNamedFloatingPointValues` to
`true` to write and accept the strings `"NaN"`, `"Infinity"`, and
`"-Infinity"`.

## Reader security

ShapeShift rejects duplicate object properties and missing required constructor
arguments by default. The shared `StartingContext` controls maximum depth and
collection length. Comments and trailing commas remain disabled unless
explicitly enabled.

## Targeted and streaming deserialization

See [Targeted and streaming deserialization](features.md#targeted-and-streaming-deserialization)
for the format-neutral `ShapeShiftPath`, `TrySeek`, fragment deserialization,
and sequence/document reader APIs, all of which `JsonDecoder` supports.

`Utf8JsonReader` only supports a single top-level JSON value per instance.
`JsonDecoder` transparently constructs a fresh reader over the unconsumed
input whenever a `ShapeShiftDocumentReader<T>` (or any other caller) reads
past one top-level value into genuine further content, so a single
`JsonDecoder` can walk an entire newline-delimited JSON (NDJSON) stream, or
any other buffer of concatenated top-level values, without the caller
reconstructing anything itself.

## Async I/O without sync-over-async

`JsonSerializer` exposes `SerializeAsync`/`DeserializeAsync` overloads for
`Stream`, `PipeWriter`, and `PipeReader`, plus `DeserializeAllAsync` for a
sequence of concatenated top-level values (such as NDJSON), all without ever
calling `.Wait()`, `.Result`, or `GetAwaiter().GetResult()` on synchronous
work, and without a fake-async `TextReader.ReadToEnd` equivalent:

[!code-csharp[JsonAsyncStreaming](../../samples/cs/JsonAsyncStreaming.cs#JsonAsyncStreaming)]

Serialization writes the value once (via the existing synchronous
`Serialize(IBufferWriter<byte>, ...)` conversion) and then flushes the
`PipeWriter`/`Stream` asynchronously. Deserialization instead reads a
`PipeReader`/`Stream` incrementally: a `JsonValueBoundaryScanner` drives
`Utf8JsonReader`'s own incremental-parsing support (`JsonReaderState`
resumption and `TrySkip()`) to recognize, without fully decoding, when one
complete top-level value has been buffered, releasing insignificant
whitespace between values back to the pipe as soon as it is confirmed safe to
discard. Only then does the existing synchronous `JsonDecoder` run once, over
that value's bytes. `maxBufferedSize` bounds how large a single value's
buffered span may grow while still unresolved, guarding against a value that
never completes (for example, a truncated payload or a hostile, unbounded
input) without capping the size of the whitespace that may separate
well-formed values in a long-running NDJSON-style sequence. All overloads
accept a `CancellationToken` and use `ConfigureAwait(false)` throughout.
