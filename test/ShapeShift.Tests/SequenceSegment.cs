// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;

namespace ShapeShift.Tests;

/// <summary>
/// A minimal <see cref="ReadOnlySequenceSegment{T}"/> implementation that lets tests build
/// multi-segment <see cref="ReadOnlySequence{T}"/> instances (simulating fragmented network
/// buffers) out of a series of small byte arrays.
/// </summary>
public sealed class SequenceSegment : ReadOnlySequenceSegment<byte>
{
	private SequenceSegment(ReadOnlyMemory<byte> memory, long runningIndex)
	{
		this.Memory = memory;
		this.RunningIndex = runningIndex;
	}

	/// <summary>
	/// Builds a <see cref="ReadOnlySequence{T}"/> of bytes that is fragmented into a separate
	/// segment for each of the given chunks, preserving their order.
	/// </summary>
	/// <param name="chunks">The byte chunks to chain together, in order.</param>
	/// <returns>A sequence spanning all the given chunks across independent memory segments.</returns>
	public static ReadOnlySequence<byte> Create(IEnumerable<byte[]> chunks)
	{
		SequenceSegment? first = null;
		SequenceSegment? last = null;
		foreach (byte[] chunk in chunks)
		{
			if (first is null)
			{
				first = last = new SequenceSegment(chunk, 0);
			}
			else
			{
				SequenceSegment next = new(chunk, last!.RunningIndex + last.Memory.Length);
				last!.Next = next;
				last = next;
			}
		}

		if (first is null)
		{
			return ReadOnlySequence<byte>.Empty;
		}

		return new ReadOnlySequence<byte>(first, 0, last!, last!.Memory.Length);
	}

	/// <summary>
	/// Splits <paramref name="data"/> into consecutive chunks of at most <paramref name="chunkSize"/>
	/// bytes each and builds a multi-segment <see cref="ReadOnlySequence{T}"/> of bytes out of them.
	/// </summary>
	/// <param name="data">The complete byte content.</param>
	/// <param name="chunkSize">The maximum number of bytes per segment.</param>
	/// <returns>A fragmented sequence equivalent to <paramref name="data"/>.</returns>
	public static ReadOnlySequence<byte> Chunk(byte[] data, int chunkSize)
	{
		List<byte[]> chunks = [];
		for (int i = 0; i < data.Length; i += chunkSize)
		{
			int length = Math.Min(chunkSize, data.Length - i);
			chunks.Add(data[i..(i + length)]);
		}

		return chunks.Count == 0 ? ReadOnlySequence<byte>.Empty : Create(chunks);
	}
}
