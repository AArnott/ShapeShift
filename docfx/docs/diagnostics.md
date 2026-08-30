# Diagnostics

ShapeShift reports problems in two complementary ways.

- **At run time**, every serialization failure carries a
  <xref:ShapeShift.ShapeShiftPath> breadcrumb that names the exact value
  that failed.
- **At build time**, a set of Roslyn analyzers moves the most common authoring
  mistakes forward from the first serialization attempt to the compiler.

The analyzers are strictly advisory. Runtime behavior is correct without them:
every condition an analyzer reports is also detected at run time and surfaced as
a <xref:ShapeShift.ShapeShiftSerializationException> with an actionable message.

## Exception paths

When a value deep inside an object graph cannot be written or read, the failure
is attributed to the precise location in the document. Each converter that is
responsible for an enclosing map property or vector element attaches its step to
the exception as the failure unwinds, so the outermost frame observes a complete
path from the root of the document.

[!code-csharp[ExceptionPaths](../../samples/cs/DiagnosingFailures.cs#ExceptionPaths)]

<xref:ShapeShift.ShapeShiftSerializationException.Path> is a
<xref:ShapeShift.ShapeShiftPath>, so it can be compared, rendered in
JSONPath-like notation, or handed straight back to
`TryDeserializeFragment` to re-read just the offending fragment.

Breadcrumbs are attached for:

- nested object properties, on both the serializing and deserializing side,
- vector elements, by index,
- string-keyed map entries, by property name,
- non-string-keyed dictionary entries, as `[entry][0]` for the key and
  `[entry][1]` for the value,
- rectangular array elements, as `[1][flattenedIndex]` within the
  dimensions/values envelope,
- union payloads, as `[1]` within the discriminator/value envelope,
- extension-data properties, by property name.

Exceptions thrown by your own code — including custom converters — are never
swallowed. A non-ShapeShift exception is wrapped in a
<xref:ShapeShift.ShapeShiftSerializationException> that preserves the original as
its `InnerException`; a ShapeShift exception is re-thrown as-is with its stack
trace intact and only its path extended. Cancellation propagates untouched.

Custom converters that iterate over sub-values can participate by calling
<xref:ShapeShift.ShapeShiftSerializationException.AddEnclosingPathElement(ShapeShift.ShapeShiftPathElement)>
from an exception filter:

[!code-csharp[CustomConverterBreadcrumbs](../../samples/cs/DiagnosingFailures.cs#CustomConverterBreadcrumbs)]

The method always returns `true`, which makes the filter fall through to the
`throw;` that rethrows the original exception without disturbing its stack trace.

## Analyzers

The ShapeShift package ships analyzers and code fixes under
`analyzers/dotnet/cs`, so referencing the package is all that is required to get
build-time feedback. The analyzers target C# only; no Visual Basic support is
claimed or tested.

Analyzers never run on generated code: each one calls
`ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None)`, because
PolyType's generated shapes satisfy these rules by construction and cannot be
edited by hand. All analyzers enable concurrent execution and resolve the
symbols they need once per compilation.

| ID | Title | Category | Severity |
| -- | ----- | -------- | -------- |
| [SHIFT001](analyzers/SHIFT001.md) | Converter type is not a ShapeShift converter | ShapeShift.Usage | Error |
| [SHIFT002](analyzers/SHIFT002.md) | Converter type cannot be activated | ShapeShift.Usage | Error |
| [SHIFT003](analyzers/SHIFT003.md) | Converter type converts a different data type | ShapeShift.Usage | Error |
| [SHIFT004](analyzers/SHIFT004.md) | Type has no generated shape | ShapeShift.Usage | Warning |
| [SHIFT005](analyzers/SHIFT005.md) | Ambiguous serialized name | ShapeShift.Usage | Warning |
| [SHIFT006](analyzers/SHIFT006.md) | Ambiguous serialized name under a naming policy | ShapeShift.Usage | Info |
| [SHIFT007](analyzers/SHIFT007.md) | Reflection-based activation is not trimming or NativeAOT safe | ShapeShift.Reliability | Info |
| [SHIFT008](analyzers/SHIFT008.md) | Unsupported ShapeShift contract | ShapeShift.Usage | Error |

Diagnostic IDs are permanent. A retired ID is never reused for a different
meaning, and each ID has a dedicated topic linked from the diagnostic's help
link so that the IDE and the build log both point at the same explanation.

### Configuring severity

Every diagnostic can be re-tuned per project or per folder with an
`.editorconfig` entry:

```ini
# Escalate the naming-policy collision advisory to a build warning.
dotnet_diagnostic.SHIFT006.severity = warning

# Silence the reflection opt-in advisory in a project that deliberately uses it.
dotnet_diagnostic.SHIFT007.severity = none
```

### Code fixes

ShapeShift only offers fixes that cannot change the serialized form of your data
or invent behavior:

- **SHIFT004** offers to apply `[PolyType.GenerateShape]` to the type and make
  the declaration `partial`. The fix is offered only when the type has exactly
  one declaration in the current solution.
- **SHIFT002** offers to widen an existing non-public parameterless converter
  constructor to `public`. When no parameterless constructor exists at all, no
  fix is offered, because only the author knows how the converter should be
  constructed.

The remaining diagnostics have no automatic fix. Renaming a member, changing the
type a converter converts, or removing an extension-data member all alter the
wire format or the public API, so those choices stay with you.
