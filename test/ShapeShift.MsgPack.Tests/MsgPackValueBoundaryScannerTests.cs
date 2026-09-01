// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using ShapeShift.Tests;

namespace ShapeShift.MsgPack.Tests;

/// <summary>
/// Directly exercises <see cref="MsgPackValueBoundaryScanner"/>'s resumable, self-delimiting byte-code walk,
/// independent of the higher-level <see cref="PipeReaderExtensions.ReadValueAsync{T}"/> pump loop.
/// </summary>
public class MsgPackValueBoundaryScannerTests : TestBase
{
	/// <summary>
	/// Gets every named case, each a complete, self-contained MessagePack encoding of exactly one top-level value.
	/// </summary>
	public static IEnumerable<(string Name, byte[] Bytes)> ScalarAndContainerCases { get; } =
	[
		("nil", [0xc0]),
		("false", [0xc2]),
		("true", [0xc3]),
		("positive fixint (0)", [0x00]),
		("positive fixint (127)", [0x7f]),
		("negative fixint (-1)", [0xff]),
		("negative fixint (-32)", [0xe0]),
		("uint8", [0xcc, 0x2a]),
		("uint16", [0xcd, 0x01, 0x02]),
		("uint32", [0xce, 0x01, 0x02, 0x03, 0x04]),
		("uint64", [0xcf, 0, 0, 0, 0, 0, 0, 0, 1]),
		("int8", [0xd0, 0x80]),
		("int16", [0xd1, 0x80, 0x00]),
		("int32", [0xd2, 0x80, 0, 0, 0]),
		("int64", [0xd3, 0x80, 0, 0, 0, 0, 0, 0, 0]),
		("float32", [0xca, 0, 0, 0, 0]),
		("float64", [0xcb, 0, 0, 0, 0, 0, 0, 0, 0]),
		("fixstr", [0xa3, 0x61, 0x62, 0x63]),
		("str8", [0xd9, 0x02, 0x41, 0x42]),
		("str16", [0xda, 0x00, 0x02, 0x41, 0x42]),
		("str32", [0xdb, 0, 0, 0, 2, 0x41, 0x42]),
		("bin8", [0xc4, 0x02, 0x01, 0x02]),
		("bin16", [0xc5, 0x00, 0x02, 0x01, 0x02]),
		("bin32", [0xc6, 0, 0, 0, 2, 0x01, 0x02]),
		("empty fixarray", [0x90]),
		("fixarray with one element", [0x91, 0xc0]),
		("array16", [0xdc, 0x00, 0x01, 0xc0]),
		("array32", [0xdd, 0, 0, 0, 1, 0xc0]),
		("empty fixmap", [0x80]),
		("fixmap with one pair", [0x81, 0x01, 0x02]),
		("map16", [0xde, 0x00, 0x01, 0x01, 0x02]),
		("map32", [0xdf, 0, 0, 0, 1, 0x01, 0x02]),
		("fixext1", [0xd4, 0x01, 0xaa]),
		("fixext2", [0xd5, 0x01, 0xaa, 0xbb]),
		("fixext4", [0xd6, 0x01, 0, 0, 0, 0]),
		("fixext8", [0xd7, 0x01, 0, 0, 0, 0, 0, 0, 0, 0]),
		("fixext16", [0xd8, 0x01, .. new byte[16]]),
		("ext8", [0xc7, 0x02, 0x01, 0xaa, 0xbb]),
		("ext16", [0xc8, 0x00, 0x02, 0x01, 0xaa, 0xbb]),
		("ext32", [0xc9, 0, 0, 0, 2, 0x01, 0xaa, 0xbb]),
		("nested map of arrays", [0x81, 0xa1, 0x6b, 0x92, 0x01, 0x02]),
	];

	[Test]
	public async Task RecognizesEachCompleteEncoding()
	{
		foreach ((string name, byte[] bytes) in ScalarAndContainerCases)
		{
			MsgPackValueBoundaryScanner scanner = new();
			ReadOnlySequence<byte> buffer = new(bytes);
			bool found = scanner.TryScan(buffer, isFinalBlock: true, out SequencePosition end, out _);

			await Assert.That(found).IsTrue();
			await Assert.That(buffer.GetOffset(end)).IsEqualTo((long)bytes.Length);
		}
	}

	[Test]
	public async Task RequestsMoreDataWhenTruncated()
	{
		foreach ((string name, byte[] bytes) in ScalarAndContainerCases)
		{
			if (bytes.Length <= 1)
			{
				// A single-byte encoding cannot be truncated and remain non-empty.
				continue;
			}

			MsgPackValueBoundaryScanner scanner = new();
			ReadOnlySequence<byte> truncated = new(bytes[..^1]);
			bool found = scanner.TryScan(truncated, isFinalBlock: false, out _, out _);

			await Assert.That(found).IsFalse();
		}
	}

