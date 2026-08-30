// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Equality;

/// <summary>
/// Compares values of a union type by first comparing which case each value belongs to.
/// </summary>
/// <typeparam name="TUnion">The declared union type.</typeparam>
/// <param name="getUnionCaseIndex">Determines which case a value belongs to.</param>
/// <param name="baseComparer">The comparer for values that match no declared case.</param>
/// <param name="caseComparers">The comparer for each declared case, indexed by case index.</param>
internal sealed class UnionComparer<TUnion>(
	Getter<TUnion, int> getUnionCaseIndex,
	StructuralComparer<TUnion> baseComparer,
	StructuralComparer<TUnion>[] caseComparers) : StructuralComparer<TUnion>
{
	private const int Seed = 0x554E4900;

	/// <inheritdoc/>
	internal override bool EqualsCore(TUnion x, TUnion y, ref ComparisonState state)
	{
		int xIndex = getUnionCaseIndex(ref Unsafe.AsRef(in x));
		int yIndex = getUnionCaseIndex(ref Unsafe.AsRef(in y));
		return xIndex == yIndex && this.GetComparer(xIndex).EqualsCore(x, y, ref state);
	}

	/// <inheritdoc/>
	internal override int GetHashCodeCore(TUnion value, ref HashState state)
	{
		int index = getUnionCaseIndex(ref Unsafe.AsRef(in value));
		return HashCombiner.Finalize(HashCombiner.Combine(
			HashCombiner.Combine(Seed, index),
			this.GetComparer(index).GetHashCodeCore(value, ref state)));
	}

	private StructuralComparer<TUnion> GetComparer(int index)
		=> index < 0 ? baseComparer : caseComparers[index];
}
