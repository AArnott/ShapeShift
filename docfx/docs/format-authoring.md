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
| Format-specific encoder/decoder methods | Optional | Native representations the shared interfaces do not expose. See [Primitives the shared interfaces do not expose](#primitives-the-shared-interfaces-do-not-expose). |
| Format-specific converters | Usually | Types the format represents natively, such as byte arrays. |
| `IValueBoundaryScanner` implementation | For async APIs | Recognizes one complete top-level value in a growing buffer. |
| `IReferencePreservingSerializer<TEncoder, TDecoder>` | Optional | An unambiguous back-reference token, if the format has one. |
| Conformance adapter | Strongly recommended | Runs the shared conformance kit over the format. |

Nothing else is needed. In particular, a format package never implements
object mapping, member naming, versioning policy, or `ShapeShiftValue`.

`IEncoder` and `IDecoder` are deliberately small, and a format is not limited to
them: a format's own encoder and decoder may declare whatever public members its
wire format deserves, and its own converters — which name the concrete encoder
and decoder types — can call them. That is how a native representation the
shared vocabulary cannot express gets used without every other format having to
grow a concept it does not have.

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

## `TryReadNull` consumes; `NextTokenType` is the peek

`IDecoder.TryReadNull` has the conventional `Try` semantics: it is `ReadNull`
without the throw.

- Next token is `Null`: **consume it** and return `true`.
- Next token is anything else: return `false` having **consumed nothing**.

So the common shape of a nullable read is one call, and a `true` answer must not
be followed by `ReadNull`:

```cs
if (decoder.TryReadNull())
{
    return null;   // the null is already consumed
}
```

`NextTokenType` is the peek — it never consumes, whatever the answer. A converter
that needs to know a null is coming *and still hand the token to somebody else*
asks that instead:

```cs
// This converter delegates the token rather than reading it, so it must not consume.
if (decoder.NextTokenType == TokenType.Null)
{
    return this.ReadNoneWithoutConsuming(ref decoder);
}

return inner.Read(ref decoder, context);
```

Two consequences worth pinning down in your own tests, because a decoder that
gets either wrong still passes the naive round-trip:

1. A `true` answer must run whatever per-value bookkeeping the format needs. A
   null consumed as the **last** element of a length-prefixed container is the
   case that catches a missed frame update: the synthesized `EndVector` will not
   appear if the count was not decremented.
2. A `false` answer must leave the decoder byte-for-byte where it was, including
   when the next token opens a container.

[!code-csharp[TryReadNull](../../samples/ubjson/UbjsonDecoder.cs#DecoderNull)]

`IDecoder.ReadNull` has a default implementation written in terms of
`TryReadNull`, so it is correct as inherited. Decoders still declare their own —
partly for a better error message, and partly because they have no choice: see
[Adding to the token vocabulary](#adding-to-the-token-vocabulary).

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

## Primitives the shared interfaces do not expose

The previous section is about types the format has *no* native representation
for. This one is the opposite case: the format has a **better** representation
than the shared vocabulary can name.

UBJSON is a real example. It has a `C` type — one marker byte plus one ASCII
byte — for a single character. `IEncoder` has no `char` member, so the shared
`char` converter writes a one-character string, which in UBJSON costs four bytes
instead of two and tells a reader less than the payload actually knows.

The answer is **not** to add `char` to `IEncoder`. It is three small pieces that
live entirely inside the format package.

### 1. A format-specific encoder method

Declare it on your concrete encoder. It is public API of your package, not of
ShapeShift:

[!code-csharp[NativeChar](../../samples/ubjson/UbjsonEncoder.cs#EncoderNativeChar)]

### 2. A format-specific decoder method

Give it the same `Try` shape as `TryReadNull`: consume on `true`, consume
nothing on `false`. Reporting `false` rather than throwing is what lets the
converter fall back to the shared representation, so a payload written by some
other implementation of your format still reads:

[!code-csharp[NativeChar](../../samples/ubjson/UbjsonDecoder.cs#DecoderNativeChar)]

### 3. A converter over the concrete encoder and decoder

`ShapeShiftConverter<T, TEncoder, TDecoder>` does not require `TEncoder` and
`TDecoder` to be type parameters. Name your own types and the converter can call
anything they declare — including members no interface knows about. Register it
in the serializer's constructor, where it takes precedence over the shared
layer's converter for the same type:

[!code-csharp[NativeCharConverter](../../samples/ubjson/UbjsonCharConverter.cs#NativeCharConverter)]

```cs
public UbjsonSerializer()
{
    this.Converters = [new UbjsonBinaryConverter(), new UbjsonCharConverter()];
}
```

Note that `GetContract` still has to be honest. A format-specific
representation is not an excuse to leave the schema projection guessing.

This pattern costs other formats nothing, needs no new token, and is
NativeAOT-safe: the converter is a non-generic class instantiated by the code
that registers it, so nothing is constructed reflectively on the serialization
path.

`ShapeShift.MsgPack` uses the same shape at a larger scale. Its reserved
extension codes for `decimal`, `Int128`, `TimeSpan`, and reference preservation
are reached through format-specific `MsgPackEncoder`/`MsgPackDecoder` members
such as `TryPeekExtensionHeader`, not through anything in `IEncoder`.

### When the format-specific representation is *all* there is

The `char` example has a lossless fallback in the shared vocabulary, because
`C` and a one-character string denote the same value. Not every case does. A
type that the shared layer has no converter for at all — `System.Guid` is the
obvious one — is simply a type your format package chooses to support, by
registering a converter for it the same way. Say so in your package README,
including the exact wire encoding, because a reader written against your format
by somebody else has no other way to find out.

## Adding to the token vocabulary

The question behind all of the above is: what happens when a primitive turns out
to be broadly useful and ShapeShift wants it in `IEncoder`/`IDecoder` so that
*every* format can offer it?

The honest answer is that it is a breaking change, and the usual escape hatch
does not work here.

**Adding a required (abstract) interface member breaks every format package,**
at compile time and at run time. That is uncontroversial and it is not going to
happen outside a major version.

**Adding a member with a default implementation does not soften it,** because
ShapeShift's encoders and decoders are `ref` structs. C# does not let a `ref`
struct inherit a default interface method:

```
error CS9245: 'IThing.ReadExtra()' cannot implement interface member
'IThing.ReadExtra()' for ref struct 'OldFormat'
```

and an already-compiled `ref` struct formatter fails at run time rather than
silently picking the default up, because the default implementation would have
to be dispatched on a boxed receiver:

```
System.InvalidProgramException: Cannot create boxed ByRef-like values.
```

Both of those are measured, not assumed. The same addition *is* source- and
binary-compatible for a formatter written as a `class`, but `ref` struct is the
conventional and recommended shape, so that is cold comfort.

For the same reason, a "capability interface" that shared, format-neutral code
tries to detect at run time does not work either: a
`where TEncoder : IEncoder, allows ref struct` type parameter cannot be tested
against or converted to another interface without boxing.

That leaves the policy ShapeShift actually follows:

1. **Format-specific converters are the supported extension point**, and they
   are not a stopgap. They are how a format claims a representation, today and
   after any future vocabulary change. The capability is expressed by
   *registration* — the serializer that knows about the type registers the
   converter — rather than by run-time type tests that `ref` structs cannot
   support.
2. **A new primitive that becomes broadly useful is added in a major version**,
   with the addition and the required edit for format authors listed in the
   release notes. `IEncoder`/`IDecoder` are deliberately small so that this is
   rare.
3. **Until then, the shared layer expresses the value in existing tokens.** A
   format that has something better registers a converter and wins locally; a
   format that does not keeps working unchanged. Both payloads remain readable
   by the other's reader whenever the native form has a lossless equivalent in
   the shared vocabulary — which is exactly why the decoder method above
   reports `false` instead of throwing.

The practical rule for a format author: if you want the native representation,
write the three pieces above. Do not wait for the interface to grow, and do not
petition for a token that only your format can produce.

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

A format package supplies converters only for types it represents natively —
whether because the shared layer cannot represent them at all, or because the
format can do better than the shared vocabulary, as
[the previous section](#primitives-the-shared-interfaces-do-not-expose)
describes. Each one is registered in the serializer's constructor, where it takes
precedence over the shared layer's converter for the same type:

```cs
public UbjsonSerializer()
{
    this.Converters = [new UbjsonBinaryConverter(), new UbjsonCharConverter()];
}
```

Override `ShapeShiftConverter<TEncoder, TDecoder>.GetContract` on every converter
you write. The default returns `null`, which makes `GetContract` describe the
type as an `UndocumentedContract` rather than guess at a representation the
converter does not actually produce — correct, but unhelpful to the JSON Schema
projection and to anyone reading the contract. The returned contract must
describe exactly what `WriteObject` emits. See
[Schema and contract inspection](schema.md).

When a converter builds an `ObjectContract` of its own, populate each
`PropertyContract.MemberName` from `PropertyContract.GetMemberName(propertyShape)`.
That is the CLR member name an expression-based path is matched against, so a
contract that omits it silently opts its type out of
[`GetPath`](features.md#targeted-and-streaming-deserialization).

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
above: token semantics, the consume-on-true `TryReadNull` contract, container
state, `Skip`, path traversal, every primitive width, binary and dynamic values,
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
- [ ] `TryReadNull` consumes the null when it answers `true` — including the
      per-value bookkeeping a synthesized end token depends on — and consumes
      nothing when it answers `false`.
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
- [ ] Any native representation the shared interfaces cannot name is reached
      through a format-specific encoder/decoder method and a converter over the
      concrete encoder and decoder — never by asking for a new shared token. Its
      decoder method reports `false` rather than throwing when the value arrived
      in the shared representation instead.
- [ ] Format-specific converters enforce the context limits and override
      `GetContract`.
- [ ] A boundary scanner exists if the package offers async APIs, and it never
      claims completeness early or reports `examined` past a value in progress.
- [ ] No reflection, no `InternalsVisibleTo`, `IsAotCompatible` enabled.
- [ ] The conformance suite passes, with every skip justified by a real
      limitation of the wire format.
