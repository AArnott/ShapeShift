# CBOR

`ShapeShift.Cbor` writes RFC 8949 CBOR while retaining ShapeShift and PolyType
as the object-mapping layer. It is NativeAOT-friendly when applications use
source-generated shapes.

```csharp
[GenerateShape]
partial record Person(string Name);

var serializer = new ShapeShift.Cbor.CborSerializer();
byte[] cbor = serializer.Serialize(new Person("Ada"));
Person? copy = serializer.Deserialize<Person>(cbor);
```

Object contracts are CBOR maps with text-string keys and collection contracts
are CBOR arrays. Byte arrays use CBOR byte strings. Signed and unsigned
64-bit integers use their native CBOR integer families; values that require a
larger integer or an exact decimal use CBOR's standard bignum and decimal
fraction tags. `DateTime` uses CBOR tag 0 (an RFC 3339 date/time string), and
`TimeSpan` is represented as a signed 64-bit tick count.

## Map keys

RFC 8949 major type 5 allows any data item as a map key. Typed objects and
`Dictionary<string, TValue>` still use text keys. Every other typed dictionary
is still a CBOR array of two-element key/value arrays. Untyped
`ShapeShiftValue` input is different:

- A map whose keys are all text, including an empty map, is a `ShapeShiftMap`.
- A map with any other key is a `ShapeShiftDataMap`. Its entries keep the order
  they had on the wire. Structural equality ignores that order and rejects two
  entries whose keys compare as the same dynamic value.
- Writing a `ShapeShiftDataMap` that contains only text keys produces an
  ordinary text-key map, so it reads back as a `ShapeShiftMap`.

```csharp
ShapeShiftValue value = new ShapeShiftDataMap(
[
    new ShapeShiftDataMapEntry(1UL, "one"),
    new ShapeShiftDataMapEntry(new byte[] { 0x00 }, 2UL),
]);

var serializer = new ShapeShift.Cbor.CborSerializer();
byte[] cbor = serializer.Serialize(value);
ShapeShiftDataMap map = (ShapeShiftDataMap)serializer.Deserialize<ShapeShiftValue>(cbor)!;
```

That value is the definite-length map `{1: "one", h'00': 2}`:

```text
a2 01 63 6f6e65 41 00 02
```

A positive integer is major type 0, so it reads back as
`ShapeShiftUnsignedInteger` even when it was written from a signed `long`.
Negative integers stay `ShapeShiftInteger`. Half-precision numbers, including
negative zero, read as `ShapeShiftFloat` (`double`). Writing a floating-point
value uses the shortest CBOR float encoding that preserves it, so a
half-precision infinity, NaN, or zero is written back as half-precision, while
a value such as `0.1` is written as double-precision. CBOR simple values,
including undefined (simple value 23), read as `ShapeShiftUnsignedInteger` of
their code; writing that value writes an
integer, not a simple value. A tag 1 key is the numeric content of the tag,
because the dynamic model has no date type. A map that contains both an integer
and the same integer under tag 1 therefore has two equal keys and is rejected
when read as `ShapeShiftValue`. The token decoder still accepts that document:
text keys are `TokenType.PropertyName`, and every other key is the token of
the data item itself.

Formats that do not implement `IEncoder.ArbitraryMapKeys` reject a
`ShapeShiftDataMap` that contains a non-text key. The serializer's default
depth limit remains 64, so a map nested deeper than that is rejected by
`CborSerializer` even though a direct `CborDecoder` walk does not apply that
limit.

`CborEncoder` and `CborDecoder` expose their underlying
`System.Formats.Cbor` reader/writer for custom converters. ShapeShift's shared
converter policies, contracts, naming, unknown-property retention, and
source-generated PolyType shapes work the same way as for the other format
packages.
