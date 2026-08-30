# Customizing the core

Everything on this page is declared by the format-neutral `ShapeShift` package,
so it applies identically to `ShapeShift.Json`, `ShapeShift.MsgPack`,
`ShapeShift.Yaml`, `ShapeShift.Taml`, and any third-party format package. The
samples use `JsonSerializer` only because its output is readable.

Nothing here uses reflection: converters are supplied as instances or built by
factories from source-generated PolyType shapes, so a customized serializer is
as trimming-safe and NativeAOT-safe as an unconfigured one. The one exception is
the explicitly annotated `WithReflectionConverterTypes` opt-in described under
[Reflection-based activation](#reflection-based-activation).

## Immutable configuration

A serializer is an immutable record. Configure it at construction, hold it in a
static field, and use it concurrently; there are no mutable process-wide
defaults to race with. Deriving a variation with a `with` expression produces a
new serializer and leaves the original — and its converter cache — alone.

[!code-csharp[ImmutableConfiguration](../../samples/cs/CoreCustomization.cs#ImmutableConfiguration)]

Each distinct configuration builds its own converter cache the first time it is
used, so prefer a small number of long-lived serializers over one per operation.

## Naming

`PropertyNamingPolicy` renames every property that does not name itself.
`CamelCase`, `PascalCase`, `KebabLowerCase`, `KebabUpperCase`, `SnakeLowerCase`,
and `SnakeUpperCase` are built in, and a custom policy is one `ConvertName`
override away.

[!code-csharp[NamingPolicy](../../samples/cs/CoreCustomization.cs#NamingPolicy)]

Individual members opt out by naming themselves, which is also how a member is
excluded from the contract entirely:

[!code-csharp[IncludingExcludingMembers](../../samples/cs/CoreCustomization.cs#IncludingExcludingMembers)]

[!code-csharp[ChangingPropertyNames](../../samples/cs/CoreCustomization.cs#ChangingPropertyNames)]

Enum values are written by name by default and honor their own aliases. Set
`SerializeEnumValuesByName` to `false` to write the underlying numbers instead.

[!code-csharp[ChangingEnumNames](../../samples/cs/CoreCustomization.cs#ChangingEnumNames)]

The analyzers report a name that two members would share, either directly
([SHIFT005](analyzers/SHIFT005.md)) or after a naming policy is applied
([SHIFT006](analyzers/SHIFT006.md)).

## Default-value omission

`SerializeDefaultValues` chooses which properties whose values equal their
declared defaults are written. `Always` (the default) writes everything;
`Required` keeps the values needed to reconstruct the object and drops the rest;
`Never` drops them all; `ValueTypes` and `ReferenceTypes` select by category.

[!code-csharp[DefaultValues](../../samples/cs/CoreCustomization.cs#DefaultValues)]

Omission is a property of map-shaped output. A positional encoding cannot
generally omit an interior value without a presence scheme, so format-specific
positional converters may decline it; see
[MessagePack positional contracts](msgpack.md#positional-array-contracts).

## Strictness

Deserialization is strict by default: a duplicate property is an error, a
missing required constructor parameter or required member is an error, and a
null assigned to a non-nullable member is an error. Each failure carries a
[path breadcrumb](diagnostics.md) naming the offending value.

[!code-csharp[Strictness](../../samples/cs/CoreCustomization.cs#Strictness)]

`DeserializeDefaultValuesPolicy` relaxes those rules deliberately and per
serializer:

| Value | Effect |
| -- | -- |
| `Default` | Rejects missing required values and nulls for non-nullable members. |
| `AllowNullValuesForNonNullableProperties` | Accepts an explicit null for a non-nullable member. |
| `AllowMissingValuesForRequiredProperties` | Also accepts a payload that omits a required value, leaving the declared default in place. |

Immutable types are supported without relaxing anything: a constructor parameter
is matched to the property it initializes, including when that property renames
itself.

[!code-csharp[DeserializingConstructors](../../samples/cs/CoreCustomization.cs#DeserializingConstructors)]

## Security limits

Untrusted input is bounded by the context every operation starts from. The
limits below are enforced by the shared converters and by every conforming
format package, and custom converters are expected to honor them too.

[!code-csharp[SecurityLimits](../../samples/cs/CoreCustomization.cs#SecurityLimits)]

| Limit | Default | Bounds |
| -- | -- | -- |
| `MaxDepth` | 64 | Nesting of the object graph. |
| `MaxCollectionLength` | 1,000,000 | Elements in one collection. |
| `MaxStringLength` | 16,777,216 | Characters in one string. |
| `MaxBinaryLength` | 67,108,864 | Bytes in one binary value. |

Hostile dictionary keys are a separate concern: choose a comparer for the
member rather than relying on the type's default equality. Specify it with an
attribute so ShapeShift uses it while deserializing, and in the initializer so
code that constructs the object uses it too.

[!code-csharp[CustomComparerOnMember](../../samples/cs/CoreCustomization.cs#CustomComparerOnMember)]

Collision-resistant hashing for structural comparers is a distinct opt-in; see
[Structural equality and hashing](equality.md).

## Custom converters

Register a converter *instance* when a type has one representation the whole
application agrees on. This is the most direct customization: no activation and
nothing for trimming to preserve. Overriding `GetContract` keeps
[schema generation](schema.md) honest about what the converter actually writes.

[!code-csharp[ConverterInstance](../../samples/cs/CoreCustomization.cs#ConverterInstance)]

Converters are appended to the collection the format already installed, not
substituted for it, so format-provided converters survive:

[!code-csharp[RegisterConverters](../../samples/cs/CoreCustomization.cs#RegisterConverters)]

A converter may also be attached to a type, property, or parameter with
`ShapeShiftConverterAttribute`, which PolyType resolves as an associated type
rather than by reflection. The analyzers verify that the attributed type really
is a converter ([SHIFT001](analyzers/SHIFT001.md)), that it can be constructed
([SHIFT002](analyzers/SHIFT002.md)), and that it converts the type it is applied
to ([SHIFT003](analyzers/SHIFT003.md)).

## Converter factories

A factory answers for types it cannot enumerate ahead of time. It is consulted
after `Converters` and returns `null` for anything it does not handle.

When the factory knows exactly which type it serves, no generic type parameter
is needed:

[!code-csharp[FactoryNonGeneric](../../samples/cs/CoreCustomization.cs#FactoryNonGeneric)]

When the converter needs the converted type as a generic type parameter,
implement `ITypeShapeFunc` on the same class and let the shape call back into
it. `ITypeShape<T>.Invoke` supplies `T` without reflection, which is what keeps
this pattern NativeAOT-safe:

[!code-csharp[FactoryGeneric](../../samples/cs/CoreCustomization.cs#FactoryGeneric)]

When the converter needs generic type parameters for *parts* of the type — the
element type of a collection, for instance — use a `TypeShapeVisitor`, and ask
the `ConverterContext` for the converters of those parts:

[!code-csharp[FactoryVisitor](../../samples/cs/CoreCustomization.cs#FactoryVisitor)]

A converter obtained from `ConverterContext` or `SerializationContext` is the
converter the serializer would otherwise have used, so delegating to it composes
with the rest of the configuration. Call `DepthStep` before converting nested
values, and honor `MaxCollectionLength` and the other limits, so a custom
converter is as safe against hostile input as a built-in one.

## Converter state

`StartingContext` supplies the ambient state and limits each operation begins
with. Because the context is a struct, change a local copy and reassign it:

[!code-csharp[ModifyingStartingContextState](../../samples/cs/CoreCustomization.cs#ModifyingStartingContextState)]

Use an object reference as the key, exposed by whichever component reads it, so
that two unrelated components cannot collide on the same string.

[!code-csharp[ConverterState](../../samples/cs/CoreCustomization.cs#ConverterState)]

## Reflection-based activation

`WithReflectionConverterTypes` accepts converter `Type` objects and activates
them at runtime. It is annotated with `RequiresDynamicCode` and
`RequiresUnreferencedCode` and reported by
[SHIFT007](analyzers/SHIFT007.md), because a constructor reached only through a
`Type` can be trimmed away. An application that never calls it is unaffected:
converter instances, factories, and generated shapes remain the default path.

## Sample

The complete, executable sample used above is
[CoreCustomization.cs](https://github.com/AArnott/ShapeShift/blob/main/samples/cs/CoreCustomization.cs),
and it is exercised by `test/Samples.Tests`.
