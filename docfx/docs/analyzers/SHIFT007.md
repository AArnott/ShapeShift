# SHIFT007: Reflection-based activation is not trimming or NativeAOT safe

| Property | Value |
| -------- | ----- |
| **Rule ID** | SHIFT007 |
| **Category** | ShapeShift.Reliability |
| **Default severity** | Info |
| **Enabled by default** | Yes |
| **Code fix** | None |

## Cause

Code calls `WithReflectionConverterTypes` on a ShapeShift serializer, or uses
PolyType's `ReflectionTypeShapeProvider`.

## Rule description

ShapeShift's default path — converter instances, converter factories and
source-generated type shapes — is free of reflection and safe for trimming and
NativeAOT. Two opt-ins step outside that guarantee:

- `WithReflectionConverterTypes` activates converter `Type` objects through
  reflection, which may need to construct closed generic types at run time.
- `ReflectionTypeShapeProvider` derives contracts by reflecting over types
  instead of using generated shapes.

Both are supported and deliberately explicit, but a trimmed or NativeAOT
deployment can fail at run time unless every constructor and member they reach
is rooted.

The diagnostic is reported at `Info` severity so that the opt-in stays visible
during review without failing builds that intend to use it.

## How to fix violations

Prefer registering converter instances or factories, and source-generated
shapes:

```csharp
// Reflection-based opt-in
JsonSerializer serializer = (JsonSerializer)new JsonSerializer()
    .WithReflectionConverterTypes([typeof(PersonConverter)]);
```

```csharp
// NativeAOT-safe
JsonSerializer serializer = new() { Converters = [new PersonConverter()] };
```

For type shapes, apply `[GenerateShape]` to your types or declare a
`[GenerateShapeFor<T>]` witness instead of using
`ReflectionTypeShapeProvider.Default`. See [SHIFT004](SHIFT004.md).

## When to suppress warnings

Suppress this diagnostic in applications that are never trimmed or published as
NativeAOT and that deliberately use the reflection opt-in:

```ini
dotnet_diagnostic.SHIFT007.severity = none
```

## See also

- [Diagnostics](../diagnostics.md)
- [SHIFT004](SHIFT004.md)
