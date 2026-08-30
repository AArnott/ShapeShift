# SHIFT004: Type has no generated shape

| Property | Value |
| -------- | ----- |
| **Rule ID** | SHIFT004 |
| **Category** | ShapeShift.Usage |
| **Default severity** | Warning |
| **Enabled by default** | Yes |
| **Code fix** | Apply `[GenerateShape]` to the type |

## Cause

A call site supplies a type argument to a method whose type parameter is
constrained to PolyType's `IShapeable<T>`, but the supplied type does not
provide the required shape.

## Rule description

ShapeShift's NativeAOT-ready APIs obtain contracts from PolyType
source-generated shapes. The C# compiler already reports the unsatisfied
constraint, but its message describes an interface conversion rather than the
ShapeShift remedy. This diagnostic names the type that needs a shape and enables
a code fix that applies the attribute for you.

Type arguments that are themselves type parameters are not reported, because the
final type is unknown at that call site.

## How to fix violations

Apply `[GenerateShape]` to the type and make the declaration `partial`:

```csharp
// Violation
public class Person
{
    public string? Name { get; set; }
}

string json = serializer.Serialize(person);
```

```csharp
// Fixed
[GenerateShape]
public partial class Person
{
    public string? Name { get; set; }
}

string json = serializer.Serialize(person);
```

When the type is not yours to edit — a BCL type, or a type from another package
— declare a witness class instead and pass it as the provider type argument:

```csharp
[GenerateShapeFor<Uri>]
internal partial class Witness;

string json = serializer.Serialize<Uri, Witness>(uri);
```

The same diagnostic is reported when a witness is supplied that provides a shape
for some other type.

## Code fix

The IDE offers to apply `[PolyType.GenerateShape]` and add the `partial`
modifier. The fix is offered only when the type has exactly one declaration in
the current solution, so there is never a question about which file to edit, and
it is never offered for types that come from referenced assemblies.

## When to suppress warnings

Do not suppress this diagnostic. The call site does not compile while the
violation remains.

## See also

- [Diagnostics](../diagnostics.md)
- [SHIFT007](SHIFT007.md)
