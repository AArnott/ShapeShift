# SHIFT003: Converter type converts a different data type

| Property | Value |
| -------- | ----- |
| **Rule ID** | SHIFT003 |
| **Category** | ShapeShift.Usage |
| **Default severity** | Error |
| **Enabled by default** | Yes |
| **Code fix** | None |

## Cause

A converter named by <xref:ShapeShift.ShapeShiftConverterAttribute> converts a
data type other than the annotated type, or the type of the annotated property,
field or parameter.

## Rule description

ShapeShift casts the activated converter to
`ShapeShiftConverter<TDeclaredType, TEncoder, TDecoder>`. That type is
invariant, so the converter's data type must match exactly. A converter for a
base type cannot serve a derived type, and a converter for a derived type cannot
serve a base type.

Nullable reference annotations are ignored: a converter for `Person` may be
applied to a `Person?` member.

Open generic converter types are not analyzed, because ShapeShift resolves those
through PolyType associated type shapes.

## How to fix violations

Point the attribute at a converter whose data type matches the annotated
declaration, or change the converter's base type.

```csharp
// Violation: the converter converts Person, but is applied to Animal.
[ShapeShiftConverter(typeof(PersonConverter))]
public class Animal { }
```

```csharp
// Fixed
[ShapeShiftConverter(typeof(AnimalConverter))]
public class Animal { }

public class AnimalConverter : ShapeShiftConverter<Animal, JsonEncoder, JsonDecoder>
{
    // ...
}
```

For polymorphic hierarchies, describe the hierarchy with PolyType's
`[DerivedTypeShape]` attributes rather than pointing several types at a single
base-type converter.

## When to suppress warnings

Do not suppress this diagnostic. The annotated declaration cannot be serialized
while the violation remains.

## See also

- [Diagnostics](../diagnostics.md)
- [SHIFT001](SHIFT001.md)
- [SHIFT002](SHIFT002.md)
