// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Equality;

/// <summary>
/// Compares sequences element by element, in order.
/// </summary>
/// <typeparam name="TEnumerable">The sequence type.</typeparam>
/// <typeparam name="TElement">The element type.</typeparam>
/// <param name="getEnumerable">Exposes the sequence's elements.</param>
/// <param name="elementComparer">The structural comparer for elements.</param>
internal sealed class SequenceComparer<TEnumerable, TElement>(
	Func<TEnumerable, IEnumerable<TElement>> getEnumerable,
	StructuralComparer<TElement> elementComparer) : StructuralComparer<TEnumerable>
{
	private const int Seed = 0x53455100;

	/// <inheritdoc/>
	internal override bool EqualsCore(TEnumerable x, TEnumerable y, ref ComparisonState state)
	{
		if (IsReferenceType)
		{
			if (ReferenceEquals(x, y))
			{
				return true;
			}

			if (state.EnterOrAssumeEqual(x!, y!))
			{
				state.Exit();
				return true;
			}
		}

		bool result = this.SequencesEqual(getEnumerable(x), getEnumerable(y), ref state);

		if (IsReferenceType)
		{
			state.Exit();
		}

		return result;
	}

	/// <inheritdoc/>
	internal override int GetHashCodeCore(TEnumerable value, ref HashState state)
	{
		if (IsReferenceType && !state.TryEnter(value!, out int memoized))
		{
			return memoized;
		}

		int hash = Seed;
		foreach (TElement element in getEnumerable(value))
		{
			hash = HashCombiner.Combine(hash, elementComparer.GetHashCodeWithNullHandling(element, ref state));
		}

		hash = HashCombiner.Finalize(hash);
		if (IsReferenceType)
		{
			state.Exit(value!, hash);
		}

		return hash;
	}

	private bool SequencesEqual(IEnumerable<TElement> left, IEnumerable<TElement> right, ref ComparisonState state)
	{
		if (left is IReadOnlyCollection<TElement> leftCollection &&
			right is IReadOnlyCollection<TElement> rightCollection &&
			leftCollection.Count != rightCollection.Count)
		{
			return false;
		}

		using IEnumerator<TElement> leftEnumerator = left.GetEnumerator();
		using IEnumerator<TElement> rightEnumerator = right.GetEnumerator();
		while (leftEnumerator.MoveNext())
		{
			if (!rightEnumerator.MoveNext() ||
				!elementComparer.EqualsWithNullHandling(leftEnumerator.Current, rightEnumerator.Current, ref state))
			{
				return false;
			}
		}

		return !rightEnumerator.MoveNext();
	}
}
