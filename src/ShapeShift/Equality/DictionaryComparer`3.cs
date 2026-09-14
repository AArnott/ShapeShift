// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Equality;

/// <summary>
/// Compares dictionaries entry by entry without regard to enumeration order.
/// </summary>
/// <typeparam name="TDictionary">The dictionary type.</typeparam>
/// <typeparam name="TKey">The key type.</typeparam>
/// <typeparam name="TValue">The value type.</typeparam>
/// <param name="getDictionary">Exposes the dictionary's entries.</param>
/// <param name="keyComparer">The structural comparer for keys.</param>
/// <param name="valueComparer">The structural comparer for values.</param>
/// <remarks>
/// <para>
/// The dictionary's own key comparer is deliberately <em>not</em> consulted. Two dictionaries are
/// equal when they contain the same set of structurally equal key/value pairs, which makes equality
/// independent of how each instance happens to be configured.
/// </para>
/// <para>
/// When a dictionary contains multiple keys that are structurally equal to each other (possible when
/// the dictionary's own comparer is coarser than structural equality, such as
/// <see cref="StringComparer.OrdinalIgnoreCase"/>), entries are matched greedily.
/// </para>
/// </remarks>
internal sealed class DictionaryComparer<TDictionary, TKey, TValue>(
	Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getDictionary,
	StructuralComparer<TKey> keyComparer,
	StructuralComparer<TValue> valueComparer) : StructuralComparer<TDictionary>
	where TKey : notnull
{
	private const int Seed = 0x4D415000;

	/// <inheritdoc/>
	internal override bool EqualsCore(TDictionary x, TDictionary y, ref ComparisonState state)
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

		bool result = this.EntriesEqual(getDictionary(x), getDictionary(y), ref state);

		if (IsReferenceType)
		{
			state.Exit();
		}

		return result;
	}

	/// <inheritdoc/>
	internal override int GetHashCodeCore(TDictionary value, ref HashState state)
	{
		if (IsReferenceType && !state.TryEnter(value!, out int memoized))
		{
			return memoized;
		}

		int hash = Seed;
		int count = 0;
		foreach (KeyValuePair<TKey, TValue> entry in getDictionary(value))
		{
			int entryHash = HashCombiner.Combine(keyComparer.GetHashCodeWithNullHandling(entry.Key, ref state), valueComparer.GetHashCodeWithNullHandling(entry.Value, ref state));
			hash = HashCombiner.CombineUnordered(hash, HashCombiner.Finalize(entryHash));
			count++;
		}

		hash = HashCombiner.Finalize(HashCombiner.Combine(hash, count));
		if (IsReferenceType)
		{
			state.Exit(value!, hash);
		}

		return hash;
	}

	private bool EntriesEqual(IReadOnlyDictionary<TKey, TValue> left, IReadOnlyDictionary<TKey, TValue> right, ref ComparisonState state)
	{
		if (left.Count != right.Count)
		{
			return false;
		}

		if (left.Count == 0)
		{
			return true;
		}

		List<KeyValuePair<TKey, TValue>> candidates = new(right.Count);
		Dictionary<TKey, int> index = new(right.Count, keyComparer);
		bool structurallyDuplicateKeys = false;
		foreach (KeyValuePair<TKey, TValue> entry in right)
		{
			if (!index.TryAdd(entry.Key, candidates.Count))
			{
				structurallyDuplicateKeys = true;
			}

			candidates.Add(entry);
		}

		bool[] consumed = new bool[candidates.Count];
		foreach (KeyValuePair<TKey, TValue> entry in left)
		{
			if (!this.TryConsumeMatch(entry, candidates, index, consumed, structurallyDuplicateKeys, ref state))
			{
				return false;
			}
		}

		return true;
	}

	private bool TryConsumeMatch(
		KeyValuePair<TKey, TValue> entry,
		List<KeyValuePair<TKey, TValue>> candidates,
		Dictionary<TKey, int> index,
		bool[] consumed,
		bool structurallyDuplicateKeys,
		ref ComparisonState state)
	{
		if (!structurallyDuplicateKeys)
		{
			if (!index.TryGetValue(entry.Key, out int position) || consumed[position])
			{
				return false;
			}

			if (!valueComparer.EqualsWithNullHandling(entry.Value, candidates[position].Value, ref state))
			{
				return false;
			}

			consumed[position] = true;
			return true;
		}

		for (int i = 0; i < candidates.Count; i++)
		{
			if (consumed[i] ||
				!keyComparer.EqualsWithNullHandling(entry.Key, candidates[i].Key, ref state) ||
				!valueComparer.EqualsWithNullHandling(entry.Value, candidates[i].Value, ref state))
			{
				continue;
			}

			consumed[i] = true;
			return true;
		}

		return false;
	}
}
