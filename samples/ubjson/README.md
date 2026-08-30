# UBJSON: a third-party format package sample

A complete, working ShapeShift format package for
[UBJSON](https://ubjson.org/) (Universal Binary JSON) Draft 12, written the way a
third-party package outside this repository would be: using only public ShapeShift APIs.

It exists to be read alongside the
[format authoring guide](../../docfx/docs/format-authoring.md), which quotes from these files.

| File | What it demonstrates |
| --- | --- |
| `UbjsonMarkers.cs` | Keeping the wire alphabet in one place so the encoder and decoder cannot disagree. |
| `UbjsonEncoder.cs` | An `IEncoder`: a `ref struct` that projects the ShapeShift data model onto one wire format. |
| `UbjsonDecoder.cs` | An `IDecoder`: `NextTokenType`, the non-consuming `TryReadNull`, container frames, synthesized end tokens, allocation-free `Skip`, and bounds checks on every length. |
| `UbjsonSerializer.cs` | Binding `ShapeShiftSerializer<TEncoder, TDecoder>` to those types and offering natural buffer shapes. |
| `UbjsonBinaryConverter.cs` | A format-specific converter, including its `GetContract` schema hook and its `MaxBinaryLength` enforcement. |
| `UbjsonValueBoundaryScanner.cs` | An `IValueBoundaryScanner`, which is all a format needs in order to gain asynchronous stream and pipe APIs. |
| `UbjsonConformanceAdapter.cs` | Running `ShapeShift.Conformance`, declaring the format's limitations, and adding format-specific cases. |
| `UbjsonSamples.cs` | Using the finished package. |

## Deliberate design choices

* **Binary values use the conventional `[$U#n` optimized array**, which is UBJSON's
  established way to carry bytes. A `uint8`-typed array written by another producer is
  therefore read back as binary rather than as a vector of numbers, the same ambiguity JSON
  has with base64 strings.
* **The encoder writes only unoptimized containers.** The decoder reads both forms, so
  payloads from other UBJSON implementations are understood, but the format declares
  `ReportsContainerCounts = false` because its own output carries no counts.
* **`decimal`, wide integers, and `ulong` above `long.MaxValue` travel as high-precision
  (`H`) numbers**, which preserves every digit rather than silently narrowing.
* **Non-finite floats are written as IEEE binary32/binary64 rather than as `null`.** The
  UBJSON draft suggests encoding them as null; this package keeps the bits, because a `NaN`
  that becomes a `null` is silent data loss.
* **A container whose declared element type is itself a container (`$[`) is rejected**
  rather than half-supported.

## Running it

The conformance suite and the walkthroughs are executed by
`test/Ubjson.Sample.Tests`:

```pwsh
dotnet test --project test/Ubjson.Sample.Tests/Ubjson.Sample.Tests.csproj -c Release
```
