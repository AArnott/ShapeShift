# Diagnostics

ShapeShift reports problems in two complementary ways.

- **At run time**, every serialization failure carries a
  <xref:ShapeShift.ShapeShiftPath> breadcrumb that names the exact value
  that failed.
- **At build time**, the [ShapeShift analyzers](../analyzers/index.md) move the
  most common authoring mistakes forward from the first serialization attempt
  to the compiler.

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
