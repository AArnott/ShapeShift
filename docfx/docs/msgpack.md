# MessagePack

`ShapeShift.MsgPack` implements MessagePack primitives directly while retaining
ShapeShift and PolyType as the object-mapping layer. The package has no
dependency on another MessagePack serializer and is NativeAOT-ready with
source-generated shapes.

[!code-csharp[MsgPackSerialization](../../samples/cs/MsgPackSerialization.cs#MsgPackSerialization)]

`MsgPackSerializer` supports contiguous binary input, potentially segmented
`ReadOnlySequence<byte>` input, caller-owned `IBufferWriter<byte>` output, and
incremental, non-buffering asynchronous I/O for `Stream`, `PipeWriter`, and
`PipeReader`. `MsgPackEncoder` and `MsgPackDecoder` expose their underlying
output and unread input, plus low-level array, map, extension, and raw-bytes
primitives, for custom converters.

## Contracts

Object contracts are maps with string keys by default. This favors version
tolerance and readability in diagnostic tools. Dictionaries with string keys are
also maps; dictionaries with other key types use arrays of two-element
`[key, value]` arrays.

Unknown map entries can be retained with `ShapeShiftExtensionDataAttribute` as
described in [Unknown-property retention](features.md#unknown-property-retention).
Values are captured as `ShapeShiftValue`, preserving MessagePack binary and
numeric token distinctions for forward-compatible round trips.

Byte arrays use the MessagePack binary family. Signed and unsigned integers use
the smallest standard integer representation that preserves their value.
`float` and `double` use MessagePack float32 and float64 respectively.
`DateTime` uses the standard MessagePack timestamp extension and is read as UTC.

## Positional (array) contracts

A type that declares `MsgPackArrayContractAttribute` is written as an array
whose elements are identified by the positions `MsgPackKeyAttribute` assigns,
instead of as a map keyed by property name. Property names never reach the wire,
which shrinks small records substantially.

[!code-csharp[PositionalContract](../../samples/cs/MsgPackPositionalContracts.cs#PositionalContract)]

Positions are a permanent part of the contract, and the rules that keep them
usable across versions are enforced when the converter is built rather than
discovered in production:

1. Every serializable member needs an explicit `[MsgPackKey]`. There is no
   implicit ordering to accidentally depend on, and no way for a member to slip
   into a payload without a stable position.
2. A key belongs to its member forever. Retire keys; never reuse or reorder
   them. Reusing a key silently reinterprets old payloads.
3. New members take keys above every key already in use.
4. A retired key becomes a hole, written as a `nil` placeholder whenever a later
   position is still written, so every later position stays where it belongs.
5. A reader accepts a shorter array (members at the missing positions keep their
   defaults, subject to required-member validation) and a longer one (surplus
   elements are skipped). That is what makes appending a member compatible in
   both directions.

[!code-csharp[PositionalVersioning](../../samples/cs/MsgPackPositionalContracts.cs#PositionalVersioning)]

Keys range from `0` to `MsgPackKeyAttribute.MaxIndex` (1023). Every position
below the highest one in use costs at least one byte on the wire even when
nothing occupies it, so the bound keeps a typo from turning a small object into
an enormous array.

### Omitted and default values

A MessagePack array cannot express "this interior element is absent" as distinct
from "this element is null". A positional contract therefore **declines**
`SerializeDefaultValuesPolicy` omission for interior positions: those members are
always written, at their real values, even when the value is the default.

Omission is honored only for the *tail* of the array, where a shorter array is
an unambiguous statement that the remaining positions were not written. Required
members are never elided even there, because a reader could not reconstruct the
object without them. The result is that no payload is ever ambiguous about which
positions it carries, while `SerializeDefaultValuesPolicy.Never` still delivers
most of its benefit for the trailing optional members it usually targets.

### Unsupported combinations

Positional contracts reject, at converter-construction time, the cases they
could not honor faithfully:

| Rejected | Reason |
| --- | --- |
| A member with no `[MsgPackKey]` | Its position would not be stable. |
| Two members claiming one key | The payload would be ambiguous. |
| A key outside `0`..`MsgPackKeyAttribute.MaxIndex` | A sparse array of that size is never intended. |
| `[ShapeShiftExtensionData]` on the same type | Unknown positions have no names to retain them under. |
| `[ShapeShiftConverter]` on a *member* | Positional members resolve their converter from the member's type; apply the converter to the type, or register it with the serializer. |

Custom converters registered for a type, converter factories, and
`[ShapeShiftConverter]` applied to a *type* all work normally inside a
positional contract.

## Reference preservation

`PreserveReferences` writes each object once and refers back to it afterwards,
so a graph that shares objects stays a graph.

[!code-csharp[ReferencePreservation](../../samples/cs/MsgPackReferencePreservation.cs#ReferencePreservation)]

`ReferencePreservationMode.RejectCycles` preserves identity and rejects cycles.
`AllowCycles` additionally reconstructs graphs that refer back to themselves;
see the remarks on that member for the constructor limitations that apply to
types participating in a cycle, and for the denial-of-service consideration that
comes with accepting cyclic graphs from untrusted senders.

[!code-csharp[ReferenceCycles](../../samples/cs/MsgPackReferencePreservation.cs#ReferenceCycles)]

A reference is written as the reserved `105` extension carrying the narrowest
big-endian unsigned identifier that fits (1, 2, or 4 bytes), for a total of 3 to
6 bytes. Readers reject any other payload width, and a reader with reference
preservation turned off reports the reference extension by name rather than
failing with a generic type error. Because references are a runtime protocol
rather than a static shape, `GetContract` throws `NotSupportedException` while
`PreserveReferences` is enabled.

## Extension types

The MessagePack specification splits the signed 8-bit extension type space in
two: codes `0` through `127` are application specific, while negative codes are
reserved for the specification itself. ShapeShift therefore places every encoding
it invents in the application-specific half, and reserves a contiguous block,
`100` through `109`, so that future ShapeShift features never have to negotiate
with codes an application may already be using.

| Code | Meaning | Payload |
| ---: | --- | --- |
| -1 | `DateTime`, `DateTimeOffset` instant | The standard MessagePack timestamp (4, 8, or 12 bytes) |
| 100 | `decimal` | Four big-endian `Int32` values in `decimal.GetBits` order (16 bytes) |
| 101 | `Int128` | 16-byte big-endian two's complement integer |
| 102 | `UInt128` | 16-byte big-endian unsigned integer |
| 103 | `BigInteger` | Variable-length big-endian two's complement integer |
| 104 | `TimeSpan` | Big-endian signed 64-bit tick count (8 bytes) |
| 105 | Object reference | 1, 2, or 4-byte big-endian unsigned identifier |
| 106-109 | Reserved for future ShapeShift use | Rejected by readers |

These constants are available as `MsgPackExtensionCodes`, along with
`IsReservedByShapeShift` for code that needs to steer clear of the block.

Readers validate both the extension type code and the payload length, and report
a reserved extension found where it does not belong by naming the feature that
produced it, so a payload written with (say) reference preservation enabled
produces an actionable error rather than a confusing one when read without it.

Extension codes outside the reserved block are opaque to ShapeShift: they decode
as binary values so that an application's own extensions survive an
unknown-data round trip, and they are never produced by ShapeShift itself.
Custom converters can read and write them through
`MsgPackEncoder.WriteExtension`, `MsgPackDecoder.TryPeekExtensionHeader`, and
`MsgPackDecoder.ReadExtension`.

Aside from the specification's timestamp, these encodings are ShapeShift-specific
and require a ShapeShift-aware reader; they are deterministic, but they are not
part of the core MessagePack specification.

## Schema

`MsgPackSerializer.GetContract` produces the format-neutral contract, and
`ShapeShift.Json`'s `JsonSchema.Create` renders it as JSON Schema 2020-12 with
MessagePack annotations such as `x-msgpack-type` and `x-msgpack-extension`. A
positional contract is projected as an array of `prefixItems`, with `null`
placeholders standing in for positions no member claims. See
[Schema and contract inspection](schema.md#messagepack-annotations).

## Segmented buffers and no-copy reads

`MsgPackDecoder` reads a `ReadOnlySequence<byte>` in place. It never
consolidates a segmented sequence into one contiguous buffer, so skipping over
(or seeking past) content a caller does not want costs nothing but pointer
arithmetic, no matter how the input is chopped into segments. Only a value that
is actually materialized -- a string, a byte array, an extension payload -- is
copied, and even then only when that one value straddles a segment boundary.

That property is what makes a targeted read over a pipe worthwhile: pulling one
small field out of a multi-megabyte segmented document allocates only the field.

Length and count headers are validated against the input that remains before
anything is allocated for them, so a corrupt or hostile 32-bit length cannot
provoke an enormous allocation or overflow a counter.

## Targeted and streaming deserialization

See [Targeted and streaming deserialization](features.md#targeted-and-streaming-deserialization)
for the format-neutral `ShapeShiftPath`, `TrySeek`, fragment deserialization,
and sequence/document reader APIs, all of which `MsgPackDecoder` supports.

Unlike JSON, MessagePack values are self-delimiting by design: a buffer
containing several concatenated top-level values is already a valid stream
with no special handling required, so `ShapeShiftDocumentReader<T>` simply
reads values from wherever `MsgPackDecoder`'s current position leaves off after
each one.

`TryDeserializeFragmentAsync` extends targeted reads to a `PipeReader` or
`Stream`. It buffers only the enclosing top-level value -- a value's extent
cannot be known before its framing has been walked -- and then seeks the path
directly over the pipe's own segments.

## Async I/O without sync-over-async

`MsgPackSerializer` exposes `SerializeAsync`/`DeserializeAsync` overloads for
`Stream`, `PipeWriter`, and `PipeReader`, plus `SerializeAllAsync` and
`DeserializeAllAsync` for an endless sequence of concatenated top-level values,
all without ever calling `.Wait()`, `.Result`, or `GetAwaiter().GetResult()` on
synchronous work, and without a fake-async `Stream.ReadAsync`-into-a-single-buffer
equivalent:

[!code-csharp[MsgPackAsyncStreaming](../../samples/cs/MsgPackAsyncStreaming.cs#MsgPackAsyncStreaming)]

Serialization writes the value once (via the existing synchronous
`Serialize(IBufferWriter<byte>, ...)` conversion) and then flushes the
`PipeWriter`/`Stream` asynchronously; `SerializeAllAsync` flushes between values
so a slow consumer applies backpressure rather than letting an unbounded buffer
accumulate. Deserialization instead reads a `PipeReader`/`Stream`
incrementally: a `MsgPackValueBoundaryScanner` walks the MessagePack
type-tag/length framing well enough to recognize, without fully decoding, when
one complete top-level value has been buffered. Because MessagePack has no
whitespace or separator between values, every byte the scanner examines is
unconditionally part of the value in progress, so no bytes are ever released
back to the pipe before the value is complete. Only then does the existing
synchronous `MsgPackDecoder` run once, over that value's bytes.
`maxBufferedSize` bounds how large a single value's buffered span may grow while
still unresolved, guarding against a value that never completes (for example, a
truncated payload, or a hostile, unbounded nested container or binary/string
length prefix). All overloads accept a `CancellationToken` and use
`ConfigureAwait(false)` throughout.

[!code-csharp[EndlessStreaming](../../samples/cs/MsgPackFramedStreaming.cs#EndlessStreaming)]

## Framed streams

MessagePack values are self-delimiting, so a stream of concatenated values needs
no framing at all. Framing earns its four bytes when a transport must know a
message's extent *before* anything parses it: to hand a whole message off to
another component, to reject an implausibly large message without decoding it,
to skip a message whose contract the receiver does not implement, or to
interleave MessagePack with other content on one connection.

A frame is a `MsgPackFraming.LengthPrefixByteCount` (4) byte big-endian unsigned
length, followed by exactly that many bytes, which must contain exactly one
complete MessagePack value.

[!code-csharp[Framing](../../samples/cs/MsgPackFramedStreaming.cs#Framing)]

`SerializeFrameAsync`, `DeserializeFrameAsync`, and `DeserializeAllFramesAsync`
work against both `PipeWriter`/`PipeReader` and `Stream`. Readers:

- reject a frame whose declared length exceeds `maxFrameLength` using the length
  prefix alone, before any of the frame is buffered, so an attacker-controlled
  prefix cannot make a reader wait for (and buffer) a gigabyte;
- reject a stream that ends inside a frame with a `DecoderException`, while
  ending gracefully at a frame boundary; and
- reject frame content that is not exactly one complete MessagePack value.

Writing a frame is the one place framing costs more than four bytes: the length
prefix cannot be written until the value's length is known, so the value is
converted into a scratch buffer first.

[!code-csharp[TargetedAsyncRead](../../samples/cs/MsgPackFramedStreaming.cs#TargetedAsyncRead)]

## Samples

- [MsgPackSerialization.cs](https://github.com/AArnott/ShapeShift/blob/main/samples/cs/MsgPackSerialization.cs)
- [MsgPackPositionalContracts.cs](https://github.com/AArnott/ShapeShift/blob/main/samples/cs/MsgPackPositionalContracts.cs)
- [MsgPackReferencePreservation.cs](https://github.com/AArnott/ShapeShift/blob/main/samples/cs/MsgPackReferencePreservation.cs)
- [MsgPackAsyncStreaming.cs](https://github.com/AArnott/ShapeShift/blob/main/samples/cs/MsgPackAsyncStreaming.cs)
- [MsgPackFramedStreaming.cs](https://github.com/AArnott/ShapeShift/blob/main/samples/cs/MsgPackFramedStreaming.cs)
