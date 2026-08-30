# MessagePack

`ShapeShift.MsgPack` implements MessagePack primitives directly while retaining
ShapeShift and PolyType as the object-mapping layer. The package has no
dependency on another MessagePack serializer and is NativeAOT-ready with
source-generated shapes.

[!code-csharp[MsgPackSerialization](../../samples/cs/MsgPackSerialization.cs#MsgPackSerialization)]

`MsgPackSerializer` supports contiguous UTF-8-independent binary input,
potentially segmented `ReadOnlySequence<byte>` input, caller-owned
`IBufferWriter<byte>` output, and incremental, non-buffering asynchronous I/O
for `Stream`, `PipeWriter`, and `PipeReader`. `MsgPackEncoder` and
`MsgPackDecoder` expose their underlying output and unread input for custom
converters.

## Contracts

Object contracts are maps with string keys. This favors version tolerance and
readability in diagnostic tools. Dictionaries with string keys are also maps;
dictionaries with other key types use arrays of two-element `[key, value]`
arrays.

Unknown map entries can be retained with `ShapeShiftExtensionDataAttribute` as
described in [Unknown-property retention](features.md#unknown-property-retention).
Values are captured as `ShapeShiftValue`, preserving MessagePack binary and
numeric token distinctions for forward-compatible round trips.

Byte arrays use the MessagePack binary family. Signed and unsigned integers use
the smallest standard integer representation that preserves their value.
`float` and `double` use MessagePack float32 and float64 respectively.
`DateTime` uses the standard MessagePack timestamp extension and is read as UTC.

## ShapeShift extension types

MessagePack does not define interoperable encodings for several .NET scalar
types. ShapeShift reserves these signed extension type codes:

| Code | Type | Payload |
| ---: | --- | --- |
| -40 | `decimal` | Four big-endian `Int32` values in `decimal.GetBits` order |
| -41 | `Int128` | 16-byte big-endian two's-complement integer |
| -42 | `UInt128` | 16-byte big-endian unsigned integer |
| -43 | `BigInteger` | Variable-length big-endian two's-complement integer |
| -44 | `TimeSpan` | Big-endian signed 64-bit tick count |

Readers validate the extension type and fixed payload lengths. These encodings
are deterministic but require ShapeShift-aware readers; they are not part of
the core MessagePack specification.

## Schema

`MsgPackSerializer.GetContract` produces the format-neutral contract, and
`ShapeShift.Json`'s `JsonSchema.Create` renders it as JSON Schema 2020-12 with
MessagePack annotations such as `x-msgpack-type` and `x-msgpack-extension`. See
[Schema and contract inspection](schema.md#messagepack-annotations).

## Targeted and streaming deserialization

See [Targeted and streaming deserialization](features.md#targeted-and-streaming-deserialization)
for the format-neutral `ShapeShiftPath`, `TrySeek`, fragment deserialization,
and sequence/document reader APIs, all of which `MsgPackDecoder` supports.

Unlike JSON, MessagePack values are self-delimiting by design: a buffer
containing several concatenated top-level values is already a valid stream
with no special handling required, so `ShapeShiftDocumentReader<T>` simply
reads values from wherever `MsgPackDecoder`'s current position (`Remaining`)
leaves off after each one.

## Async I/O without sync-over-async

`MsgPackSerializer` exposes `SerializeAsync`/`DeserializeAsync` overloads for
`Stream`, `PipeWriter`, and `PipeReader`, plus `DeserializeAllAsync` for a
sequence of concatenated top-level values, all without ever calling
`.Wait()`, `.Result`, or `GetAwaiter().GetResult()` on synchronous work, and
without a fake-async `Stream.ReadAsync`-into-a-single-buffer equivalent:

[!code-csharp[MsgPackAsyncStreaming](../../samples/cs/MsgPackAsyncStreaming.cs#MsgPackAsyncStreaming)]

Serialization writes the value once (via the existing synchronous
`Serialize(IBufferWriter<byte>, ...)` conversion) and then flushes the
`PipeWriter`/`Stream` asynchronously. Deserialization instead reads a
`PipeReader`/`Stream` incrementally: a `MsgPackValueBoundaryScanner` walks the
MessagePack type-tag/length framing well enough to recognize, without fully
decoding, when one complete top-level value has been buffered. Because
MessagePack has no whitespace or separator between values, every byte the
scanner examines is unconditionally part of the value in progress, so no
bytes are ever released back to the pipe before the value is complete. Only
then does the existing synchronous `MsgPackDecoder` run once, over that
value's bytes. `maxBufferedSize` bounds how large a single value's buffered
span may grow while still unresolved, guarding against a value that never
completes (for example, a truncated payload, or a hostile, unbounded nested
container or binary/string length prefix). All overloads accept a
`CancellationToken` and use `ConfigureAwait(false)` throughout.
