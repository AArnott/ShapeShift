# ShapeShift.Json

NativeAOT-friendly JSON serialization using ShapeShift's shared PolyType
contracts and converters.

```csharp
[GenerateShape]
partial record Person(string Name);

var serializer = new ShapeShift.Json.JsonSerializer();
string json = serializer.Serialize(new Person("Ada"));
Person? copy = serializer.Deserialize<Person>(json);
```

See the [JSON documentation](../../docfx/docs/json.md) for supported APIs,
security defaults, and wire-representation details.
