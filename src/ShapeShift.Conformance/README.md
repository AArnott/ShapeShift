# ShapeShift.Conformance

A conformance kit for authors of third-party ShapeShift format packages.

## Features

* Verifies an `IEncoder`/`IDecoder` pair against the contracts that ShapeShift's
  format-agnostic converter layer relies on: token semantics, the non-consuming
  `TryReadNull` invariant, container/end-token state rules, `Skip` and
  `ShapeShiftPath` traversal, every primitive width, binary and dynamic values,
  malformed and truncated input, security limits, and converter interactions.
* No test framework dependency. The suite is a list of named, runnable test
  cases, so it drops into TUnit, xUnit, NUnit, MSTest, or a console app.
* Capability-driven: a format declares what it can represent and inapplicable
  cases report themselves as skipped instead of failing.
* Extensible: add your own format-specific cases through the same collector the
  built-in suites use.

## Usage

```cs
public sealed class MyFormatConformanceAdapter
    : FormatConformanceAdapter<MyEncoder, MyDecoder>
{
    public override string FormatName => "MyFormat";

    public override ShapeShiftSerializer<MyEncoder, MyDecoder> CreateSerializer() => new MySerializer();

    public override byte[] Encode(EncodeAction<MyEncoder> action)
    {
        ArrayBufferWriter<byte> buffer = new();
        MyEncoder encoder = new(buffer);
        action(ref encoder);
        encoder.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    public override TResult Decode<TResult>(ReadOnlyMemory<byte> payload, DecodeFunc<MyDecoder, TResult> func)
    {
        MyDecoder decoder = new(payload.Span);
        return func(ref decoder);
    }
}
```

```cs
[Test]
[MethodDataSource(nameof(Cases))]
public void Conformance(ConformanceTestCase testCase) => testCase.Run();

public static IEnumerable<Func<ConformanceTestCase>> Cases() =>
    ConformanceSuite.CreateTestCases(new MyFormatConformanceAdapter())
        .Select(c => (Func<ConformanceTestCase>)(() => c));
```

See the format authoring guide (`docfx/docs/format-authoring.md`) for the full
contract that these tests enforce, and `samples/ubjson` for a complete
third-party format package that runs this kit.
