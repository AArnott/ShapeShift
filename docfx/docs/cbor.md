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
fraction tags. `DateTime` is written as CBOR tag 0 (an RFC 3339 date/time
string). Readers also accept tag 1, the number of seconds from the Unix epoch
(RFC 8949 section 3.4.2). `TimeSpan` is represented as a signed 64-bit tick count.

`CborEncoder` and `CborDecoder` expose their underlying
`System.Formats.Cbor` reader/writer for custom converters. ShapeShift's shared
converter policies, contracts, naming, unknown-property retention, and
source-generated PolyType shapes work the same way as for the other format
packages.
