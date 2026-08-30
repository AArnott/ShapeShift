# Features

## NativeAOT-ready contracts

ShapeShift uses PolyType source-generated type shapes for NativeAOT-ready object
mapping. Reflection-based converter type activation is available only through
the explicitly annotated `WithReflectionConverterTypes` opt-in. Converter
instances, factories, and generated shapes remain reflection-free.

## Shared format-neutral behavior

Every ShapeShift format shares support for:

- Objects with mutable or parameterized construction.
- Mutable and immutable collections.
- Rectangular arrays of any rank.
- String-keyed maps and non-string-keyed dictionary entries.
- Nullable and other optional values.
- Enum names and numeric values.
- PolyType surrogates and attributed unions.
- Custom converters and converter factories.
- Naming policies, callbacks, reference preservation, and string interning.
- Default-value omission.
- Strict duplicate, required-member, and non-nullable-member validation.
- Configurable depth, collection, string, and binary length limits.

Objects are written as maps of named properties in every format, which is the
version-tolerant choice. A format whose wire model rewards compactness may also
offer an explicit positional mode with stricter versioning rules; see
[MessagePack positional contracts](msgpack.md#positional-array-contracts).

All of it is configured the same way in every format: immutably, on the
serializer instance. See [Customizing the core](customization.md) for the naming,
default-value, strictness, and security policies, and for custom converters and
converter factories.

## Reference preservation

`PreserveReferences` makes an object graph that shares objects stay a graph: an
object is written once and referred back to afterwards, and the reader
reconstructs the sharing. `RejectCycles` preserves identity while refusing
cycles; `AllowCycles` also reconstructs graphs that refer back to themselves.

Each format chooses how a reference is represented, because there is no
format-neutral answer: see
[MessagePack reference preservation](msgpack.md#reference-preservation) for the
reserved extension MessagePack uses, its size, and the errors a mismatched
reader reports. Because references are a runtime protocol rather than a static
shape, `GetContract` is not available while preservation is enabled.

## Targeted and streaming deserialization

Every ShapeShift decoder supports skipping and seeking without deserializing
intervening content, and reading a sequence of top-level values (or the
elements of a nested vector) one at a time.

`ShapeShiftPath` identifies a location within a document as a sequence of
property names and vector indices, independent of any particular format:

[!code-csharp[TargetedDeserialization](../../samples/cs/TargetedDeserialization.cs#TargetedDeserialization)]

`TrySeek` (a `ref`-receiver extension member on `IDecoder`) advances a decoder
to the value at a path, skipping everything else along the way without
allocating or converting it, and returns `false` (leaving the decoder
unusable for further reads of the current value) if the path does not exist
in the document. `TryDeserializeFragment`/`DeserializeFragment` combine
`TrySeek` with an ordinary typed deserialize of whatever is found there.

`ShapeShiftSequenceReader<T>` and `ShapeShiftDocumentReader<T>` enumerate
multiple values sharing one decoder without loading them all into memory at
once:

- A sequence reader enumerates the elements of a vector — the root of a
  document, or one reached first by `TrySeek` — the same way a JSON array or
  MessagePack array's elements would otherwise all be deserialized together
  into a single collection.
- A document reader enumerates whole top-level values, one after another,
  until the decoder reaches the end of its input. This supports
  newline-delimited JSON (NDJSON) and any other stream of concatenated
  top-level values.

[!code-csharp[StreamingDeserialization](../../samples/cs/StreamingDeserialization.cs#StreamingDeserialization)]

Both reader types are plain (non-`ref`) structs that do not themselves store
the decoder, so a `foreach`-like loop passes the same decoder by `ref` to
each call to `MoveNext`. This keeps them usable across `await` boundaries even
though the decoders they read from are typically `ref struct` types that
cannot themselves cross an `await`. Dispose a reader (or use a `using`
statement) when finished with it to release any pooled resources it may hold.

A decoder that reads a segmented `ReadOnlySequence<byte>` should walk it in
place rather than consolidating it, so that a targeted read of a small fragment
costs a small fragment's worth of work no matter how large the surrounding
document is or how it happens to be chopped into segments.
`ShapeShift.MsgPack` does exactly that; see
[Segmented buffers and no-copy reads](msgpack.md#segmented-buffers-and-no-copy-reads).

See [JSON](json.md#targeted-and-streaming-deserialization) and
[MessagePack](msgpack.md#targeted-and-streaming-deserialization) for
format-specific notes.

## Dynamic values

`ShapeShiftValue` is a NativeAOT-safe, format-neutral value tree. Its concrete
types represent null, Boolean, signed and unsigned integers, arbitrary-precision
integers, floating-point and decimal numbers, strings, binary data, arrays, and
string-keyed maps.

Dynamic values do not load CLR types from payload metadata. This makes them
suitable for inspecting untyped input without introducing typeless
deserialization risks.

## Unknown-property retention

Apply `ShapeShiftExtensionDataAttribute` to one
`Dictionary<string, ShapeShiftValue>` member to capture properties that are not
declared by the generated contract and write them back as peer properties:

[!code-csharp[UnknownDataRetention](../../samples/cs/UnknownDataRetention.cs#UnknownDataRetention)]

The extension-data member is excluded from the ordinary object contract, so its
dictionary is flattened rather than nested under the CLR member name. Extension
keys that collide with declared wire property names are rejected. A type may
declare only one extension-data member. The member must have a getter; when it
returns `null`, it must also have a setter so ShapeShift can assign a dictionary.

Extension-data deserialization currently requires a parameterless constructor.
This avoids retaining untrusted data in constructor argument state and keeps
construction deterministic. Maps are string-keyed because ShapeShift object
contracts expose property names as strings.

Formats may not be able to preserve every distinction. For example, JSON has no
native binary token, so a `ShapeShiftBinary` writes as base64 text but untyped
JSON text is read as `ShapeShiftString`.

See [JSON](json.md) for JSON APIs and representation details.

Rectangular arrays use a two-element envelope containing a dimensions vector
and a row-major flat values vector. For example, a `2 x 3` array is represented
as `[[2, 3], [v0, v1, v2, v3, v4, v5]]`. This preserves zero-length dimensions
that a naively nested representation would lose.

## Schema and contract inspection

`GetContract` produces a format-neutral description of what a serializer would
write, and `ShapeShift.Json` projects that description to JSON Schema 2020-12,
optionally with MessagePack annotations. See
[Schema and contract inspection](schema.md).

## Structural equality and hashing

`StructuralEqualityComparer.Create<T>()` builds a deep `IEqualityComparer<T>`
from the same source-generated shapes the serializer uses, covering objects,
collections, dictionaries, unions, surrogates, `ShapeShiftValue` and cyclic
graphs. Collision-resistant hashing is available as an opt-in policy. See
[Structural equality and hashing](equality.md).

## Diagnostics and analyzers

Every serialization failure carries a `ShapeShiftPath` breadcrumb that names the
exact value that failed, and the ShapeShift package ships Roslyn analyzers that
move common authoring mistakes forward to build time. Runtime behavior is
correct without the analyzers. See [Diagnostics](diagnostics.md).

## Third-party formats

The core is format-neutral, so a new format package supplies only an `IEncoder`,
an `IDecoder`, and a serializer that binds them together; objects, collections,
policies, limits, targeted reads, and schema generation come from the shared
layer. `ShapeShift.Conformance` verifies that a new pair honors the contracts
that layer relies on. See
[Authoring a format package](format-authoring.md).
