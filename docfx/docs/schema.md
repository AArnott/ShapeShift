# Schema and contract inspection

ShapeShift can describe *what it would write* without writing anything. Two
layers are available:

1. A **format-neutral contract model** (`ShapeShift.Schema`) produced from
   PolyType shapes and the serializer's own converter selection and policies.
2. A **JSON Schema projection** (`ShapeShift.Json`) that renders any contract as
   a standards-based JSON Schema 2020-12 document, optionally annotated with
   MessagePack-specific keywords.

Both layers are NativeAOT-safe and require no reflection: the contract is built
by the same shape visitor machinery that builds converters.

## The contract model

`ShapeShiftSerializer<TEncoder, TDecoder>.GetContract` returns a `DataContract`
describing the wire shape of a type *for that serializer instance*, honoring the
naming policy, default-value policies, enum and surrogate settings, and any
custom converters that have been registered.

[!code-csharp[Contract](../../samples/cs/SchemaGeneration.cs#Contract)]

`DataContract.Kind` discriminates the node type:

| `DataContractKind` | Concrete type | Wire shape |
| --- | --- | --- |
| `Primitive` | `PrimitiveContract` | A single scalar token, identified by `PrimitiveDataType`. |
| `Object` | `ObjectContract` | A string-keyed map of declared properties. |
| `Sequence` | `SequenceContract` | A vector of elements; `IsSet` marks set semantics. |
| `RectangularArray` | `RectangularArrayContract` | A two-element `[dimensions, row-major values]` envelope. |
| `Map` | `MapContract` | A string-keyed map, or a vector of `[key, value]` pairs. |
| `Enum` | `EnumContract` | A name string or the underlying numeric value. |
| `Optional` | `OptionalContract` | The underlying value, or null. |
| `Union` | `UnionContract` | A two-element `[discriminator, value]` array. |
| `Surrogate` | `SurrogateContract` | The surrogate type's contract. |
| `Dynamic` | `DynamicContract` | Any value (`ShapeShiftValue` and format DOM types). |
| `Undocumented` | `UndocumentedContract` | A custom converter that did not describe itself. |

Contracts are immutable reference types (not records) because the graph may be
cyclic: a recursive type's contract references itself. Each type is described
exactly once per serializer instance, so reference equality is meaningful and
can be used to detect recursion and shared definitions. `ReferencedContracts`
enumerates a node's immediate children for graph walks.

Contract generation is not supported when `PreserveReferences` is enabled: the
reference-tracking envelope is a runtime protocol rather than a static shape, so
`GetContract` throws `NotSupportedException` instead of publishing a schema that
payloads would not satisfy.

## Nullability, requiredness, and default-value policies

`PropertyContract` reports the serializer's actual behavior rather than the CLR
declaration alone:

- `Name` is the wire name after the naming policy; `DeclaredName` is the
  shape-declared name (which honors `PropertyShapeAttribute.Name`).
- `IsRequired` is `true` only when the deserializer will fault if the property is
  missing, so it accounts for
  `DeserializeDefaultValuesPolicy.AllowMissingValuesForRequiredProperties`.
- `IsNullable` is `false` only when the deserializer will reject an explicit
  null, so it accounts for
  `DeserializeDefaultValuesPolicy.AllowNullValuesForNonNullableProperties` and
  the nullability of both the getter and the setter or constructor parameter.
- `IsAlwaysWritten` is `false` when `SerializeDefaultValuesPolicy` may omit the
  property. `DefaultValue` carries the value that would be omitted when the
  policy makes it available.

## Custom converters

Custom converters may override `GetContract` to describe themselves. The
`ContractContext<TEncoder, TDecoder>` argument exposes the shape being described
(when there is one) and can build contracts for nested types, so a converter
that wraps a value can describe the wrapper precisely.

[!code-csharp[CustomConverterContract](../../samples/cs/SchemaGeneration.cs#CustomConverterContract)]

A converter that does *not* override `GetContract` yields an
`UndocumentedContract` naming the converter type. This is deliberate: an
undocumented contract is honest, whereas inferring the shape from the converter's
CLR type would routinely be wrong, and a wrong schema is worse than an absent
one.

## JSON Schema projection

`JsonSerializer.GetJsonSchema` projects the contract to a
[JSON Schema 2020-12](https://json-schema.org/draft/2020-12/release-notes)
document as a `System.Text.Json.Nodes.JsonObject`.

[!code-csharp[JsonSchema](../../samples/cs/SchemaGeneration.cs#JsonSchema)]

`JsonSchema.Create` accepts any `DataContract`, so a contract obtained from a
different serializer (for example, `MsgPackSerializer`) can be projected too.

Recursive contracts, and contracts referenced more than once, are emitted into
`$defs` and referenced with `$ref`. Definition names are derived from the CLR
type name and de-duplicated.

### JSON-specific representation choices

The projection documents the choices that `ShapeShift.Json` actually makes:

| Value | JSON Schema |
| --- | --- |
| `DateTime` | `{"type":"string","format":"date-time"}` (ISO 8601) |
| `DateTimeOffset` | `[date-time, offsetMinutes]`, with the offset constrained to ±840 |
| `TimeSpan` | `{"type":"string"}` with a `$comment`; the invariant `c` format is not an ISO 8601 duration, so no `format` keyword is claimed |
| `byte[]` / binary | `{"type":"string","contentEncoding":"base64"}` |
| `Int128`, `UInt128`, `BigInteger` | `{"type":"integer"}`; the fixed-width forms carry `minimum`/`maximum` bounds |
| `decimal` | `{"type":"number"}` with a `$comment`; JSON keeps the full precision as a number literal, though an IEEE 754 consumer may not |
| `char` | `{"type":"string","minLength":1,"maxLength":1}` |
| `Rune` | `{"type":"integer"}` (a Unicode scalar value) |
| Fixed-width integers | `{"type":"integer"}` with `minimum`/`maximum` bounds |
| `float`, `double`, `Half` | `{"type":"number"}`, widened to `anyOf` with `"NaN"`/`"Infinity"`/`"-Infinity"` only when `AllowNamedFloatingPointValues` is set |
| String-keyed dictionaries | `{"type":"object","additionalProperties":<value>}` |
| Other dictionaries | An array of two-element `[key, value]` arrays |
| Rectangular arrays | A two-element `[dimensions, row-major values]` array |
| Enums | `anyOf` of the name form and the numeric form, with the serialized form first |
| Unions | `oneOf` over `[discriminator, value]` tuples; the base case uses a `null` discriminator |
| Dynamic values | An unconstrained schema (`true`-equivalent) with a `$comment` |

Objects declare `additionalProperties: false` because the deserializer rejects
unknown properties, unless the type declares an extension-data member, in which
case the keyword is omitted and a `$comment` explains why. `readOnly` and
`writeOnly` reflect properties that cannot be written or read respectively.

Non-standard information uses `x-`-prefixed keywords, which validators ignore:
`x-shapeshift-undocumented`, `x-shapeshift-converter`,
`x-shapeshift-surrogate-for`, `x-shapeshift-enum-serialized-as`,
`x-shapeshift-enum-flags`, and `x-shapeshift-max-binary-length`.

### Security limits

Schemas are portable by default and therefore carry no size limits. Pass
`JsonSchemaLimits` to emit `maxItems`, `maxProperties`, `maxLength`, and
`x-shapeshift-max-binary-length` matching a `SerializationContext`'s configured
limits, which lets a gateway reject oversized documents before the serializer
sees them.

[!code-csharp[Limits](../../samples/cs/SchemaGeneration.cs#Limits)]

## MessagePack annotations

`JsonSchemaProfile.MessagePack` projects the same contract with MessagePack's
representation choices and annotations. JSON Schema remains the description
language; the `x-msgpack-type` and `x-msgpack-extension` keywords record the
MessagePack token family and extension type code that would actually be used.

[!code-csharp[MessagePackProfile](../../samples/cs/SchemaGeneration.cs#MessagePackProfile)]

Differences from the JSON profile:

- Binary uses the `bin` family (`x-msgpack-type: bin`) rather than base64 text.
- `DateTime` uses the standard timestamp extension (`x-msgpack-extension: -1`).
- `decimal`, `Int128`, `UInt128`, `BigInteger`, and `TimeSpan` use the reserved
  ShapeShift extension codes documented in
  [MessagePack extension types](msgpack.md#shapeshift-extension-types).
- `TimeSpan` is an integer tick count rather than a string.
- Named floating-point values are never offered, because MessagePack encodes
  non-finite values natively.
- Objects, arrays, and maps carry `x-msgpack-type` describing the token family.

## Sample

The complete sample used above is
[SchemaGeneration.cs](https://github.com/AArnott/ShapeShift/blob/main/samples/cs/SchemaGeneration.cs).
