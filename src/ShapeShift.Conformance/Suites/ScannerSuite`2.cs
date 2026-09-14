// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Conformance.Suites;

/// <summary>
/// Verifies the <see cref="IValueBoundaryScanner"/> a format supplies to back its asynchronous adapters.
/// </summary>
/// <typeparam name="TEncoder">The format's encoder type.</typeparam>
/// <typeparam name="TDecoder">The format's decoder type.</typeparam>
/// <remarks>
/// Because decoders are <see langword="ref" /> structs that cannot survive an <see langword="await" />,
/// asynchronous deserialization buffers input until one complete top-level value is present and then
/// decodes it synchronously. The scanner is what decides "complete", so an off-by-one there corrupts
/// every streaming read. These cases feed a payload one byte at a time -- the worst case a
/// <see cref="System.IO.Pipelines.PipeReader"/> can produce -- and check that the scanner never claims
/// completeness early, never misses completeness, and never rewinds its <c>examined</c> position.
/// </remarks>
internal sealed class ScannerSuite<TEncoder, TDecoder> : IConformanceSuite<TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public ConformanceCategory Category => ConformanceCategory.Scanner;

	/// <inheritdoc/>
	public void AddTests(ConformanceTestCollector<TEncoder, TDecoder> collector)
	{
		Requires.NotNull(collector);

		string? skipReason = collector.Adapter.CreateValueBoundaryScanner() is null
			? "The format supplies no value boundary scanner."
			: null;

		collector.Add("ScansCompleteValueInOneChunk", skipReason, adapter =>
		{
			byte[] payload = EncodeValue(adapter, 1);
			IValueBoundaryScanner scanner = adapter.CreateValueBoundaryScanner()!;
			ReadOnlySequence<byte> buffer = new(payload);

			ConformanceAssert.True(
				scanner.TryScan(buffer, isFinalBlock: true, out SequencePosition end, out SequencePosition examined),
				"A complete value in a single buffer should be recognized.");
			ConformanceAssert.Equal(payload.Length, (int)buffer.Slice(buffer.Start, end).Length, "the length the scanner attributed to a complete value");
			ConformanceAssert.Equal(
				(int)buffer.Slice(buffer.Start, end).Length,
				(int)buffer.Slice(buffer.Start, examined).Length,
				"the examined position, which must equal the end position on success");
		});

		collector.Add("IncompleteValueRequestsMoreInput", skipReason, adapter =>
		{
			byte[] payload = EncodeValue(adapter, 1);
			for (int length = 0; length < payload.Length; length++)
			{
				IValueBoundaryScanner scanner = adapter.CreateValueBoundaryScanner()!;
				ReadOnlySequence<byte> buffer = new(payload.AsMemory(0, length));
				bool found;
				try
				{
					found = scanner.TryScan(buffer, isFinalBlock: false, out _, out SequencePosition examined);
					ConformanceAssert.True(
						buffer.Slice(buffer.Start, examined).Length <= length,
						"A scanner may never report an examined position beyond the buffer it was given.");
				}
				catch (DecoderException)
				{
					// A prefix that is already provably malformed may be rejected outright.
					continue;
				}

				ConformanceAssert.False(found, $"A {length}-byte prefix of a {payload.Length}-byte value is not a complete value.");
			}
		});

		collector.Add("ScansValueDeliveredOneByteAtATime", skipReason, adapter =>
		{
			byte[] payload = EncodeValue(adapter, 1);
			IValueBoundaryScanner scanner = adapter.CreateValueBoundaryScanner()!;

			for (int length = 0; length <= payload.Length; length++)
			{
				ReadOnlySequence<byte> buffer = new(payload.AsMemory(0, length));
				bool isFinal = length == payload.Length;
				if (scanner.TryScan(buffer, isFinal, out SequencePosition end, out _))
				{
					ConformanceAssert.Equal(
						payload.Length,
						(int)buffer.Slice(buffer.Start, end).Length,
						$"the value length the scanner reported once {length} of {payload.Length} bytes had arrived");
					return;
				}
			}

			throw new ConformanceAssertionException("The scanner never recognized a complete value, even after every byte arrived.");
		});

		collector.Add("ScansSuccessiveValues", skipReason, adapter =>
		{
			byte[] first = EncodeValue(adapter, 1);
			byte[] second = EncodeValue(adapter, 2);
			byte[] combined = [.. first, .. second];

			IValueBoundaryScanner scanner = adapter.CreateValueBoundaryScanner()!;
			ReadOnlySequence<byte> buffer = new(combined);

			ConformanceAssert.True(scanner.TryScan(buffer, isFinalBlock: true, out SequencePosition firstEnd, out _), "The first of two concatenated values should be recognized.");
			int firstLength = (int)buffer.Slice(buffer.Start, firstEnd).Length;
			ConformanceAssert.True(
				firstLength > 0 && firstLength <= combined.Length,
				$"The first value's length ({firstLength}) should fall within the combined buffer.");

			ReadOnlySequence<byte> remainder = buffer.Slice(firstEnd);
			ConformanceAssert.True(scanner.TryScan(remainder, isFinalBlock: true, out SequencePosition secondEnd, out _), "The second of two concatenated values should be recognized.");
			ConformanceAssert.Equal(
				combined.Length,
				firstLength + (int)remainder.Slice(remainder.Start, secondEnd).Length,
				"the total length the scanner attributed to two concatenated values");
		});

		collector.Add("ScansValueSplitAcrossSegments", skipReason, adapter =>
		{
			byte[] payload = EncodeValue(adapter, 1);
			if (payload.Length < 2)
			{
				return;
			}

			IValueBoundaryScanner scanner = adapter.CreateValueBoundaryScanner()!;
			ReadOnlySequence<byte> buffer = CreateSegmented(payload, payload.Length / 2);

			ConformanceAssert.True(
				scanner.TryScan(buffer, isFinalBlock: true, out SequencePosition end, out _),
				"A complete value spanning two segments should be recognized.");
			ConformanceAssert.Equal(payload.Length, (int)buffer.Slice(buffer.Start, end).Length, "the length the scanner attributed to a segmented value");
		});
	}

	private static byte[] EncodeValue(FormatConformanceAdapter<TEncoder, TDecoder> adapter, long marker)
		=> adapter.Encode((ref TEncoder encoder) =>
		{
			encoder.WriteStartMap(2);
			encoder.WritePropertyName("id");
			encoder.WriteValue(marker);
			encoder.WritePropertyName("name");
			encoder.WriteValue("value");
			encoder.WriteEndMap();
		});

	private static ReadOnlySequence<byte> CreateSegmented(byte[] payload, int splitAt)
	{
		Segment first = new(payload.AsMemory(0, splitAt), 0);
		Segment second = first.Append(payload.AsMemory(splitAt));
		return new ReadOnlySequence<byte>(first, 0, second, second.Memory.Length);
	}

	/// <summary>
	/// A minimal <see cref="ReadOnlySequenceSegment{T}"/> so the suite can hand the scanner
	/// a genuinely discontiguous buffer.
	/// </summary>
	private sealed class Segment : ReadOnlySequenceSegment<byte>
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="Segment"/> class.
		/// </summary>
		/// <param name="memory">The segment's bytes.</param>
		/// <param name="runningIndex">The offset of this segment within the whole sequence.</param>
		internal Segment(ReadOnlyMemory<byte> memory, long runningIndex)
		{
			this.Memory = memory;
			this.RunningIndex = runningIndex;
		}

		/// <summary>
		/// Appends a segment after this one.
		/// </summary>
		/// <param name="memory">The next segment's bytes.</param>
		/// <returns>The appended segment.</returns>
		internal Segment Append(ReadOnlyMemory<byte> memory)
		{
			Segment next = new(memory, this.RunningIndex + this.Memory.Length);
			this.Next = next;
			return next;
		}
	}
}
