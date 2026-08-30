// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Equality;

/// <summary>
/// Compares rectangular (multidimensional) arrays, taking their dimension lengths into account.
/// </summary>
/// <typeparam name="TArray">The array type.</typeparam>
/// <typeparam name="TElement">The element type.</typeparam>
/// <param name="getEnumerable">Exposes the array's elements in row-major order.</param>
/// <param name="elementComparer">The structural comparer for elements.</param>
/// <param name="rank">The number of dimensions.</param>
/// <remarks>
/// Two arrays with the same elements but different dimension lengths (for example 2x3 and 3x2)
/// are not equal.
/// </remarks>
internal sealed class MultidimensionalArrayComparer<TArray, TElement>(
	Func<TArray, IEnumerable<TElement>> getEnumerable,
	StructuralComparer<TElement> elementComparer,
	int rank) : StructuralComparer<TArray>
{
	private const int Seed = 0x4D444100;

	/// <inheritdoc/>
	internal override bool EqualsCore(TArray x, TArray y, ref ComparisonState state)
	{
		if (ReferenceEquals(x, y))
		{
			return true;
		}

		Array left = (Array)(object)x!;
		Array right = (Array)(object)y!;
		for (int dimension = 0; dimension < rank; dimension++)
		{
			if (left.GetLength(dimension) != right.GetLength(dimension))
			{
				return false;
			}
		}

		if (state.EnterOrAssumeEqual(x!, y!))
		{
			state.Exit();
			return true;
		}

		bool result = true;
		using (IEnumerator<TElement> leftElements = getEnumerable(x).GetEnumerator())
		using (IEnumerator<TElement> rightElements = getEnumerable(y).GetEnumerator())
		{
			while (leftElements.MoveNext() && rightElements.MoveNext())
			{
				if (!elementComparer.EqualsWithNullHandling(leftElements.Current, rightElements.Current, ref state))
				{
					result = false;
					break;
				}
			}
		}

		state.Exit();
		return result;
	}

	/// <inheritdoc/>
	internal override int GetHashCodeCore(TArray value, ref HashState state)
	{
		if (!state.TryEnter(value!, out int memoized))
		{
			return memoized;
		}

		Array array = (Array)(object)value!;
		int hash = Seed;
		for (int dimension = 0; dimension < rank; dimension++)
		{
			hash = HashCombiner.Combine(hash, array.GetLength(dimension));
		}

		foreach (TElement element in getEnumerable(value))
		{
			hash = HashCombiner.Combine(hash, elementComparer.GetHashCodeWithNullHandling(element, ref state));
		}

		hash = HashCombiner.Finalize(hash);
		state.Exit(value!, hash);
		return hash;
	}
}
