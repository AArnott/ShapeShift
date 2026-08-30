# Authoring a format package

ShapeShift's core is format-neutral. Objects, collections, unions, surrogates,
naming policies, default-value omission, required-member validation, security
limits, reference tracking, path-based targeted reads, and schema generation are
implemented once, over an abstract token stream. A format package supplies only
the projection of that token stream onto one wire format.

That makes a new format a small, well-bounded piece of work — and it makes the
contract between the two halves worth stating precisely, because everything
above depends on it.

The smallest first-party implementation is `ShapeShift.Taml`. The
`samples/ubjson` directory in this repository is a complete third-party-style
package for [UBJSON](https://ubjson.org/) written against public APIs only; it
is quoted throughout this guide and is verified by `ShapeShift.Conformance` on
every build.

## What a format package contains

| Type | Required | Purpose |
| --- | --- | --- |
| `IEncoder` implementation | Yes | Writes the data model to the wire. |
| `IDecoder` implementation | Yes | Reads the data model from the wire. |
| `ShapeShiftSerializer<TEncoder, TDecoder>` subclass | Yes | Binds the two together and offers buffer shapes natural to the format. |
| Format-specific converters | Usually | Types the format represents natively, such as byte arrays. |
| `IValueBoundaryScanner` implementation | For async APIs | Recognizes one complete top-level value in a growing buffer. |
| `IReferencePreservingSerializer<TEncoder, TDecoder>` | Optional | An unambiguous back-reference token, if the format has one. |
| Conformance adapter | Strongly recommended | Runs the shared conformance kit over the format. |

Nothing else is needed. In particular, a format package never implements
object mapping, member naming, versioning policy, or `ShapeShiftValue`.

## The data model

The abstract document is:

- a **map**: a sequence of property name/value pairs;
- a **vector**: an ordered sequence of values;
- **scalars**: null, Boolean, number, string, and binary.

Objects are written as maps of named properties in every format, because that is
the version-tolerant choice. A format whose wire model rewards compactness may
offer an explicit positional mode as well; see
[MessagePack positional contracts](msgpack.md#positional-array-contracts).

Rectangular arrays, dictionaries with non-string keys, unions, and surrogates
are all expressed by the shared layer in terms of the above. A format never sees
them as such.

## Token semantics

`TokenType` is the whole vocabulary a converter dispatches on:

| Token | Meaning |
| --- | --- |
| `StartMap` / `EndMap` | Map boundaries. |
| `StartVector` / `EndVector` | Vector boundaries. |
| `PropertyName` | The key of the map entry whose value comes next. |
| `Null`, `Boolean`, `Number`, `String`, `Binary` | Scalars. |
| `EndDocument` | The input is exhausted. |

Three rules govern them.

**`NextTokenType` is a peek.** It reports what the next `Read` call would
consume, without consuming anything, and returns the same answer however many
times it is asked. It reports `EndDocument` rather than throwing once the input
is spent, so a loop may always ask what comes next — which is what reading a
stream of concatenated top-level values requires.

**Report the token you can honestly produce.** A format is not obliged to
distinguish every member. JSON has no binary family, so `ShapeShift.Json`
reports base64 blobs as `String`; TAML's scalars are untyped text, so it reports
Booleans as `String`. What matters is that the answer matches what the
subsequent read will actually accept. The conformance adapter's
`GetExpectedTokenType` is where a format declares those mappings, and the token
suite then holds it to them.

**A token type is not a type assertion about the value's width.** `Number`
covers every integer and floating-point width. Which `Read` method a converter
calls is decided by the CLR type being deserialized, not by the token, so
`ReadInt64` must accept any integer representation the encoder might have chosen
for a `long`, including a narrower one.

[!code-csharp[NextTokenType](../../samples/ubjson/UbjsonDecoder.cs#DecoderNextTokenType)]

## `TryReadNull` does not consume

`IDecoder.TryReadNull` is a **peek**, whatever it answers. `IDecoder.ReadNull`
is the consuming counterpart, and every decoder must override it, because the
default interface implementation only validates.

This trips up nearly every first implementation, because the most common calling
pattern hides the bug:

```cs
if (decoder.TryReadNull())
{
    decoder.ReadNull();   // an implementation that consumed in TryReadNull
    return null;          // still looks correct here
}
```

The pattern that breaks is the one that peeks and then *delegates*, which is
what optional values, unions, reference preservation, and every nullable
wrapper do:

```cs
// The inner converter must still see the null token if there is one.
return decoder.TryReadNull() ? this.ReadNone(ref decoder) : inner.Read(ref decoder, context);
```

[!code-csharp[TryReadNull](../../samples/ubjson/UbjsonDecoder.cs#DecoderNull)]

## Decoder end-state invariants

Every `Read` method consumes **exactly one** token — or, for a container, exactly
one start or end token — and leaves the decoder positioned on the next one. That
single rule is what lets converters compose without knowing anything about each
other.

Concretely:

1. `ReadStartMap` consumes only the start token. The entries are read by the
   caller.
2. `ReadEndMap` consumes only the end token. It must fail if entries remain
   unread, rather than skipping them, so that a converter bug surfaces as an
   error instead of as silent data loss.
3. Between `ReadPropertyName` and the value that follows, the decoder is
   mid-entry. `ReadEndMap` there is an error.
4. After the top-level value has been consumed, `NextTokenType` reports
   `EndDocument` — or the start of the next top-level value, if the input holds
   several.
5. A container whose length is declared on the wire has **no end token to
   consume**. Synthesize one: report `EndMap`/`EndVector` from `NextTokenType`
   once the declared count is exhausted, and let `ReadEndMap`/`ReadEndVector`
   pop the frame without reading a byte. Callers must never need to know which
   framing the payload used.
6. A failed read may leave the decoder unusable. That is allowed — but it must
   fail by throwing `DecoderException`, never by silently mispositioning.

The bookkeeping that makes 4 and 5 work belongs in one place, called at the end
of every read path:

[!code-csharp[ValueRead](../../samples/ubjson/UbjsonDecoder.cs#DecoderValueRead)]

`ReadStartMap` and `ReadStartVector` return `int?`. Returning `null` is always
correct. Returning a count is an optimization for length-prefixed formats and,
once offered, must be exact — a count that lies causes collections to be
pre-sized wrongly at best and truncated at worst. Declare which answer the
format gives with `FormatConformanceOptions.ReportsContainerCounts`.

## `Skip` and `TrySeek`

`IDecoder.Skip` consumes the next value in its entirety, however deeply nested,
without converting it. Unknown-property retention, positional contracts, and
path traversal all build on it.

Implement it in terms of declared widths rather than by decoding: skipping a
16 MB string should cost a pointer addition, not a UTF-8 transcode.

[!code-csharp[Skip](../../samples/ubjson/UbjsonDecoder.cs#DecoderSkip)]

`TrySeek` — a `ref`-receiver extension member on `IDecoder`, and the engine
behind `TryDeserializeFragment`, `DeserializeFragment`, and
`ShapeShiftSequenceReader<T>` — is implemented **once, format-neutrally**, on
top of `Skip`, `ReadPropertyName`, and `NextTokenType`. A format therefore gets
targeted reads for free, and its conformance path cases are really assertions
that those three primitives compose. Its documented end states are worth
knowing, because a decoder must make them reachable:

- On success, the decoder sits at the start of the sought value; unread siblings
  and ancestor closing tokens are left unconsumed.
- On failure, every container opened while searching has been fully consumed,
  including its closing token.

Skipping walks attacker-controlled structure, so bound the nesting a skip may
recurse through rather than relying on
`SerializationContext<TEncoder, TDecoder>.MaxDepth`, which applies only while
converters are running.

## Errors and paths

Two exception types divide the work:

- **`DecoderException`** — "these bytes are not valid". Thrown by the decoder.
- **`ShapeShiftSerializationException`** — "this structurally valid document does
  not match the shape being deserialized". Thrown by the converter layer, and it
  carries a `ShapeShiftPath` breadcrumb naming the exact value that failed.

A decoder reads attacker-controlled bytes, so **every** rejection must arrive as
one of those two. An `IndexOutOfRangeException`, `ArgumentOutOfRangeException`,
`NullReferenceException`, `FormatException`, `OverflowException`, or an
unbounded allocation escaping a decoder is a missing bounds check, not merely an
inconvenient exception type. Where a lower-level reader throws something else,
translate it and keep the original as `InnerException` so its position
information survives.

Path breadcrumbs are attached by the shared converter layer as an exception
propagates out through each nested member, so a format gets them automatically —
provided its decoder lets the failure reach the converter layer. A format whose
decoder discovers structural problems earlier (a text format inferring where
containers begin, say) reports a bare `DecoderException` instead, and declares
that with `FormatConformanceOptions.ReportsErrorPaths`.

Error messages should name the offending byte offset or marker. They are read by
people debugging payloads they cannot see.

## Security limits

`SerializationContext<TEncoder, TDecoder>` carries `MaxDepth`,
`MaxCollectionLength`, `MaxStringLength`, and `MaxBinaryLength`. The shared
converters enforce them, so a format normally gets them for free.

Two things a format must still do:

**Validate every length before using it.** A length or count read from the input
is attacker-controlled. Check it against the bytes actually available before
slicing, allocating, or looping — never trust a header. The decoder has no
access to the serialization context, so this is a bounds check against the
input, not a policy check.

**Enforce the policy limits in any converter you supply.** A format-specific
converter that reads a length-bearing value bypasses the shared code that would
have checked it:

[!code-csharp[BinaryConverter](../../samples/ubjson/UbjsonBinaryConverter.cs#BinaryConverter)]

Bound the decoder's own recursion and container nesting as well. `MaxDepth`
protects converters; it does not protect a `Skip` that walks a hostile document.

## Optimized scalar and binary hooks

Three members of the decoder have default implementations that are correct but
lossy or slow. Override them when the format can do better:

| Member | Default | Why override |
| --- | --- | --- |
| `ReadByteArray` | Throws `NotSupportedException` | Formats with a binary family. Pair it with `IEncoder.WriteValue(ReadOnlySpan<byte>)`. |
| `ReadDynamicNumber` | Narrows everything to `decimal` | Preserves the width and signedness the payload actually used, which is what `ShapeShiftValue` and unknown-property retention need in order to write a value back the way it arrived. |
| `ReadCharSpan` | — | The allocation-free string path; converters prefer it over `ReadString`. |

On the encoder, implement `WriteValue(ReadOnlySpan<char>)` directly rather than
forwarding to the `string` overload, and choose the narrowest representation that
round-trips: an integer written narrow must widen back losslessly.

Types with no native representation — `decimal`, `Int128`, `UInt128`,
`BigInteger`, `TimeSpan`, and often `DateTime` — need a documented, interoperable
encoding rather than a lossy narrowing. Text is a perfectly respectable choice:

[!code-csharp[Integers](../../samples/ubjson/UbjsonEncoder.cs#EncoderIntegers)]

Note also that map keys are frequently encoded differently from string values,
which is why `WritePropertyName` is a distinct operation:

[!code-csharp[PropertyName](../../samples/ubjson/UbjsonEncoder.cs#EncoderPropertyName)]

## Asynchronous adapters

Decoders are `ref` structs. They cannot live across an `await`, so a partially
decoded value cannot be paused and resumed. ShapeShift therefore does not stream
*into* a decoder; it buffers input until one complete top-level value is present
and then runs the ordinary synchronous decode exactly once over those bytes.

The only piece a format supplies is an `IValueBoundaryScanner`, which walks the
framing without converting anything:

[!code-csharp[TryScan](../../samples/ubjson/UbjsonValueBoundaryScanner.cs#ScannerTryScan)]

The scanner contract has three sharp edges:

1. **Incomplete input is not an error.** Return `false` so the caller can supply
   more bytes. Throw `DecoderException` only for input that is *provably*
   malformed, never for input that is merely short.
2. **`examined` reports what will never be re-inspected.** On success it equals
   `end`. On failure it may only advance past bytes that are provably not part of
   a value that has already begun — separator whitespace, for instance — because
   the eventual decode step still needs every byte of the value itself. A
   stateless scanner that re-walks the buffer each call simply reports the
   buffer's start, which is always safe.
3. **One instance is reused across values.** After returning `true`, the
   instance must be ready to scan the next top-level value starting from a fresh
   buffer.

Given a scanner, `PipeReaderExtensions.ReadValueAsync` supplies the whole
buffering loop, including the `maxBufferedSize` guard against a value that never
completes:

[!code-csharp[AsyncAdapter](../../samples/ubjson/UbjsonSerializer.cs#AsyncAdapter)]

`PipeWriterExtensions.FlushAndThrowIfCanceledAsync` is the corresponding write
side. Wrap `Stream` overloads around the `PipeReader`/`PipeWriter` ones with
`leaveOpen: true`, as `ShapeShift.Json` and `ShapeShift.MsgPack` do.

The synchronous streaming APIs — `ShapeShiftSequenceReader<T>` and
`ShapeShiftDocumentReader<T>` — need nothing from a format beyond the
`EndDocument` reporting described above.

## Converters, contracts, and schema

A format package supplies converters only for types it represents natively. Each
one is registered in the serializer's constructor:

```cs
public UbjsonSerializer()
{
    this.Converters = [new UbjsonBinaryConverter()];
}
```

Override `ShapeShiftConverter<TEncoder, TDecoder>.GetContract` on every converter
you write. The default returns `null`, which makes `GetContract` describe the
type as an `UndocumentedContract` rather than guess at a representation the
converter does not actually produce — correct, but unhelpful to the JSON Schema
projection and to anyone reading the contract. The returned contract must
describe exactly what `WriteObject` emits. See
[Schema and contract inspection](schema.md).

Reference preservation is the one shared feature a format must opt into
explicitly, because there is no format-neutral way to say "this is a reference"
without colliding with data that happens to look the same. A serializer that
implements `IReferencePreservingSerializer<TEncoder, TDecoder>` defines its own
unambiguous token — `ShapeShift.MsgPack` uses a reserved extension type — and a
serializer that does not implement it rejects any attempt to enable the feature
rather than silently writing a graph as a tree.

## NativeAOT and trimming

Every shipping library and default code path must be trimming-safe and
NativeAOT-ready. For a format package that means:

- No reflection on the serialization path. Shapes come from PolyType source
  generation; converters are instantiated by the code that registers them.
- No `MakeGenericType`, `Activator.CreateInstance(Type)`, or type lookup from
  payload metadata. Loading CLR types named by untrusted input is both an AOT
  hazard and a security one.
- Any feature that genuinely cannot be AOT-safe is **off by default** and
  enabled only by an explicit method call, so that an application which never
  calls it stays AOT-safe. `WithReflectionConverterTypes` is the pattern.
- Annotate the project with `<IsAotCompatible>true</IsAotCompatible>` so the
  analyzers run at build time rather than at publish time.

## Running the conformance kit

`ShapeShift.Conformance` verifies an encoder/decoder pair against everything
above: token semantics, the non-consuming `TryReadNull`, container state,
`Skip`, path traversal, every primitive width, binary and dynamic values,
malformed and truncated input (including a byte-flipping fuzz pass), the
security limits, converter and policy interactions, and the boundary scanner.

It has no test-framework dependency: a suite is a list of named, runnable cases
that drops into TUnit, xUnit, NUnit, MSTest, or a console app.

Write one adapter:

[!code-csharp[AdapterHarness](../../samples/ubjson/UbjsonConformanceAdapter.cs#AdapterHarness)]

Declare what the format genuinely cannot do. Every option defaults to the
strictest, fully self-describing behavior; relaxing one is a documented
limitation, and the affected cases then report themselves as *skipped* rather
than failing:

[!code-csharp[AdapterOptions](../../samples/ubjson/UbjsonConformanceAdapter.cs#AdapterOptions)]

Add cases that only make sense for your format through the same collector the
built-in suites use, so they are reported and filtered identically:

[!code-csharp[FormatSpecific](../../samples/ubjson/UbjsonConformanceAdapter.cs#AdapterFormatSpecific)]

Then feed the cases to a test framework, one test per case:

[!code-csharp[Conformance](../../samples/ubjson/UbjsonSamples.cs#Conformance)]

Relaxing an option to make a failure go away is the one way to misuse the kit.
Each one describes something the wire format cannot express; if the format
*could* express it, the failure is a bug.

## Packaging

- One package per format, named `<Product>.<Format>`, depending only on
  `ShapeShift`. Keep format-specific wire choices out of shared abstractions.
- Reference `ShapeShift.Conformance` from the test project only.
- Do not use `InternalsVisibleTo`. Test through public APIs; if a test needs a
  helper, the helper belongs in a public test-support package.
- Ship a `README.md` in the package, and XML documentation for every public
  member. A format's public surface is read by people who cannot see the wire.
- Document the encoding chosen for every type the format has no native
  representation for, along with any interoperability caveats for readers that
  are not yours.
- Keep configuration immutable and instance-scoped. No mutable global defaults.
- Avoid binary breaking changes in the public API, and enable package validation
  against a baseline once the package has shipped.

## Checklist

Before publishing:

- [ ] `NextTokenType` peeks, never consumes, and reports `EndDocument` at the end
      of the input.
- [ ] `TryReadNull` peeks; `ReadNull` is overridden and consumes.
- [ ] Every read consumes exactly one token and leaves the decoder on the next.
- [ ] Length-prefixed containers synthesize their end tokens.
- [ ] A container count, if reported, is always exact.
- [ ] `Skip` handles every value shape without converting, and bounds its
      nesting.
- [ ] Every length read from the input is validated against the bytes available.
- [ ] Every rejection is a `DecoderException` or a
      `ShapeShiftSerializationException`; nothing else escapes the decoder.
- [ ] Numeric narrowing on write always widens back losslessly on read.
- [ ] Types with no native representation have a documented encoding.
- [ ] `ReadDynamicNumber` preserves the payload's width; `ReadCharSpan` avoids an
      allocation; binary hooks are implemented if the format has a binary family.
- [ ] Format-specific converters enforce the context limits and override
      `GetContract`.
- [ ] A boundary scanner exists if the package offers async APIs, and it never
      claims completeness early or reports `examined` past a value in progress.
- [ ] No reflection, no `InternalsVisibleTo`, `IsAotCompatible` enabled.
- [ ] The conformance suite passes, with every skip justified by a real
      limitation of the wire format.
