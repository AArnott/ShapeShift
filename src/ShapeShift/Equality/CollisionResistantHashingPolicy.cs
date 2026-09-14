// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;

namespace ShapeShift.Equality;

/// <summary>
/// A hashing policy that runs leaf content through SipHash-2-4 keyed with a
/// process-wide random key.
/// </summary>
/// <remarks>
/// <para>
/// The key is generated once per process from <see cref="RandomNumberGenerator"/>, so hash codes
/// differ between runs and must never be persisted or transmitted.
/// </para>
/// <para>
/// Well-known leaf types (strings, byte arrays, integers, enums, floating point numbers, dates,
/// times and GUIDs) are hashed over their full canonical content. Any other leaf type is hashed by
/// feeding its own 32-bit hash code through the keyed function, which randomizes the result but
/// cannot recover entropy that the leaf type already discarded.
/// </para>
/// </remarks>
internal sealed class CollisionResistantHashingPolicy : HashingPolicy
{
	private static readonly ulong Key0;
	private static readonly ulong Key1;

	static CollisionResistantHashingPolicy()
	{
		Span<byte> key = stackalloc byte[16];
		RandomNumberGenerator.Fill(key);
		Key0 = BitConverter.ToUInt64(key);
		Key1 = BitConverter.ToUInt64(key[8..]);
	}

	/// <inheritdoc/>
	protected override bool UsesContentHashing => true;

	/// <inheritdoc/>
	internal override int HashString(string value)
		=> this.HashBytes(MemoryMarshal.AsBytes(value.AsSpan()));

	/// <inheritdoc/>
	internal override int HashBytes(ReadOnlySpan<byte> value)
		=> SipHash.Fold(SipHash.Compute(value, Key0, Key1));

	/// <inheritdoc/>
	internal override int HashOpaque(int hash)
	{
		Span<byte> buffer = stackalloc byte[4];
		BitConverter.TryWriteBytes(buffer, hash);
		return this.HashBytes(buffer);
	}
}
