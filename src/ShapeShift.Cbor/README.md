# ShapeShift.Cbor

`ShapeShift.Cbor` provides NativeAOT-friendly CBOR serialization using
ShapeShift's shared PolyType contracts and converters.

```csharp
[GenerateShape]
partial record Person(string Name);

var serializer = new ShapeShift.Cbor.CborSerializer();
byte[] cbor = serializer.Serialize(new Person("Ada"));
Person? copy = serializer.Deserialize<Person>(cbor);
```

The package writes standard CBOR maps with text keys, arrays, byte strings,
native numeric values, tagged date/times, and tagged bignum or decimal values
where needed for ShapeShift scalar fidelity. See the
[CBOR documentation](../../docfx/docs/cbor.md) for wire details.
