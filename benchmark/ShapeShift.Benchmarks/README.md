# Serializer benchmarks

This project measures:

- steady-state serialization and deserialization of deterministic, source-generated object graphs with 1, 100,
  and 1,000 orders. JSON compares ShapeShift with `System.Text.Json`; MessagePack compares ShapeShift with
  `Nerdbank.MessagePack`.
- TAML and YAML deserialization with string interning enabled and disabled for duplicated, unique, and escaped
  string values.

Run a focused screening benchmark after building:

```powershell
dotnet run --project .\benchmark\ShapeShift.Benchmarks\ShapeShift.Benchmarks.csproj --no-build -c Release -- --filter "*SerializerBenchmarks*" --job short
```

Run the string interning benchmarks:

```powershell
dotnet run --project .\benchmark\ShapeShift.Benchmarks\ShapeShift.Benchmarks.csproj --no-build -c Release -f net10.0 -- --filter "*StringInterning*" --job short
```
