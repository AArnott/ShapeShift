// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Numerics;

namespace ShapeShift.Equality;

/// <summary>
/// Determines how the hash codes of leaf (indivisible) values are computed.
/// </summary>
/// <remarks>
/// <para>
/// The deterministic policy aggregates child hash codes with <see cref="HashCombiner"/>.
/// The collision-resistant policy overrides that aggregation, so the hash of a dynamic array or map
/// depends on the process key and not only on the public combiner. Equality is the same either way.
/// </para>
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
	/// Computes the hash code of a Boolean value.
	/// </summary>
	/// <param name="value">The Boolean value.</param>
	/// <returns>The hash code.</returns>
	internal virtual int HashBoolean(bool value) => value ? 1 : 0;

	/// <summary>
	/// Combines a child hash code into an accumulator in an order sensitive way.
	/// </summary>
	/// <param name="accumulator">The accumulated hash code.</param>
	/// <param name="value">The child hash code.</param>
	/// <returns>The updated accumulator.</returns>
	internal virtual int Combine(int accumulator, int value) => HashCombiner.Combine(accumulator, value);

	/// <summary>
	/// Combines an entry hash code into an accumulator in an order insensitive way.
	/// </summary>
	/// <param name="accumulator">The accumulated hash code.</param>
	/// <param name="value">The entry hash code.</param>
	/// <returns>The updated accumulator.</returns>
	internal virtual int CombineUnordered(int accumulator, int value) => HashCombiner.CombineUnordered(accumulator, value);

	/// <summary>
	/// Applies the policy's final mix to a combined hash code.
	/// </summary>
	/// <param name="accumulator">The accumulated hash code.</param>
	/// <returns>The finalized hash code.</returns>
	internal virtual int FinalizeHash(int accumulator) => HashCombiner.Finalize(accumulator);

	/// <summary>
	/// Hashes a signed integer over its full width when this policy hashes leaf content.
	/// </summary>
	/// <param name="value">The integer.</param>
	/// <returns>The hash code.</returns>
	internal int HashInt64(long value) => this.HashUInt64(unchecked((ulong)value));

	/// <summary>
	/// Hashes an unsigned integer over its full width when this policy hashes leaf content.
	/// </summary>
	/// <param name="value">The integer.</param>
	/// <returns>The hash code.</returns>
	/// <remarks>
	/// <see cref="ulong.GetHashCode"/> folds a value with <c>(int)value ^ (int)(value &gt;&gt; 32)</c>,
	/// so every integer of the form <c>(i &lt;&lt; 32) | i</c> shares one hash code. Content hashing
	/// uses all eight bytes instead.
	/// </remarks>
	internal int HashUInt64(ulong value)
	{
		if (!this.UsesContentHashing)
		{
			return this.HashOpaque(value.GetHashCode());
		}

		Span<byte> buffer = stackalloc byte[8];
		BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
		return this.HashBytes(buffer);
	}

	/// <summary>
	/// Hashes a floating-point value consistently with <see cref="double.Equals(double)"/>.
	/// </summary>
	/// <param name="value">The number.</param>
	/// <returns>The hash code.</returns>
	internal int HashDouble(double value)
	{
		if (!this.UsesContentHashing)
		{
			return this.HashOpaque(value.GetHashCode());
		}

		ulong bits = double.IsNaN(value)
			? 0xFFF8_0000_0000_0000UL
			: BitConverter.DoubleToUInt64Bits(value == 0d ? 0d : value);
		return this.HashUInt64(bits);
	}

	/// <summary>
	/// Hashes a <see cref="BigInteger"/> over its full little-endian encoding.
	/// </summary>
	/// <param name="value">The integer.</param>
	/// <returns>The hash code.</returns>
	internal int HashBigInteger(BigInteger value)
	{
		if (!this.UsesContentHashing)
		{
			return this.HashOpaque(value.GetHashCode());
		}

		int size = value.GetByteCount(isUnsigned: false);
		Span<byte> buffer = size <= 256 ? stackalloc byte[256] : new byte[size];
		value.TryWriteBytes(buffer, out int written, isUnsigned: false, isBigEndian: false);
		return this.HashBytes(buffer[..written]);
	}

	/// <summary>
	/// Hashes a <see cref="decimal"/> consistently with decimal equality, which ignores scale.
	/// </summary>
	/// <param name="value">The number.</param>
	/// <returns>The hash code.</returns>
	internal int HashDecimal(decimal value)
	{
		if (!this.UsesContentHashing)
		{
			return this.HashOpaque(value.GetHashCode());
		}

		int[] bits = decimal.GetBits(value);
		UInt128 significand = unchecked((uint)bits[0])
			| ((UInt128)unchecked((uint)bits[1]) << 32)
			| ((UInt128)unchecked((uint)bits[2]) << 64);
		int scale = (bits[3] >> 16) & 0xFF;
		bool negative = (bits[3] & unchecked((int)0x80000000)) != 0;
		while (scale > 0 && significand % 10 == 0)
		{
			significand /= 10;
			scale--;
		}

		if (significand == 0)
		{
			negative = false;
			scale = 0;
		}

		Span<byte> buffer = stackalloc byte[18];
		BinaryPrimitives.WriteUInt64LittleEndian(buffer, (ulong)significand);
		BinaryPrimitives.WriteUInt64LittleEndian(buffer[8..], (ulong)(significand >> 64));
		buffer[16] = (byte)scale;
		buffer[17] = negative ? (byte)1 : (byte)0;
		return this.HashBytes(buffer);
	}

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
