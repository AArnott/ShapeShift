// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Equality;

/// <summary>
/// Compares set-like collections as multisets, so that enumeration order is irrelevant.
/// </summary>
/// <typeparam name="TEnumerable">The collection type.</typeparam>
/// <typeparam name="TElement">The element type.</typeparam>
/// <param name="getEnumerable">Exposes the collection's elements.</param>
/// <param name="elementComparer">The structural comparer for elements.</param>
/// <remarks>
/// The set's own comparer is not consulted: membership is decided by structural element equality.
/// </remarks>
internal sealed class SetComparer<TEnumerable, TElement>(
	Func<TEnumerable, IEnumerable<TElement>> getEnumerable,
	StructuralComparer<TElement> elementComparer) : StructuralComparer<TEnumerable>
{
	private const int Seed = 0x53455400;

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

		bool result = this.MultisetsEqual(getEnumerable(x), getEnumerable(y));

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
		int count = 0;
		foreach (TElement element in getEnumerable(value))
		{
			hash = HashCombiner.CombineUnordered(hash, HashCombiner.Finalize(elementComparer.GetHashCodeWithNullHandling(element, ref state)));
			count++;
		}

		hash = HashCombiner.Finalize(HashCombiner.Combine(hash, count));
		if (IsReferenceType)
		{
			state.Exit(value!, hash);
		}

		return hash;
	}

	private bool MultisetsEqual(IEnumerable<TElement> left, IEnumerable<TElement> right)
	{
		if (left is IReadOnlyCollection<TElement> leftCollection &&
			right is IReadOnlyCollection<TElement> rightCollection &&
			leftCollection.Count != rightCollection.Count)
		{
			return false;
		}

#pragma warning disable CS8714 // The type cannot be used as a type parameter in the generic type or method. Nullability does not match 'notnull' constraint.
		Dictionary<TElement, int> counts = new(elementComparer);
#pragma warning restore CS8714
		int nullCount = 0;
		int leftCount = 0;
		foreach (TElement element in left)
		{
			leftCount++;
			if (element is null)
			{
				nullCount++;
			}
			else
			{
				counts.TryGetValue(element, out int existing);
				counts[element] = existing + 1;
			}
		}

		int rightCount = 0;
		foreach (TElement element in right)
		{
			rightCount++;
			if (rightCount > leftCount)
			{
				return false;
			}

			if (element is null)
			{
				if (--nullCount < 0)
				{
					return false;
				}
			}
			else
			{
				if (!counts.TryGetValue(element, out int existing) || existing == 0)
				{
					return false;
				}

				counts[element] = existing - 1;
			}
		}

		return leftCount == rightCount;
	}
}
