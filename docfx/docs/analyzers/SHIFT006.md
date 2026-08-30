# SHIFT006: Ambiguous serialized name under a naming policy

| Property | Value |
| -------- | ----- |
| **Rule ID** | SHIFT006 |
| **Category** | ShapeShift.Usage |
| **Default severity** | Info |
| **Enabled by default** | Yes |
| **Code fix** | None |

## Cause

Two serialized members of a type annotated with PolyType's `[GenerateShape]`
have names that differ only by letter casing.

## Rule description

Every built-in <xref:ShapeShift.ShapeShiftNamingPolicy> normalizes letter
casing — camelCase, PascalCase, kebab-case and snake_case all do. Two members
whose declared names differ only by casing therefore map to a single serialized
name as soon as a serializer sets `PropertyNamingPolicy`, and the type can no
longer round-trip.

The diagnostic is reported at `Info` severity because the naming policy is a
run-time property of the serializer instance. A project that never configures
one is unaffected, so this is advice rather than an error.

Only casing differences are reported. Collisions that would require a specific
policy's word-separator rules to materialize are not statically knowable from
the type declaration alone and are not reported.

Names supplied through `[PropertyShape(Name = "...")]` are written verbatim and
are never transformed by a naming policy, so they never participate in this
diagnostic. A name collision involving such an attribute is reported as
[SHIFT005](SHIFT005.md) instead.

## How to fix violations

Rename one of the members, or pin its serialized name with an attribute so that
no policy applies to it.

```csharp
// Violation
[GenerateShape]
public partial class Person
{
    public int Id { get; set; }

    public int ID { get; set; }
}
```

```csharp
// Fixed
[GenerateShape]
public partial class Person
{
    public int Id { get; set; }

    [PropertyShape(Name = "externalId")]
    public int ID { get; set; }
}
```

## When to suppress warnings

Suppress this diagnostic when the type is never serialized by a serializer that
sets a naming policy:

```ini
dotnet_diagnostic.SHIFT006.severity = none
```

Projects that do configure a naming policy should consider escalating it:

```ini
dotnet_diagnostic.SHIFT006.severity = warning
```

## See also

- [Diagnostics](../diagnostics.md)
- [SHIFT005](SHIFT005.md)
