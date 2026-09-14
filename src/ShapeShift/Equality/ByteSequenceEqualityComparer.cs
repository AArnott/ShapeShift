// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Equality;

/// <summary>
/// Compares byte arrays by content.
/// </summary>
internal sealed class ByteSequenceEqualityComparer : IEqualityComparer<byte[]>
{
	/// <summary>
	/// The singleton instance.
	/// </summary>
	internal static readonly ByteSequenceEqualityComparer Instance = new();

	private ByteSequenceEqualityComparer()
	{
	}

	/// <inheritdoc/>
	public bool Equals(byte[]? x, byte[]? y)
		=> x is null ? y is null : y is not null && x.AsSpan().SequenceEqual(y);

	/// <inheritdoc/>
	public int GetHashCode(byte[] obj) => HashingPolicy.Deterministic.HashBytes(obj);
}
