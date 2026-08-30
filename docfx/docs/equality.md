# Structural equality and hashing

Two object graphs that were serialized to the same bytes should be considered
equal, even when neither type overrides <xref:System.Object.Equals(System.Object)>.
`ShapeShift.Equality` builds a deep <xref:System.Collections.Generic.IEqualityComparer`1>
for any type that has a PolyType shape, using the same source-generated shape
information the serializer uses. There is no reflection, no dynamic code
generation, and no `InternalsVisibleTo`: everything is NativeAOT safe and
trimming friendly.

## Getting a comparer

@ShapeShift.Equality.StructuralEqualityComparer is the entry point:

[!code-csharp[Basics](../../samples/cs/StructuralEquality.cs#Basics)]

The comparer is an ordinary `IEqualityComparer<T>`, so it drops straight into
`Dictionary<TKey, TValue>`, `HashSet<T>`, `Enumerable.Distinct` and friends:

[!code-csharp[HashSet](../../samples/cs/StructuralEquality.cs#HashSet)]

Overloads exist for the shape-witness patterns PolyType supports:

```cs
IEqualityComparer<Person> a = StructuralEqualityComparer.Create<Person>();               // Person : IShapeable<Person>
IEqualityComparer<Person> b = StructuralEqualityComparer.Create<Person, Witness>();      // external witness
IEqualityComparer<Person> c = StructuralEqualityComparer.Create(someTypeShape);          // an ITypeShape<Person> you already have
```

Comparers are built once per type per
@ShapeShift.Equality.StructuralEqualityComparerProvider and cached, so hold onto
the comparer (or the provider) rather than calling `Create` in a loop.

## What is compared

Every shape kind the serializer understands is supported:

| Shape | Semantics |
| --- | --- |
| Primitives and other leaf types | The type's own `IEqualityComparer<T>.Default` (`string` is ordinal). |
| Objects and constructor-backed contracts | Every readable property and field of the shape, compared structurally. |
| Sequences (lists, arrays, spans, ...) | Element-wise, in order, with matching lengths. |
| Sets (`IsSetType` shapes) | Unordered multiset comparison. |
| Dictionaries (string and non-string keys) | Unordered; each key/value pair must have a structurally equal partner. |
| Rectangular (multidimensional) arrays | Rank, per-dimension lengths, then row-major elements, so a 2&times;3 array never equals a 3&times;2 array. |
| Enums | Underlying value. |
| Optionals (`T?`, `Option<T>`, ...) | "Has value" flags must match; values compared structurally. |
| Unions | The selected case must match, then the case value is compared structurally. |
| Surrogates | Both sides are projected through the surrogate marshaler and the surrogate is compared. Only state the surrogate carries participates in equality. |
| `ShapeShiftValue` | Structural: maps are order-independent, arrays element-wise, binary by byte content. |
| `byte[]` and `ReadOnlyMemory<byte>` | By content, not by reference. |

`null` equals `null`, and `null` never equals a non-`null` value.
`GetHashCode(null)` is `0`.

### Dictionaries ignore their own comparer

A dictionary's configured <xref:System.Collections.Generic.IEqualityComparer`1>
is *not* consulted. The structural key comparer is authoritative, so a
`Dictionary<string, int>` created with `StringComparer.OrdinalIgnoreCase` and
holding `"A"` is **not** structurally equal to one holding `"a"`. This keeps
equality a property of the data, matching what round-tripping through the
serializer would produce; the comparer a particular in-memory instance happens
to use is not part of the payload.

