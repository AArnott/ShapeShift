# ShapeShift.MsgPack

NativeAOT-friendly MessagePack serialization using ShapeShift's shared PolyType
contracts and converters.

```csharp
[GenerateShape]
partial record Person(string Name);

var serializer = new ShapeShift.MsgPack.MsgPackSerializer();
byte[] messagePack = serializer.Serialize(new Person("Ada"));
Person? copy = serializer.Deserialize<Person>(messagePack);
```

The package directly implements MessagePack primitives, including binary values
and the standard timestamp extension. See the
[MessagePack documentation](../../docfx/docs/msgpack.md) for wire contracts and
ShapeShift's reserved scalar extension codes.
