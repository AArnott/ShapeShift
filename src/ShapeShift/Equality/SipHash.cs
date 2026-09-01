// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Numerics;

namespace ShapeShift.Equality;

/// <summary>
/// An implementation of the SipHash-2-4 keyed pseudorandom function.
/// </summary>
/// <remarks>
/// <para>
/// SipHash is designed to make it computationally infeasible for an attacker who does not
/// know the key to construct inputs whose hash codes collide. It is <em>not</em> a message
/// authentication code for adversaries who can observe many outputs, and it is not a
/// cryptographic hash function. It is used here only to protect hash based collections from
/// algorithmic complexity attacks.
/// </para>
/// <para>
/// See <see href="https://131002.net/siphash/">the SipHash specification</see> for details.
/// </para>
/// </remarks>
internal static class SipHash
{
	/// <summary>
	/// Computes the 64-bit SipHash-2-4 of a byte sequence.
	/// </summary>
	/// <param name="data">The message to hash.</param>
	/// <param name="k0">The first half of the 128-bit key.</param>
	/// <param name="k1">The second half of the 128-bit key.</param>
	/// <returns>The 64-bit hash.</returns>
	internal static ulong Compute(ReadOnlySpan<byte> data, ulong k0, ulong k1)
	{
		unchecked
		{
			ulong v0 = k0 ^ 0x736f6d6570736575UL;
			ulong v1 = k1 ^ 0x646f72616e646f6dUL;
			ulong v2 = k0 ^ 0x6c7967656e657261UL;
			ulong v3 = k1 ^ 0x7465646279746573UL;

			int wholeWords = data.Length & ~7;
			for (int offset = 0; offset < wholeWords; offset += 8)
			{
				ulong m = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(offset, 8));
				v3 ^= m;
				Round(ref v0, ref v1, ref v2, ref v3);
				Round(ref v0, ref v1, ref v2, ref v3);
				v0 ^= m;
			}

			ulong last = (ulong)(data.Length & 0xff) << 56;
			ReadOnlySpan<byte> tail = data[wholeWords..];
			for (int i = tail.Length - 1; i >= 0; i--)
			{
				last |= (ulong)tail[i] << (i * 8);
			}

			v3 ^= last;
			Round(ref v0, ref v1, ref v2, ref v3);
			Round(ref v0, ref v1, ref v2, ref v3);
			v0 ^= last;

			v2 ^= 0xff;
			Round(ref v0, ref v1, ref v2, ref v3);
			Round(ref v0, ref v1, ref v2, ref v3);
			Round(ref v0, ref v1, ref v2, ref v3);
			Round(ref v0, ref v1, ref v2, ref v3);

			return v0 ^ v1 ^ v2 ^ v3;
		}
	}

	/// <summary>
	/// Folds a 64-bit hash into the 32-bit value required by <see cref="object.GetHashCode"/>.
	/// </summary>
	/// <param name="hash">The 64-bit hash.</param>
	/// <returns>A 32-bit hash code.</returns>
	internal static int Fold(ulong hash) => unchecked((int)(hash ^ (hash >> 32)));

	private static void Round(ref ulong v0, ref ulong v1, ref ulong v2, ref ulong v3)
	{
		unchecked
		{
			v0 += v1;
			v1 = BitOperations.RotateLeft(v1, 13);
			v1 ^= v0;
			v0 = BitOperations.RotateLeft(v0, 32);

			v2 += v3;
			v3 = BitOperations.RotateLeft(v3, 16);
			v3 ^= v2;

			v0 += v3;
			v3 = BitOperations.RotateLeft(v3, 21);
			v3 ^= v0;

			v2 += v1;
			v1 = BitOperations.RotateLeft(v1, 17);
			v1 ^= v2;
			v2 = BitOperations.RotateLeft(v2, 32);
		}
	}
}
