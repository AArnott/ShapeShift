// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Equality;

/// <summary>
/// Adapts <see cref="ShapeShiftValueComparer"/> to a type derived from <see cref="ShapeShiftValue"/>.
/// </summary>
/// <typeparam name="T">The <see cref="ShapeShiftValue"/> derived type.</typeparam>
/// <param name="inner">The comparer for the value model.</param>
internal sealed class DerivedShapeShiftValueComparer<T>(ShapeShiftValueComparer inner) : StructuralComparer<T>
{
	/// <inheritdoc/>
	internal override bool EqualsCore(T x, T y, ref ComparisonState state)
		=> inner.EqualsCore((ShapeShiftValue)(object)x!, (ShapeShiftValue)(object)y!, ref state);

	/// <inheritdoc/>
	internal override int GetHashCodeCore(T value, ref HashState state)
		=> inner.GetHashCodeCore((ShapeShiftValue)(object)value!, ref state);
}