If you do want case-insensitive keys, override the comparer for `string`
(see [custom comparers](#custom-comparers)); the override applies everywhere
`string` appears, including dictionary keys.

### Converters do not participate

Custom converters registered with a serializer, and `[UseComparer]`, change how
values are *written*, not what they *mean*. They are deliberately ignored here so
that equality does not depend on which serializer instance you happen to have
configured. Use `WithComparer<T>` when you need to influence equality.

## Cycles and shared references

Object graphs may contain cycles and shared subgraphs. Both are handled, and
comparison always terminates.

[!code-csharp[Cycles](../../samples/cs/StructuralEquality.cs#Cycles)]

### Equality is value equivalence, not graph topology

The comparer answers "do these two graphs denote the same value?", not "do these
two graphs have the same shape?". Concretely:

* A self-loop is equal to an equivalent two-node cycle, as in the sample above.
* A node that is shared by two parents is equal to two separate but equal nodes.

This is a *bisimulation*: while comparing `(x, y)` the comparer optimistically
assumes `x == y`, and only a concrete mismatch somewhere in the unfolding can
disprove it. Because any mismatch aborts the whole comparison, the optimistic
assumption is sound. It also memoizes acyclic sharing, so comparing a wide DAG
is linear rather than exponential.

If you need reference topology to matter, compare
<xref:System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(System.Object)>
identity yourself; that is a different question than the one this API answers.

### Hash codes of cyclic graphs

`GetHashCode` unfolds the graph as a tree, memoizing by reference identity so
shared subgraphs are hashed once. When a cycle is encountered &mdash; that is,
when an object is reached that is already on the current path &mdash; the entire
hash code collapses to a single fixed constant.

This satisfies the `IEqualityComparer<T>` contract: equal objects still produce
equal hash codes, because a cyclic graph is never structurally equal to an
acyclic one, so all graphs sharing that constant are exactly the graphs that
*could* be equal to each other. The practical consequence is that cyclic graphs
all land in one hash bucket. If cyclic keys are common and performance matters,
use a comparer keyed on something cheaper.

## Custom comparers

@ShapeShift.Equality.StructuralEqualityComparerProvider is an immutable record.
`WithComparer<T>` returns a new provider in which occurrences of `T` &mdash;
anywhere in any graph, at any depth &mdash; use the comparer you supply instead
of being decomposed structurally:

[!code-csharp[CustomComparer](../../samples/cs/StructuralEquality.cs#CustomComparer)]

The supplied comparer's own `GetHashCode` is used, then run through the
provider's hashing policy, so an override composes correctly with
collision-resistant hashing.

## Deterministic versus collision-resistant hashing

By default hashing is **deterministic**: the same graph produces the same hash
code in every process and on every run, on any platform, for the same version of
this library. That makes hash codes usable in tests, in logs, and as cheap
change-detection fingerprints.

Determinism has a cost: an attacker who can choose your keys can compute
colliding keys offline and degrade a hash table to a linear scan. When keys come
from an untrusted source, opt in to collision-resistant hashing:

[!code-csharp[CollisionResistant](../../samples/cs/StructuralEquality.cs#CollisionResistant)]

Collision-resistant comparers hash strings, byte sequences and leaf hash codes
with SipHash-2-4 under a 128-bit key drawn from a cryptographic RNG once per
process.

> [!CAUTION]
> Caveats of collision-resistant hashing:
>
> * Hash codes differ between processes and between runs. **Never persist them,
>   send them across a wire, or compare them across process boundaries.**
> * It is a hash-flooding mitigation, not a MAC and not a cryptographic digest of
>   your data. Do not use it for authentication or integrity.
> * It is slower than the deterministic policy.
> * Equality semantics are unaffected: a collision-resistant comparer and a
>   deterministic comparer always agree on `Equals`.

Both policies are available as cached singletons:
@ShapeShift.Equality.StructuralEqualityComparerProvider.Default and
@ShapeShift.Equality.StructuralEqualityComparerProvider.CollisionResistant, and
the setting is also exposed as the `UseCollisionResistantHashing` init property
so it can be combined with `WithComparer<T>`.

## Thread safety

Providers and the comparers they produce are immutable and safe for concurrent
use. Comparison state (the assumption set used for cycles) lives on the stack of
the call, so a single comparer instance can serve any number of threads.
