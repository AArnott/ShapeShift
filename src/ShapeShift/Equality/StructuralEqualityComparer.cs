// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Equality;

/// <summary>
/// Creates deep, structural <see cref="IEqualityComparer{T}"/> implementations for types described by
/// PolyType shapes.
/// </summary>
/// <remarks>
/// <para>
/// The comparers created by this class compare entire object graphs by value: two values are equal when
/// their corresponding leaves are equal, regardless of whether they are the same object, how many objects
/// they are built from, or in what order a dictionary or set happens to enumerate its contents.
/// Reference cycles are supported.
/// </para>
/// <para>
/// The comparers are trimming and NativeAOT safe because they are built exclusively from source generated
/// shapes; no reflection is used.
/// </para>
/// <para>
/// Every method here is a shortcut for the equivalent method on
/// <see cref="StructuralEqualityComparerProvider.Default"/> or
/// <see cref="StructuralEqualityComparerProvider.CollisionResistant"/>. Use the provider directly when
/// comparers for particular types should be customized.
/// </para>
/// </remarks>
public static class StructuralEqualityComparer
{
	/// <summary>
	/// Creates a structural comparer for a self-describing type.
	/// </summary>
	/// <typeparam name="T">The type to compare.</typeparam>
	/// <returns>A structural comparer.</returns>
	public static IEqualityComparer<T> Create<T>()
		where T : IShapeable<T> => StructuralEqualityComparerProvider.Default.GetComparer<T>();

	/// <summary>
	/// Creates a structural comparer for a type whose shape is provided by a witness type.
	/// </summary>
	/// <typeparam name="T">The type to compare.</typeparam>
	/// <typeparam name="TProvider">The witness type that describes <typeparamref name="T"/>.</typeparam>
	/// <returns>A structural comparer.</returns>
	public static IEqualityComparer<T> Create<T, TProvider>()
		where TProvider : IShapeable<T> => StructuralEqualityComparerProvider.Default.GetComparer<T, TProvider>();

	/// <summary>
	/// Creates a structural comparer for a type with a known shape.
	/// </summary>
	/// <typeparam name="T">The type to compare.</typeparam>
	/// <param name="shape">The shape of the type to compare.</param>
	/// <returns>A structural comparer.</returns>
	public static IEqualityComparer<T> Create<T>(ITypeShape<T> shape)
		=> StructuralEqualityComparerProvider.Default.GetComparer(shape);

	/// <summary>
	/// Creates a structural comparer whose hash codes are randomized per process and resistant to
	/// deliberately constructed collisions.
	/// </summary>
	/// <typeparam name="T">The type to compare.</typeparam>
	/// <returns>A structural comparer.</returns>
	/// <inheritdoc cref="CreateCollisionResistant{T}(ITypeShape{T})" path="/remarks"/>
	public static IEqualityComparer<T> CreateCollisionResistant<T>()
		where T : IShapeable<T> => StructuralEqualityComparerProvider.CollisionResistant.GetComparer<T>();

	/// <summary>
	/// Creates a structural comparer whose hash codes are randomized per process and resistant to
	/// deliberately constructed collisions.
	/// </summary>
	/// <typeparam name="T">The type to compare.</typeparam>
	/// <typeparam name="TProvider">The witness type that describes <typeparamref name="T"/>.</typeparam>
	/// <returns>A structural comparer.</returns>
	/// <inheritdoc cref="CreateCollisionResistant{T}(ITypeShape{T})" path="/remarks"/>
	public static IEqualityComparer<T> CreateCollisionResistant<T, TProvider>()
		where TProvider : IShapeable<T> => StructuralEqualityComparerProvider.CollisionResistant.GetComparer<T, TProvider>();

	/// <summary>
	/// Creates a structural comparer whose hash codes are randomized per process and resistant to
	/// deliberately constructed collisions.
	/// </summary>
	/// <typeparam name="T">The type to compare.</typeparam>
	/// <param name="shape">The shape of the type to compare.</param>
	/// <returns>A structural comparer.</returns>
	/// <remarks>
	/// <para>
	/// Equality is identical to that of the comparers returned by the <c>Create</c> methods.
	/// Only the hash codes differ.
	/// </para>
	/// <para>
	/// The hash codes are computed with SipHash-2-4 over the content of each leaf value, under a key that
	/// is randomly generated once per process. This makes it impractical for an untrusted party to craft
	/// inputs that collide, which protects hash based collections from algorithmic complexity attacks.
	/// </para>
	/// <para>
	/// The caveats are important:
	/// hash codes are <em>not</em> comparable across processes or machines and must never be persisted or
	/// transmitted;
	/// hashing is meaningfully slower than the default policy because every leaf is hashed by content;
	/// and the result is not a message authentication code, since the hash is truncated to 32 bits.
	/// </para>
	/// </remarks>
	public static IEqualityComparer<T> CreateCollisionResistant<T>(ITypeShape<T> shape)
		=> StructuralEqualityComparerProvider.CollisionResistant.GetComparer(shape);
}
