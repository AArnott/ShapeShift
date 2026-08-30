// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Frozen;

namespace ShapeShift.Equality;

/// <summary>
/// Creates deep, structural <see cref="IEqualityComparer{T}"/> implementations from PolyType shapes.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable record. Each mutation returns a new instance, allowing an instance to be
/// safely shared and cached. Comparers are memoized per instance, so reusing an instance avoids
/// rebuilding the comparer graph.
/// </para>
/// <para>
/// See the <see href="https://aarnott.github.io/ShapeShift/docs/equality.html">structural equality</see>
/// documentation for the precise semantics that the generated comparers implement.
/// </para>
/// </remarks>
public sealed record StructuralEqualityComparerProvider
{
	private FrozenDictionary<Type, object> comparerOverrides = FrozenDictionary<Type, object>.Empty;
	private bool useCollisionResistantHashing;
	private MultiProviderTypeCache? cache;

	/// <summary>
	/// Initializes a new instance of the <see cref="StructuralEqualityComparerProvider"/> class.
	/// </summary>
	public StructuralEqualityComparerProvider()
	{
	}

	/// <summary>
	/// Gets a provider that produces comparers with deterministic (non-randomized) hash codes.
	/// </summary>
	public static StructuralEqualityComparerProvider Default { get; } = new();

	/// <summary>
	/// Gets a provider that produces comparers with process-randomized, collision resistant hash codes.
	/// </summary>
	/// <remarks>
	/// Hash codes produced by comparers from this provider must never be persisted or transmitted,
	/// as they vary from one process to the next.
	/// </remarks>
	public static StructuralEqualityComparerProvider CollisionResistant { get; } = new() { UseCollisionResistantHashing = true };

	/// <summary>
	/// Gets a value indicating whether the comparers hash the full content of values using a
	/// process-randomized, keyed hash function.
	/// </summary>
	/// <value>The default value is <see langword="false" />.</value>
	/// <remarks>
	/// <para>
	/// When <see langword="false" /> (the default), hash codes are computed by combining the hash codes
	/// that the leaf values themselves report, except for <see cref="string"/> and <see cref="byte"/>
	/// arrays, which are hashed with a deterministic content hash so that the result is stable across
	/// processes. Such hash codes are cheap but offer no protection against an adversary who chooses
	/// inputs that deliberately collide.
	/// </para>
	/// <para>
	/// When <see langword="true" />, every well-known leaf value is hashed by content with
	/// <see href="https://en.wikipedia.org/wiki/SipHash">SipHash-2-4</see> under a key that is randomly
	/// generated once per process. This dramatically raises the cost of finding collisions, at the cost
	/// of speed and of any cross-process stability.
	/// </para>
	/// </remarks>
	public bool UseCollisionResistantHashing
	{
		get => this.useCollisionResistantHashing;
		init => this.ChangeSetting(ref this.useCollisionResistantHashing, value);
	}

	/// <summary>
	/// Gets the hashing policy implied by <see cref="UseCollisionResistantHashing"/>.
	/// </summary>
	internal HashingPolicy Policy => this.UseCollisionResistantHashing ? HashingPolicy.CollisionResistant : HashingPolicy.Deterministic;

	/// <summary>
	/// Gets the user supplied comparers, keyed by the type they compare.
	/// </summary>
	private FrozenDictionary<Type, object> Overrides
	{
		get => this.comparerOverrides;
		init => this.ChangeSetting(ref this.comparerOverrides, value);
	}

	/// <summary>
	/// Gets the cache of comparers created by this instance.
	/// </summary>
	private MultiProviderTypeCache Cache
	{
		get
		{
			MultiProviderTypeCache? existing = this.cache;
			if (existing is null)
			{
				HashingPolicy policy = this.Policy;
				FrozenDictionary<Type, object> overrides = this.comparerOverrides;
				MultiProviderTypeCache created = new()
				{
					DelayedValueFactory = new DelayedComparerFactory(),
					ValueBuilderFactory = ctx => new EqualityVisitor(policy, overrides, ctx),
				};
				existing = Interlocked.CompareExchange(ref this.cache, created, null) ?? created;
			}

			return existing;
		}
	}

	/// <summary>
	/// Returns a provider that uses a caller supplied comparer for a particular type instead of
	/// comparing values of that type structurally.
	/// </summary>
	/// <typeparam name="T">The type whose comparison should be delegated to <paramref name="comparer"/>.</typeparam>
	/// <param name="comparer">
	/// The comparer to use. Its <see cref="IEqualityComparer{T}.GetHashCode(T)"/> must be consistent with its
	/// <see cref="IEqualityComparer{T}.Equals(T, T)"/> method.
	/// </param>
	/// <returns>A new provider with the override applied.</returns>
	/// <remarks>
	/// The override applies wherever a value of type <typeparamref name="T"/> appears in a graph,
	/// including as an element of a collection, a dictionary key, or a member of an object.
	/// </remarks>
	public StructuralEqualityComparerProvider WithComparer<T>(IEqualityComparer<T> comparer)
	{
		Requires.NotNull(comparer);
		Dictionary<Type, object> builder = new(this.comparerOverrides.Count + 1);
		foreach (KeyValuePair<Type, object> pair in this.comparerOverrides)
		{
			builder[pair.Key] = pair.Value;
		}

		builder[typeof(T)] = comparer;
		return this with { Overrides = builder.ToFrozenDictionary() };
	}

	/// <summary>
	/// Gets a structural comparer for a type with a known shape.
	/// </summary>
	/// <typeparam name="T">The type to compare.</typeparam>
	/// <param name="shape">The shape of the type to compare.</param>
	/// <returns>A structural comparer.</returns>
	public IEqualityComparer<T> GetComparer<T>(ITypeShape<T> shape)
	{
		Requires.NotNull(shape);
		return (IEqualityComparer<T>)this.Cache.GetOrAdd(shape)!;
	}

	/// <summary>
	/// Gets a structural comparer for a type with a shape provided by another type.
	/// </summary>
	/// <typeparam name="T">The type to compare.</typeparam>
	/// <param name="provider">The shape provider.</param>
	/// <returns>A structural comparer.</returns>
	public IEqualityComparer<T> GetComparer<T>(ITypeShapeProvider provider)
	{
		Requires.NotNull(provider);
		return (IEqualityComparer<T>)this.Cache.GetOrAddOrThrow(typeof(T), provider);
	}

	/// <summary>
	/// Gets a structural comparer for a self-describing type.
	/// </summary>
	/// <typeparam name="T">The type to compare.</typeparam>
	/// <returns>A structural comparer.</returns>
	public IEqualityComparer<T> GetComparer<T>()
		where T : IShapeable<T> => this.GetComparer(T.GetTypeShape());

	/// <summary>
	/// Gets a structural comparer for a type whose shape is provided by a witness type.
	/// </summary>
	/// <typeparam name="T">The type to compare.</typeparam>
	/// <typeparam name="TProvider">The witness type that describes <typeparamref name="T"/>.</typeparam>
	/// <returns>A structural comparer.</returns>
	public IEqualityComparer<T> GetComparer<T, TProvider>()
		where TProvider : IShapeable<T> => this.GetComparer(TProvider.GetTypeShape());

	private void ChangeSetting<T>(ref T location, T value)
	{
		if (!EqualityComparer<T>.Default.Equals(location, value))
		{
			this.cache = null;
			location = value;
		}
	}
}
