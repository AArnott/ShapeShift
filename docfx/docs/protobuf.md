# Protobuf-style binary encoding

The `ShapeShift.Protobuf` package maps PolyType contracts to the protobuf-style binary encoding implemented by this package.

Install the `ShapeShift.Protobuf` package and annotate serialized root types with PolyType's `GenerateShapeAttribute`:

```csharp
using PolyType;
using ShapeShift.Protobuf;

[GenerateShape]
public partial record Person(string Name, int Age);

ProtobufSerializer serializer = new();
byte[] payload = serializer.Serialize(new Person("Ada", 37));
Person? actual = serializer.Deserialize<Person>(payload);
```

`ProtobufSerializer` supports serialization to and from byte arrays while retaining ShapeShift's shared converter and PolyType infrastructure. This keeps serialization compatible with trimmed and NativeAOT applications when using source-generated shapes.
