// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Equality;

/// <summary>
/// The base class for all structural equality comparers produced from a type shape.
/// </summary>
/// <typeparam name="T">The type of value to compare.</typeparam>
/// <remarks>
/// Derived types implement <see cref="EqualsCore"/> and <see cref="GetHashCodeCore"/>, which
/// receive the traversal state required to terminate on cyclic object graphs. The
/// <see cref="IEqualityComparer{T}"/> implementation creates that state and applies the
/// uniform <see langword="null"/> handling rules.
/// </remarks>
internal abstract class StructuralComparer<T> : IEqualityComparer<T>
{
	/// <summary>
	/// Gets a value indicating whether values of <typeparamref name="T"/> can participate in reference cycles.
	/// </summary>
	protected static bool IsReferenceType { get; } = !typeof(T).IsValueType;

	/// <inheritdoc/>
	public bool Equals(T? x, T? y)
	{
		ComparisonState state = default;
		return this.EqualsWithNullHandling(x, y, ref state);
	}

	/// <inheritdoc/>
	public int GetHashCode(T obj)
	{
		HashState state = default;
		int hash = this.GetHashCodeWithNullHandling(obj, ref state);
		return state.CycleDetected ? HashCombiner.CyclicHash : hash;
	}

	/// <summary>
	/// Compares two non-<see langword="null"/> values for structural equality.
	/// </summary>
	/// <param name="x">The first value.</param>
	/// <param name="y">The second value.</param>
	/// <param name="state">The traversal state that guarantees termination on cyclic graphs.</param>
	/// <returns><see langword="true"/> if the values are structurally equal.</returns>
	internal abstract bool EqualsCore(T x, T y, ref ComparisonState state);

	/// <summary>
	/// Computes the structural hash code of a non-<see langword="null"/> value.
	/// </summary>
	/// <param name="value">The value to hash.</param>
	/// <param name="state">The traversal state that memoizes shared nodes and detects cycles.</param>
	/// <returns>The hash code.</returns>
	internal abstract int GetHashCodeCore(T value, ref HashState state);

	/// <summary>
	/// Compares two possibly <see langword="null"/> values for structural equality.
	/// </summary>
	/// <param name="x">The first value.</param>
	/// <param name="y">The second value.</param>
	/// <param name="state">The traversal state.</param>
	/// <returns><see langword="true"/> if the values are structurally equal.</returns>
	internal bool EqualsWithNullHandling(T? x, T? y, ref ComparisonState state)
	{
		if (x is null)
		{
			return y is null;
		}

		return y is not null && this.EqualsCore(x, y, ref state);
	}

	/// <summary>
	/// Computes the structural hash code of a possibly <see langword="null"/> value.
	/// </summary>
	/// <param name="value">The value to hash.</param>
	/// <param name="state">The traversal state.</param>
	/// <returns>The hash code.</returns>
	internal int GetHashCodeWithNullHandling(T? value, ref HashState state)
		=> value is null ? HashCombiner.NullHash : this.GetHashCodeCore(value, ref state);
}
