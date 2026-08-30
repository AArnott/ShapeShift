# SHIFT005: Ambiguous serialized name

| Property | Value |
| -------- | ----- |
| **Rule ID** | SHIFT005 |
| **Category** | ShapeShift.Usage |
| **Default severity** | Warning |
| **Enabled by default** | Yes |
| **Code fix** | None |

## Cause

Two serialized members of a type annotated with PolyType's `[GenerateShape]`
produce the same serialized name.

## Rule description

Serialized names must be unique within a type. Because C# already forbids two
members with the same identifier, this collision always involves at least one
`[PropertyShape(Name = "...")]` attribute.

At run time, ShapeShift rejects the ambiguity: the converter cannot register two
readers or writers under one name, and a document that contains the name twice
is rejected as a duplicate property.

The analyzer examines only the members ShapeShift serializes by default: public
instance properties with a public getter, and public instance fields. Members
marked `[PropertyShape(Ignore = true)]` and the
<xref:ShapeShift.ShapeShiftExtensionDataAttribute> member are excluded.

## How to fix violations

Give each member a distinct serialized name.

```csharp
// Violation
[GenerateShape]
public partial class Person
{
    public string? Name { get; set; }

    [PropertyShape(Name = "Name")]
    public string? Alias { get; set; }
}
```

```csharp
// Fixed
[GenerateShape]
public partial class Person
{
    public string? Name { get; set; }

    [PropertyShape(Name = "alias")]
    public string? Alias { get; set; }
}
```

If one of the members should not appear on the wire at all, mark it
`[PropertyShape(Ignore = true)]`.

## When to suppress warnings

Do not suppress this diagnostic. There is no configuration under which both
members can round-trip.

## See also

- [Diagnostics](../diagnostics.md)
- [SHIFT006](SHIFT006.md)
