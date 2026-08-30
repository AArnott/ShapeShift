# SHIFT008: Unsupported ShapeShift contract

| Property | Value |
| -------- | ----- |
| **Rule ID** | SHIFT008 |
| **Category** | ShapeShift.Usage |
| **Default severity** | Error |
| **Enabled by default** | Yes |
| **Code fix** | None |

## Cause

A type declares an extension-data contract that ShapeShift cannot build a
converter for.

## Rule description

<xref:ShapeShift.ShapeShiftExtensionDataAttribute> captures unknown properties so
that a document round-trips without losing forward-compatible data. The contract
has four requirements, each of which ShapeShift enforces at run time by throwing
a <xref:ShapeShift.ShapeShiftSerializationException> while preparing the
converter graph:

1. A type may declare at most one extension-data member.
2. The member's type must be exactly `Dictionary<string, ShapeShiftValue>`.
3. The member must have a getter.
4. A class that declares an extension-data member must be deserializable through
   a parameterless constructor.

This analyzer reports each violation on the attribute application.

## How to fix violations

```csharp
// Violation: wrong member type, and no parameterless constructor.
public class Extensible
{
    public Extensible(int required) { }

    [ShapeShiftExtensionData]
    public Dictionary<string, string> Extra { get; } = new();
}
```

```csharp
// Fixed
[GenerateShape]
public partial class Extensible
{
    public int Required { get; set; }

    [ShapeShiftExtensionData]
    public Dictionary<string, ShapeShiftValue> Extra { get; } = new(StringComparer.Ordinal);
}
```

Structs are never reported for requirement 4, because a struct always has a
parameterless constructor available to the deserializer.

## When to suppress warnings

Do not suppress this diagnostic. The type cannot be serialized while the
violation remains.

## See also

- [Diagnostics](../diagnostics.md)
- [Features](../features.md)
