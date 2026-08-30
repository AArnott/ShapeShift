// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.Numerics;
using ShapeShift.Tests;

namespace ShapeShift.MsgPack.Tests;

/// <summary>
/// Verifies that <see cref="MsgPackDecoder"/> reads a segmented <see cref="ReadOnlySequence{T}"/> in place:
/// values that straddle segment boundaries decode correctly, and content the caller skips over is never copied
/// (in particular, the sequence is never consolidated into one contiguous buffer).
/// </summary>
public partial class MsgPackSegmentedBufferTests : TestBase
{
	private readonly MsgPackSerializer serializer = new();

	[Test]
	[Arguments(1)]
	[Arguments(2)]
	[Arguments(3)]
	[Arguments(7)]
	[Arguments(64)]
	public async Task EveryValueKind_SurvivesEverySegmentation(int segmentSize)
	{
		Exotic value = new(
			"a string long enough to straddle several segments, with non-ASCII: \u00e9\u00fc\u4e2d\u6587",
			[1, 2, 3, 4, 5, 6, 7, 8, 9, 10],
			decimal.MaxValue,
			BigInteger.Parse("1234567890123456789012345678901234567890", System.Globalization.CultureInfo.InvariantCulture),
			TimeSpan.FromDays(3),
			new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc),
			[.. Enumerable.Range(0, 40).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture))]);
		byte[] encoded = this.serializer.Serialize(value);

		Exotic? actual = this.serializer.Deserialize<Exotic>(Segment.Create(encoded, segmentSize));

		await Assert.That(actual!.Text).IsEqualTo(value.Text);
		await Assert.That(actual.Blob.SequenceEqual(value.Blob)).IsTrue();
		await Assert.That(actual.Money).IsEqualTo(value.Money);
		await Assert.That(actual.Huge).IsEqualTo(value.Huge);
		await Assert.That(actual.Duration).IsEqualTo(value.Duration);
		await Assert.That(actual.When).IsEqualTo(value.When);
		await Assert.That(actual.Items.SequenceEqual(value.Items)).IsTrue();
	}

	[Test]
	public async Task LargeContainerHeadersSplitAcrossSegments_AreRead()
	{
		// 300 elements forces the array16 header, whose length prefix can itself straddle a boundary.
		string[] value = [.. Enumerable.Range(0, 300).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture))];
		byte[] encoded = this.serializer.Serialize<string[], Witness>(value);

		string[]? actual = this.serializer.Deserialize<string[], Witness>(Segment.Create(encoded, 2));

		await Assert.That(actual!.SequenceEqual(value)).IsTrue();
	}

	[Test]
	public async Task TargetedRead_FindsAValueBeyondSegmentBoundaries()
	{
		byte[] encoded = BuildDocument(payloadLength: 4096);

		bool found = this.serializer.TryDeserializeFragment<string, Witness>(Segment.Create(encoded, 16), new ShapeShiftPath("Header"), out string? header);

		await Assert.That(found).IsTrue();
		await Assert.That(header).IsEqualTo("hello");
	}

	[Test]
	public async Task TargetedRead_DoesNotConsolidateTheSequence()
	{
		// A decoder that copied a segmented sequence into one buffer (as an earlier implementation did) would
		// allocate at least the whole payload here. Walking it in place allocates only the small value asked for.
		const int PayloadLength = 4 * 1024 * 1024;
		byte[] encoded = BuildDocument(PayloadLength);
		ReadOnlySequence<byte> sequence = Segment.Create(encoded, 4096);

		// Warm up so that converter construction and JIT allocations are not attributed to the measured run.
		this.serializer.TryDeserializeFragment<string, Witness>(sequence, new ShapeShiftPath("Header"), out _);

		long before = GC.GetAllocatedBytesForCurrentThread();
		bool found = this.serializer.TryDeserializeFragment<string, Witness>(sequence, new ShapeShiftPath("Header"), out string? header);
		long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

		await Assert.That(found).IsTrue();
		await Assert.That(header).IsEqualTo("hello");
		await Assert.That(allocated).IsLessThan(64 * 1024);
		await Assert.That(sequence.Length).IsGreaterThan(PayloadLength);
	}

	[Test]
	public async Task WholeDocumentRead_DoesNotConsolidateSkippedContent()
	{
		// Deserializing a type that ignores a huge unknown property must not copy that property either.
		const int PayloadLength = 4 * 1024 * 1024;
		byte[] encoded = BuildDocument(PayloadLength);
		ReadOnlySequence<byte> sequence = Segment.Create(encoded, 4096);

		this.serializer.Deserialize<Header>(sequence);

		long before = GC.GetAllocatedBytesForCurrentThread();
		Header? actual = this.serializer.Deserialize<Header>(sequence);
		long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

		await Assert.That(actual!.HeaderValue).IsEqualTo("hello");
		await Assert.That(allocated).IsLessThan(64 * 1024);
	}

	[Test]
	public async Task SegmentedInput_RejectsTruncatedValues()
	{
		byte[] encoded = this.serializer.Serialize<string, Witness>("abcdefghijklmnop");
		ReadOnlySequence<byte> truncated = Segment.Create(encoded[..^4], 3);

		Func<string?> deserialize = () => this.serializer.Deserialize<string, Witness>(truncated);

		await Assert.That(deserialize).Throws<DecoderException>();
	}

	[Test]
	public async Task SegmentedInput_RejectsAnImpossibleLengthHeader()
	{
		// str32 claiming 4 GiB minus one, with three bytes of content.
		byte[] malformed = [0xdb, 0xff, 0xff, 0xff, 0xff, (byte)'a', (byte)'b', (byte)'c'];

		Func<string?> deserialize = () => this.serializer.Deserialize<string, Witness>(Segment.Create(malformed, 2));

		await Assert.That(deserialize).Throws<DecoderException>();
	}

	[Test]
	public async Task SegmentedInput_RejectsAnImpossibleContainerCount()
	{
		// array32 claiming 4 GiB minus one elements in an 8 byte buffer.
		byte[] malformed = [0xdd, 0xff, 0xff, 0xff, 0xff, 0x01, 0x02, 0x03];

		Func<string[]?> deserialize = () => this.serializer.Deserialize<string[], Witness>(Segment.Create(malformed, 3));

		await Assert.That(deserialize).Throws<DecoderException>();
	}

	[Test]
	public async Task SingleSegmentSequence_TakesTheContiguousPath()
	{
		byte[] encoded = this.serializer.Serialize<string, Witness>("hello");

		string? actual = this.serializer.Deserialize<string, Witness>(new ReadOnlySequence<byte>(encoded));

		await Assert.That(actual).IsEqualTo("hello");
	}

	/// <summary>
	/// Builds a two-property map whose first property is a large binary blob and whose second is a short string,
	/// so that reaching the string requires skipping the blob.
	/// </summary>
	private static byte[] BuildDocument(int payloadLength)
	{
		ArrayBufferWriter<byte> buffer = new();
		MsgPackEncoder encoder = new(buffer);
		encoder.WriteMapHeader(2);
		encoder.WritePropertyName("Payload");
		encoder.WriteValue(new byte[payloadLength].AsSpan());
		encoder.WritePropertyName("Header");
		encoder.WriteValue("hello");
		return buffer.WrittenSpan.ToArray();
	}

	[GenerateShape]
	internal partial record Exotic(
		string Text,
		byte[] Blob,
		decimal Money,
		BigInteger Huge,
		TimeSpan Duration,
		DateTime When,
		List<string> Items);

	[GenerateShape]
	internal partial class Header
	{
		[PropertyShape(Name = "Header")]
		public string? HeaderValue { get; set; }
	}

	[GenerateShapeFor<string>]
	[GenerateShapeFor<string[]>]
	private partial class Witness;

	private sealed class Segment : ReadOnlySequenceSegment<byte>
	{
		private Segment(ReadOnlyMemory<byte> memory)
		{
			this.Memory = memory;
		}

		/// <summary>
		/// Chops a buffer into a chain of segments of a fixed size, so that values land on and across boundaries.
		/// </summary>
		internal static ReadOnlySequence<byte> Create(byte[] bytes, int segmentSize)
		{
			Segment first = new(bytes.AsMemory(0, Math.Min(segmentSize, bytes.Length)));
			Segment last = first;
			for (int start = segmentSize; start < bytes.Length; start += segmentSize)
			{
				last = last.Append(bytes.AsMemory(start, Math.Min(segmentSize, bytes.Length - start)));
			}

			return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
		}

		private Segment Append(ReadOnlyMemory<byte> memory)
		{
			Segment segment = new(memory) { RunningIndex = this.RunningIndex + this.Memory.Length };
			this.Next = segment;
			return segment;
		}
	}
}