	[Test]
	public async Task FeedingOneByteAtATime_EventuallyRecognizesNestedStructure()
	{
		// { "a": [1, 2, 3], "b": "hello world" } equivalent, hand-encoded:
		// fixmap(2) { fixstr("a"): fixarray(3)[1,2,3], fixstr("b"): fixstr("hello world") }
		byte[] value =
		[
			0x82, // fixmap, 2 pairs
			0xa1, 0x61, // "a"
			0x93, 0x01, 0x02, 0x03, // [1, 2, 3]
			0xa1, 0x62, // "b"
			0xab, // fixstr, length 11
			.. "hello world"u8,
		];

		MsgPackValueBoundaryScanner scanner = new();
		bool found = false;
		ReadOnlySequence<byte> lastBuffer = default;
		SequencePosition end = default;
		int examinedOffset = 0;
		for (int i = 1; i <= value.Length; i++)
		{
			ReadOnlySequence<byte> partial = new(value[examinedOffset..i]);
			bool isFinalBlock = i == value.Length;
			found = scanner.TryScan(partial, isFinalBlock, out end, out SequencePosition examined);
			examinedOffset += (int)partial.Slice(partial.Start, examined).Length;
			lastBuffer = partial;
			if (i < value.Length)
			{
				await Assert.That(found).IsFalse();
			}
		}

		await Assert.That(found).IsTrue();
		await Assert.That(lastBuffer.Slice(end).IsEmpty).IsTrue();
	}

	[Test]
	public async Task FeedingViaMultiSegmentSequence_RecognizesLargeArray()
	{
		// A 500-element array of small unsigned integers, fed through a chain of 7-byte memory segments
		// (each independent, simulating fragmented network buffers) to validate that scanning resumes
		// correctly across segment boundaries as well as across separate TryScan calls.
		List<byte> encoded = [0xdc, 0x01, 0xf4]; // array16, count = 500
		for (int i = 0; i < 500; i++)
		{
			encoded.Add(0xcc); // uint8
			encoded.Add((byte)(i % 256));
		}

		byte[] value = [.. encoded];
		MsgPackValueBoundaryScanner scanner = new();
		bool found = false;
		ReadOnlySequence<byte> lastBuffer = default;
		SequencePosition end = default;
		int fed = 0;
		int examinedOffset = 0;
		while (fed < value.Length)
		{
			fed = Math.Min(fed + 7, value.Length);
			ReadOnlySequence<byte> partial = SequenceSegment.Chunk(value[examinedOffset..fed], chunkSize: 7);
			found = scanner.TryScan(partial, isFinalBlock: fed == value.Length, out end, out SequencePosition examined);
			examinedOffset += (int)partial.Slice(partial.Start, examined).Length;
			lastBuffer = partial;
		}

		await Assert.That(found).IsTrue();
		await Assert.That(lastBuffer.Slice(end).IsEmpty).IsTrue();
	}

	[Test]
	public async Task ReservedCode_ThrowsDecoderException()
	{
		MsgPackValueBoundaryScanner scanner = new();
		ReadOnlySequence<byte> buffer = new(new byte[] { 0xc1 });

		void Act() => scanner.TryScan(buffer, isFinalBlock: true, out _, out _);

		await Assert.That(Act).Throws<DecoderException>();
	}

	[Test]
	public async Task ReservedCode_NestedInsideContainer_ThrowsDecoderException()
	{
		MsgPackValueBoundaryScanner scanner = new();
		ReadOnlySequence<byte> buffer = new(new byte[] { 0x91, 0xc1 }); // fixarray[reserved]

		void Act() => scanner.TryScan(buffer, isFinalBlock: true, out _, out _);

		await Assert.That(Act).Throws<DecoderException>();
	}

	[Test]
	public async Task ScannerInstance_IsReusableAcrossConcatenatedValues()
	{
		byte[] concatenated = [0xc0, 0x01, 0xa1, 0x78]; // nil, positive fixint 1, fixstr "x"
		MsgPackValueBoundaryScanner scanner = new();
		ReadOnlySequence<byte> buffer = new(concatenated);

		// Note: ReadOnlySequence<byte>.GetOffset returns an offset relative to the *underlying storage*,
		// not relative to a sliced view's Start, so byte counts consumed between two positions are computed
		// via Slice(start, end).Length rather than GetOffset here.
		bool first = scanner.TryScan(buffer, isFinalBlock: true, out SequencePosition firstEnd, out _);
		long firstLength = buffer.Slice(buffer.Start, firstEnd).Length;
		bool second = scanner.TryScan(buffer.Slice(firstEnd), isFinalBlock: true, out SequencePosition secondEnd, out _);
		long secondLength = buffer.Slice(firstEnd, secondEnd).Length;
		bool third = scanner.TryScan(buffer.Slice(secondEnd), isFinalBlock: true, out SequencePosition thirdEnd, out _);
		long thirdLength = buffer.Slice(secondEnd, thirdEnd).Length;

		await Assert.That(first).IsTrue();
		await Assert.That(firstLength).IsEqualTo(1L);
		await Assert.That(second).IsTrue();
		await Assert.That(secondLength).IsEqualTo(1L);
		await Assert.That(third).IsTrue();
		await Assert.That(thirdLength).IsEqualTo(2L);
	}
}
