// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Equality;

/// <summary>
/// Deterministic helpers for combining the hash codes of a node's children.
/// </summary>
/// <remarks>
/// These helpers are intentionally free of process randomization so that the default
/// hashing behavior is stable within and across processes for all leaf types whose own
/// hash codes are stable. Collision resistance is introduced by
/// <see cref="HashingPolicy"/> at the leaves rather than during aggregation.
/// </remarks>
internal static class HashCombiner
{
	/// <summary>
	/// The hash code reported for a <see langword="null"/> value.
	/// </summary>
	internal const int NullHash = 0;

	/// <summary>
	/// The hash code reported for any value whose graph contains a reference cycle.
	/// </summary>
	internal const int CyclicHash = 0xC0FFEE;

	/// <summary>
	/// Combines a child hash code into an accumulator in an order sensitive way.
	/// </summary>
	/// <param name="accumulator">The accumulated hash code.</param>
	/// <param name="value">The child hash code.</param>
	/// <returns>The updated accumulator.</returns>
	internal static int Combine(int accumulator, int value) => unchecked((accumulator * 31) + value);

	/// <summary>
	/// Combines an entry hash code into an accumulator in an order insensitive way.
	/// </summary>
	/// <param name="accumulator">The accumulated hash code.</param>
	/// <param name="value">The entry hash code.</param>
	/// <returns>The updated accumulator.</returns>
	/// <remarks>
	/// Addition is commutative and associative, which is what makes the hash code of a
	/// dictionary or set independent of enumeration order.
	/// </remarks>
	internal static int CombineUnordered(int accumulator, int value) => unchecked(accumulator + value);

	/// <summary>
	/// Applies a final avalanche step to a combined hash code.
	/// </summary>
	/// <param name="accumulator">The accumulated hash code.</param>
	/// <returns>The finalized hash code.</returns>
	internal static int Finalize(int accumulator)
	{
		unchecked
		{
			uint h = (uint)accumulator;
			h ^= h >> 16;
			h *= 0x7feb352d;
			h ^= h >> 15;
			h *= 0x846ca68b;
			h ^= h >> 16;
			return (int)h;
		}
	}
}
