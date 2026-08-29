# MessagePack

`ShapeShift.MsgPack` implements MessagePack primitives directly while retaining
ShapeShift and PolyType as the object-mapping layer. The package has no
dependency on another MessagePack serializer and is NativeAOT-ready with
source-generated shapes.

[!code-csharp[MsgPackSerialization](../../samples/cs/MsgPackSerialization.cs#MsgPackSerialization)]

`MsgPackSerializer` supports contiguous UTF-8-independent binary input,
potentially segmented `ReadOnlySequence<byte>` input, caller-owned
`IBufferWriter<byte>` output, and `Stream` convenience methods. `MsgPackEncoder`
and `MsgPackDecoder` expose their underlying output and unread input for custom
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

## Current stream behavior

The stream convenience methods buffer one complete value. Incremental
`PipeReader`/`PipeWriter`, framing, and endless top-level streaming APIs are
tracked as a separate milestone.
