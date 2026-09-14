# ShapeShift analyzers

The ShapeShift package ships analyzers and code fixes under
`analyzers/dotnet/cs`, so referencing the package is all that is required to get
build-time feedback. The analyzers target C# only; no Visual Basic support is
claimed or tested.

The analyzers are strictly advisory. Runtime behavior is correct without them:
every condition an analyzer reports is also detected at run time and surfaced as
a <xref:ShapeShift.ShapeShiftSerializationException> with an actionable message.

Analyzers never run on generated code: each one calls
`ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None)`, because
PolyType's generated shapes satisfy these rules by construction and cannot be
edited by hand. All analyzers enable concurrent execution and resolve the
symbols they need once per compilation.

| ID | Title | Category | Severity |
| -- | ----- | -------- | -------- |
| [SHIFT001](SHIFT001.md) | Converter type is not a ShapeShift converter | ShapeShift.Usage | Error |
| [SHIFT002](SHIFT002.md) | Converter type cannot be activated | ShapeShift.Usage | Error |
| [SHIFT003](SHIFT003.md) | Converter type converts a different data type | ShapeShift.Usage | Error |
| [SHIFT004](SHIFT004.md) | Type has no generated shape | ShapeShift.Usage | Warning |
| [SHIFT005](SHIFT005.md) | Ambiguous serialized name | ShapeShift.Usage | Warning |
| [SHIFT006](SHIFT006.md) | Ambiguous serialized name under a naming policy | ShapeShift.Usage | Info |
| [SHIFT007](SHIFT007.md) | Reflection-based activation is not trimming or NativeAOT safe | ShapeShift.Reliability | Info |
| [SHIFT008](SHIFT008.md) | Unsupported ShapeShift contract | ShapeShift.Usage | Error |

Diagnostic IDs are permanent. A retired ID is never reused for a different
meaning, and each ID has a dedicated topic linked from the diagnostic's help
link so that the IDE and the build log both point at the same explanation.

## Configuring severity

Every diagnostic can be re-tuned per project or per folder with an
`.editorconfig` entry:

```ini
# Escalate the naming-policy collision advisory to a build warning.
dotnet_diagnostic.SHIFT006.severity = warning

# Silence the reflection opt-in advisory in a project that deliberately uses it.
dotnet_diagnostic.SHIFT007.severity = none
```

## Code fixes

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

Runtime serialization failures are covered separately under
[Diagnostics](../docs/diagnostics.md).
