// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Equality;

/// <summary>
/// Compares values that are treated as indivisible leaves, delegating equality to the type's own
/// semantics and hashing to the active <see cref="HashingPolicy"/>.
/// </summary>
/// <typeparam name="T">The leaf type.</typeparam>
/// <param name="equality">The equality semantics for the leaf type.</param>
/// <param name="hash">The hash function for the leaf type.</param>
internal sealed class LeafComparer<T>(IEqualityComparer<T> equality, Func<T, int> hash) : StructuralComparer<T>
{
	/// <inheritdoc/>
	internal override bool EqualsCore(T x, T y, ref ComparisonState state) => equality.Equals(x, y);

	/// <inheritdoc/>
	internal override int GetHashCodeCore(T value, ref HashState state) => hash(value);
}
