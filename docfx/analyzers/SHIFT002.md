# SHIFT002: Converter type cannot be activated

| Property | Value |
| -------- | ----- |
| **Rule ID** | SHIFT002 |
| **Category** | ShapeShift.Usage |
| **Default severity** | Error |
| **Enabled by default** | Yes |
| **Code fix** | Make an existing parameterless constructor public |

## Cause

A converter named by <xref:ShapeShift.ShapeShiftConverterAttribute> is abstract,
or it has no public parameterless constructor.

## Rule description

ShapeShift activates an attribute-specified converter through its public
parameterless constructor. A converter that is abstract, that declares only
parameterized constructors, or whose parameterless constructor is not public
cannot be activated, and serialization of the annotated type throws a
<xref:ShapeShift.ShapeShiftSerializationException>.

Open generic converter types are not analyzed, because ShapeShift resolves those
through PolyType associated type shapes.

## How to fix violations

Give the converter a public parameterless constructor.

```csharp
// Violation
public class PersonConverter : ShapeShiftConverter<Person, JsonEncoder, JsonDecoder>
{
    private PersonConverter() { }
    // ...
}
```

```csharp
// Fixed
public class PersonConverter : ShapeShiftConverter<Person, JsonEncoder, JsonDecoder>
{
    public PersonConverter() { }
    // ...
}
```

If the converter genuinely requires construction arguments, do not use the
attribute. Register a fully constructed instance on the serializer instead:

```csharp
JsonSerializer serializer = new() { Converters = [new PersonConverter(dependency)] };
```

A converter may also declare a constructor that takes a
<xref:ShapeShift.ConverterContext`2>; that form is available through the
serializer's converter-type collection rather than through the attribute.

## Code fix

When the converter already declares a non-public parameterless constructor, the
IDE offers to widen it to `public`. No fix is offered when no parameterless
constructor exists, because adding one could skip initialization the author
requires.

## When to suppress warnings

Do not suppress this diagnostic. The annotated type cannot be serialized while
the violation remains.

## See also

- [Diagnostics](../docs/diagnostics.md)
- [SHIFT001](SHIFT001.md)
- [SHIFT003](SHIFT003.md)
