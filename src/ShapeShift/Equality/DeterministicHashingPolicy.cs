// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Equality;

/// <summary>
/// A hashing policy that never introduces randomization, so that hash codes are
/// reproducible across runs and processes for every leaf type whose own hash code is.
/// </summary>
/// <remarks>
/// Strings are hashed with FNV-1a over their UTF-16 code units rather than
/// <see cref="string.GetHashCode()"/>, which the .NET runtime randomizes per process.
/// </remarks>
internal sealed class DeterministicHashingPolicy : HashingPolicy
{
	private const uint FnvOffsetBasis = 2166136261;
	private const uint FnvPrime = 16777619;

	/// <inheritdoc/>
	protected override bool UsesContentHashing => false;

	/// <inheritdoc/>
	internal override int HashString(string value)
	{
		unchecked
		{
			uint hash = FnvOffsetBasis;
			foreach (char ch in value)
			{
				hash = (hash ^ (byte)ch) * FnvPrime;
				hash = (hash ^ (byte)(ch >> 8)) * FnvPrime;
			}

			return (int)hash;
		}
	}

	/// <inheritdoc/>
	internal override int HashBytes(ReadOnlySpan<byte> value)
	{
		unchecked
		{
			uint hash = FnvOffsetBasis;
			foreach (byte b in value)
			{
				hash = (hash ^ b) * FnvPrime;
			}

			return (int)hash;
		}
	}

	/// <inheritdoc/>
	internal override int HashOpaque(int hash) => hash;
}
