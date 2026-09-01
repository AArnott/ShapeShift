// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Equality;

/// <summary>
/// A factory for comparers that stand in for a comparer that is still under construction,
/// which is what makes recursive type shapes possible.
/// </summary>
internal sealed class DelayedComparerFactory : IDelayedValueFactory
{
	/// <inheritdoc/>
	public DelayedValue Create<T>(ITypeShape<T> typeShape)
		=> new DelayedValue<StructuralComparer<T>>(self => new DelayedComparer<T>(self));

	/// <summary>
	/// A comparer that forwards to another comparer that is not yet available.
	/// </summary>
	/// <typeparam name="T">The type to compare.</typeparam>
	/// <param name="self">A box that will eventually contain the real comparer.</param>
	private sealed class DelayedComparer<T>(DelayedValue<StructuralComparer<T>> self) : StructuralComparer<T>
	{
		/// <inheritdoc/>
		internal override bool EqualsCore(T x, T y, ref ComparisonState state) => self.Result.EqualsCore(x, y, ref state);

		/// <inheritdoc/>
		internal override int GetHashCodeCore(T value, ref HashState state) => self.Result.GetHashCodeCore(value, ref state);
	}
}
