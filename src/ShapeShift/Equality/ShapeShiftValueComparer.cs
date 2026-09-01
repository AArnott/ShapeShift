// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Equality;

/// <summary>
/// Compares <see cref="ShapeShiftValue"/> graphs structurally, including the contents of
/// <see cref="ShapeShiftArray"/>, <see cref="ShapeShiftMap"/> and <see cref="ShapeShiftBinary"/> nodes,
/// which the compiler generated record equality compares by reference.
/// </summary>
/// <param name="policy">The hashing policy.</param>
/// <remarks>
/// <para>
/// Numeric nodes are compared strictly by node kind: a <see cref="ShapeShiftInteger"/> is never equal
/// to a <see cref="ShapeShiftUnsignedInteger"/>, <see cref="ShapeShiftFloat"/> or
/// <see cref="ShapeShiftDecimal"/> even when they denote the same number. This preserves the
/// distinctions the data format itself preserves.
/// </para>
/// <para>
/// Map keys are matched with ordinal string equality regardless of the comparer that the underlying
/// dictionary instance happens to use, and map entry order is irrelevant.
/// </para>
/// </remarks>
internal sealed class ShapeShiftValueComparer(HashingPolicy policy) : StructuralComparer<ShapeShiftValue>
{
	private const int NullSeed = 0x53564E00;
	private const int BooleanSeed = 0x53564200;
	private const int IntegerSeed = 0x53564900;
	private const int UnsignedSeed = 0x53565500;
	private const int BigIntegerSeed = 0x53564700;
	private const int FloatSeed = 0x53564600;
	private const int DecimalSeed = 0x53564400;
	private const int StringSeed = 0x53565300;
	private const int BinarySeed = 0x53565900;
	private const int ArraySeed = 0x53564100;
	private const int MapSeed = 0x53564D00;

	/// <inheritdoc/>
	internal override bool EqualsCore(ShapeShiftValue x, ShapeShiftValue y, ref ComparisonState state)
	{
		if (ReferenceEquals(x, y))
		{
			return true;
		}

		switch (x)
		{
			case ShapeShiftNull:
				return y is ShapeShiftNull;
			case ShapeShiftBoolean xBoolean:
				return y is ShapeShiftBoolean yBoolean && xBoolean.Value == yBoolean.Value;
			case ShapeShiftInteger xInteger:
				return y is ShapeShiftInteger yInteger && xInteger.Value == yInteger.Value;
			case ShapeShiftUnsignedInteger xUnsigned:
				return y is ShapeShiftUnsignedInteger yUnsigned && xUnsigned.Value == yUnsigned.Value;
			case ShapeShiftBigInteger xBig:
				return y is ShapeShiftBigInteger yBig && xBig.Value == yBig.Value;
			case ShapeShiftFloat xFloat:
				return y is ShapeShiftFloat yFloat && xFloat.Value.Equals(yFloat.Value);
			case ShapeShiftDecimal xDecimal:
				return y is ShapeShiftDecimal yDecimal && xDecimal.Value == yDecimal.Value;
			case ShapeShiftString xString:
				return y is ShapeShiftString yString && string.Equals(xString.Value, yString.Value, StringComparison.Ordinal);
			case ShapeShiftBinary xBinary:
				return y is ShapeShiftBinary yBinary && xBinary.Value.Span.SequenceEqual(yBinary.Value.Span);
			case ShapeShiftArray xArray:
				return y is ShapeShiftArray yArray && this.ArraysEqual(xArray, yArray, ref state);
			case ShapeShiftMap xMap:
				return y is ShapeShiftMap yMap && this.MapsEqual(xMap, yMap, ref state);
			default:
				return x.Equals(y);
		}
	}

