// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Equality;

/// <summary>
/// Determines how the hash codes of leaf (indivisible) values are computed.
/// </summary>
/// <remarks>
/// Only leaves differ between policies. Aggregation of child hash codes is always performed by
/// <see cref="HashCombiner"/>, which keeps the shape of the algorithm identical so that the two
/// policies agree on <em>equality</em> in every case and differ only in hash quality.
/// </remarks>
internal abstract class HashingPolicy
{
	/// <summary>
	/// Gets the policy that produces stable, non-randomized hash codes.
	/// </summary>
	internal static HashingPolicy Deterministic { get; } = new DeterministicHashingPolicy();

	/// <summary>
	/// Gets the policy that produces process-randomized, collision resistant hash codes.
	/// </summary>
	internal static HashingPolicy CollisionResistant { get; } = new CollisionResistantHashingPolicy();

	/// <summary>
	/// Gets a value indicating whether this policy hashes the full content of well-known leaf types
	/// instead of relying on <see cref="object.GetHashCode"/>.
	/// </summary>
	protected abstract bool UsesContentHashing { get; }

	/// <summary>
	/// Computes the hash code of a string.
	/// </summary>
	/// <param name="value">The string to hash.</param>
	/// <returns>The hash code.</returns>
	internal abstract int HashString(string value);

	/// <summary>
	/// Computes the hash code of a byte sequence.
	/// </summary>
	/// <param name="value">The bytes to hash.</param>
	/// <returns>The hash code.</returns>
	internal abstract int HashBytes(ReadOnlySpan<byte> value);

	/// <summary>
	/// Post-processes a hash code obtained from an opaque leaf's own <see cref="object.GetHashCode"/>.
	/// </summary>
	/// <param name="hash">The hash code reported by the leaf type.</param>
	/// <returns>The hash code to use.</returns>
	internal abstract int HashOpaque(int hash);

	/// <summary>
	/// Creates a comparer for a leaf type, which is compared with its own equality semantics
	/// and hashed according to this policy.
	/// </summary>
	/// <typeparam name="T">The leaf type.</typeparam>
	/// <param name="equality">The equality semantics for the leaf type.</param>
	/// <returns>The comparer.</returns>
	internal StructuralComparer<T> CreateLeafComparer<T>(IEqualityComparer<T> equality)
	{
		HashingPolicy policy = this;
		if (typeof(T) == typeof(string))
		{
			return new LeafComparer<T>(equality, value => policy.HashString((string)(object)value!));
		}

		if (typeof(T) == typeof(byte[]))
		{
			return new LeafComparer<T>(equality, value => policy.HashBytes((byte[])(object)value!));
		}

		if (this.UsesContentHashing && LeafEncoders.TryGet<T>() is { } encoder)
		{
			return new LeafComparer<T>(equality, value =>
			{
				Span<byte> buffer = stackalloc byte[LeafEncoders.MaxEncodedLength];
				int length = encoder(value, buffer);
				return policy.HashBytes(buffer[..length]);
			});
		}

		return new LeafComparer<T>(equality, value => policy.HashOpaque(equality.GetHashCode(value!)));
	}
}
