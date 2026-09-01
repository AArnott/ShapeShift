# SHIFT001: Converter type is not a ShapeShift converter

| Property | Value |
| -------- | ----- |
| **Rule ID** | SHIFT001 |
| **Category** | ShapeShift.Usage |
| **Default severity** | Error |
| **Enabled by default** | Yes |
| **Code fix** | None |

## Cause

A type named by <xref:ShapeShift.ShapeShiftConverterAttribute> does not derive
from <xref:ShapeShift.ShapeShiftConverter`3>.

## Rule description

ShapeShift activates the type named by the attribute and casts the result to a
converter. When the type is not a converter at all, the cast fails and
serialization throws an `InvalidCastException` wrapped in a
<xref:ShapeShift.ShapeShiftSerializationException> the first time the annotated
type is serialized — potentially long after the code was written.

Open generic converter types are not analyzed, because ShapeShift resolves those
through PolyType associated type shapes.

## How to fix violations

Derive the converter from `ShapeShiftConverter<T, TEncoder, TDecoder>` for the
encoder and decoder of the format you are targeting.

```csharp
// Violation
[ShapeShiftConverter(typeof(PersonConverter))]
public class Person { }

public class PersonConverter { }
```

```csharp
// Fixed
[ShapeShiftConverter(typeof(PersonConverter))]
public class Person { }

public class PersonConverter : ShapeShiftConverter<Person, JsonEncoder, JsonDecoder>
{
    public override Person? Read(ref JsonDecoder decoder, SerializationContext<JsonEncoder, JsonDecoder> context) => new();

    public override void Write(ref JsonEncoder encoder, in Person? value, SerializationContext<JsonEncoder, JsonDecoder> context) => encoder.WriteNull();
}
```

## When to suppress warnings

Do not suppress this diagnostic. The annotated type cannot be serialized while
the violation remains.

## See also

- [Diagnostics](../docs/diagnostics.md)
- [SHIFT002](SHIFT002.md)
- [SHIFT003](SHIFT003.md)
