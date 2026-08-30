// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Equality;

/// <summary>
/// Delegates to a user supplied comparer for a particular type.
/// </summary>
/// <typeparam name="T">The type being compared.</typeparam>
/// <param name="comparer">The user supplied comparer.</param>
/// <param name="policy">The hashing policy, applied to the user comparer's hash code.</param>
internal sealed class UserComparer<T>(IEqualityComparer<T> comparer, HashingPolicy policy) : StructuralComparer<T>
{
	/// <inheritdoc/>
	internal override bool EqualsCore(T x, T y, ref ComparisonState state) => comparer.Equals(x, y);

	/// <inheritdoc/>
	internal override int GetHashCodeCore(T value, ref HashState state) => policy.HashOpaque(comparer.GetHashCode(value!));
}