	/// <inheritdoc/>
	internal override int GetHashCodeCore(ShapeShiftValue value, ref HashState state)
	{
		switch (value)
		{
			case ShapeShiftNull:
				return NullSeed;
			case ShapeShiftBoolean booleanNode:
				return HashCombiner.Finalize(HashCombiner.Combine(BooleanSeed, booleanNode.Value ? 1 : 0));
			case ShapeShiftInteger integerNode:
				return HashCombiner.Finalize(HashCombiner.Combine(IntegerSeed, policy.HashOpaque(integerNode.Value.GetHashCode())));
			case ShapeShiftUnsignedInteger unsignedNode:
				return HashCombiner.Finalize(HashCombiner.Combine(UnsignedSeed, policy.HashOpaque(unsignedNode.Value.GetHashCode())));
			case ShapeShiftBigInteger bigNode:
				return HashCombiner.Finalize(HashCombiner.Combine(BigIntegerSeed, policy.HashOpaque(bigNode.Value.GetHashCode())));
			case ShapeShiftFloat floatNode:
				return HashCombiner.Finalize(HashCombiner.Combine(FloatSeed, policy.HashOpaque(floatNode.Value.GetHashCode())));
			case ShapeShiftDecimal decimalNode:
				return HashCombiner.Finalize(HashCombiner.Combine(DecimalSeed, policy.HashOpaque(decimalNode.Value.GetHashCode())));
			case ShapeShiftString stringNode:
				return HashCombiner.Finalize(HashCombiner.Combine(StringSeed, policy.HashString(stringNode.Value)));
			case ShapeShiftBinary binaryNode:
				return HashCombiner.Finalize(HashCombiner.Combine(BinarySeed, policy.HashBytes(binaryNode.Value.Span)));
			case ShapeShiftArray arrayNode:
				return this.HashArray(arrayNode, ref state);
			case ShapeShiftMap mapNode:
				return this.HashMap(mapNode, ref state);
			default:
				return policy.HashOpaque(value.GetHashCode());
		}
	}

	private bool ArraysEqual(ShapeShiftArray x, ShapeShiftArray y, ref ComparisonState state)
	{
		if (state.EnterOrAssumeEqual(x, y))
		{
			state.Exit();
			return true;
		}

		bool result = x.Items.Count == y.Items.Count;
		if (result)
		{
			for (int i = 0; i < x.Items.Count; i++)
			{
				if (!this.EqualsWithNullHandling(x.Items[i], y.Items[i], ref state))
				{
					result = false;
					break;
				}
			}
		}

		state.Exit();
		return result;
	}

	private bool MapsEqual(ShapeShiftMap x, ShapeShiftMap y, ref ComparisonState state)
	{
		if (state.EnterOrAssumeEqual(x, y))
		{
			state.Exit();
			return true;
		}

		bool result = x.Properties.Count == y.Properties.Count;
		if (result)
		{
			Dictionary<string, ShapeShiftValue> right = new(y.Properties.Count, StringComparer.Ordinal);
			foreach (KeyValuePair<string, ShapeShiftValue> entry in y.Properties)
			{
				right[entry.Key] = entry.Value;
			}

			foreach (KeyValuePair<string, ShapeShiftValue> entry in x.Properties)
			{
				if (!right.TryGetValue(entry.Key, out ShapeShiftValue? candidate) ||
					!this.EqualsWithNullHandling(entry.Value, candidate, ref state))
				{
					result = false;
					break;
				}
			}
		}

		state.Exit();
		return result;
	}

	private int HashArray(ShapeShiftArray node, ref HashState state)
	{
		if (!state.TryEnter(node, out int memoized))
		{
			return memoized;
		}

		int hash = ArraySeed;
		foreach (ShapeShiftValue item in node.Items)
		{
			hash = HashCombiner.Combine(hash, this.GetHashCodeWithNullHandling(item, ref state));
		}

		hash = HashCombiner.Finalize(hash);
		state.Exit(node, hash);
		return hash;
	}

	private int HashMap(ShapeShiftMap node, ref HashState state)
	{
		if (!state.TryEnter(node, out int memoized))
		{
			return memoized;
		}

		int hash = MapSeed;
		int count = 0;
		foreach (KeyValuePair<string, ShapeShiftValue> entry in node.Properties)
		{
			int entryHash = HashCombiner.Combine(policy.HashString(entry.Key), this.GetHashCodeWithNullHandling(entry.Value, ref state));
			hash = HashCombiner.CombineUnordered(hash, HashCombiner.Finalize(entryHash));
			count++;
		}

		hash = HashCombiner.Finalize(HashCombiner.Combine(hash, count));
		state.Exit(node, hash);
		return hash;
	}
}
