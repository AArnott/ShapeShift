// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using ShapeShift.Tests;

namespace ShapeShift.Json.Tests;

/// <summary>
/// Directly exercises <see cref="JsonValueBoundaryScanner"/>'s incremental, resumable value recognition,
/// independent of the higher-level <see cref="PipeReaderExtensions.ReadValueAsync{T}"/> pump loop.
/// </summary>
public class JsonValueBoundaryScannerTests : TestBase
{
	/// <summary>
	/// Gets every named case, each a complete, self-contained JSON encoding of exactly one top-level value
	/// (with no surrounding whitespace, so its UTF-8 byte length is unambiguous).
	/// </summary>
	public static IEnumerable<(string Name, byte[] Bytes)> ScalarAndContainerCases { get; } =
	[
		("null", Encode("null")),
		("true", Encode("true")),
		("false", Encode("false")),
		("zero", Encode("0")),
		("negative zero", Encode("-0")),
		("positive integer", Encode("123")),
		("negative integer", Encode("-123")),
		("decimal", Encode("3.14")),
		("exponent", Encode("1e10")),
		("negative exponent", Encode("1E-10")),
		("empty string", Encode("\"\"")),
		("simple string", Encode("\"abc\"")),
		("string with escape", Encode("\"a\\nb\"")),
		("empty object", Encode("{}")),
		("object with one property", Encode("{\"a\":1}")),
		("object with multiple properties", Encode("{\"a\":1,\"b\":true,\"c\":null}")),
		("empty array", Encode("[]")),
		("array with elements", Encode("[1,2,3]")),
		("nested object and array", Encode("{\"a\":[1,2],\"b\":{\"c\":\"x\"}}")),
	];

	[Test]
	public async Task RecognizesEachCompleteEncoding()
	{
		foreach ((string name, byte[] bytes) in ScalarAndContainerCases)
		{
			JsonValueBoundaryScanner scanner = new();
			ReadOnlySequence<byte> buffer = new(bytes);
			bool found = scanner.TryScan(buffer, isFinalBlock: true, out SequencePosition end, out _);

			await Assert.That(found).IsTrue();
			await Assert.That(buffer.Slice(buffer.Start, end).Length).IsEqualTo((long)bytes.Length);
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

			JsonValueBoundaryScanner scanner = new();
			ReadOnlySequence<byte> truncated = new(bytes[..^1]);
			bool found = scanner.TryScan(truncated, isFinalBlock: false, out _, out _);

			await Assert.That(found).IsFalse();
		}
	}

	[Test]
	public async Task FeedingOneByteAtATime_EventuallyRecognizesNestedStructure()
	{
		byte[] value = Encode("""{"a":[1,2,3],"b":"hello world"}""");

		JsonValueBoundaryScanner scanner = new();
		bool found = false;
		ReadOnlySequence<byte> lastBuffer = default;
		SequencePosition end = default;

		// Each call must be given a buffer starting exactly where the previous call's `examined` position left
		// off (mirroring how a caller is expected to advance an underlying PipeReader), so track how much of
		// `value` has been examined-but-not-yet-boundary-confirmed and slice from there each time.
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
	public async Task FeedingOneByteAtATime_DisambiguatesTrailingDigitsOfNumber()
	{
		// "123" followed immediately by a NDJSON-style newline separator: while only "123" has arrived and more
		// data could still be pending, the reader must not commit to a 3-digit number prematurely.
		byte[] value = Encode("123");

		JsonValueBoundaryScanner scanner = new();
		bool found = false;
		int examinedOffset = 0;
		for (int i = 1; i <= value.Length; i++)
		{
			ReadOnlySequence<byte> partial = new(value[examinedOffset..i]);

			// isFinalBlock stays false throughout: this simulates a number at the end of an in-progress buffer
			// whose true end has not yet been observed by the caller.
			found = scanner.TryScan(partial, isFinalBlock: false, out _, out SequencePosition examined);

			// A partially-arrived bare scalar is never safe to examine past: the scanner must report `examined`
			// as the start of the buffer (nothing consumed) so the still-undecided digits remain available.
			examinedOffset += (int)partial.Slice(partial.Start, examined).Length;
		}

		// With isFinalBlock: false throughout, a bare number can never be conclusively recognized: more digits
		// might still follow in a not-yet-received chunk.
		await Assert.That(found).IsFalse();
		await Assert.That(examinedOffset).IsEqualTo(0);

		// Only once the caller asserts no more data is coming (isFinalBlock: true) is the number finalized.
		found = scanner.TryScan(new ReadOnlySequence<byte>(value), isFinalBlock: true, out SequencePosition end, out _);
		await Assert.That(found).IsTrue();
	}

	[Test]
	public async Task FeedingViaMultiSegmentSequence_RecognizesLargeArray()
	{
		// A 500-element array of small integers, fed through a chain of 7-byte memory segments (each
		// independent, simulating fragmented network buffers) to validate that scanning resumes correctly
		// across segment boundaries as well as across separate TryScan calls.
		StringBuilder json = new();
		json.Append('[');
		for (int i = 0; i < 500; i++)
		{
			if (i > 0)
			{
				json.Append(',');
			}

			json.Append(i);
		}

		json.Append(']');

		byte[] value = Encode(json.ToString());
		JsonValueBoundaryScanner scanner = new();
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
	public async Task MalformedInput_ThrowsJsonException()
	{
		JsonValueBoundaryScanner scanner = new();
		ReadOnlySequence<byte> buffer = new(Encode("{invalid}"));

		void Act() => scanner.TryScan(buffer, isFinalBlock: true, out _, out _);

		await Assert.That(Act).Throws<System.Text.Json.JsonException>();
	}

	[Test]
	public async Task ScannerInstance_IsReusableAcrossConcatenatedValues()
	{
		// Note: a bare number cannot be immediately followed by another value with no separator (JSON requires
		// a delimiter, such as whitespace, after a number unless it is the last thing in the document), so this
		// concatenation deliberately sticks to literal/string boundaries, which are always self-delimiting.
		byte[] concatenated = Encode("null\"x\"true");
		JsonValueBoundaryScanner scanner = new();
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
		await Assert.That(firstLength).IsEqualTo(4L); // "null"
		await Assert.That(second).IsTrue();
		await Assert.That(secondLength).IsEqualTo(3L); // "\"x\""
		await Assert.That(third).IsTrue();
		await Assert.That(thirdLength).IsEqualTo(4L); // "true"
	}

	private static byte[] Encode(string json) => Encoding.UTF8.GetBytes(json);
}
