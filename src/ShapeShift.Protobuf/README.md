# ShapeShift.Protobuf

A protobuf-style binary format package for ShapeShift.

This package follows the same thin-adapter model as the other format packages in this repository: the shared converter layer decides the ShapeShift token stream, and the protobuf encoder/decoder converts those tokens to and from a compact binary wire format.

## Usage

```csharp
using ShapeShift.Protobuf;

ProtobufSerializer serializer = new();
byte[] payload = serializer.Serialize(new Person("Ada", 37));
Person? actual = serializer.Deserialize<Person>(payload);
```
