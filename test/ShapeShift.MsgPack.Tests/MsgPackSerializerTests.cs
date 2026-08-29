// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.Numerics;
using ShapeShift.Tests;

namespace ShapeShift.MsgPack.Tests;

public partial class MsgPackSerializerTests : TestBase
{
	private readonly MsgPackSerializer serializer = new();

	[Test]
	public async Task PrimitiveInteger_UsesCompactEncoding()
	{
		byte[] encoded = this.serializer.Serialize<int, Witness>(42);

		await Assert.That(encoded.AsSpan().SequenceEqual([(byte)0x2a])).IsTrue();
		await Assert.That(this.serializer.Deserialize<int, Witness>(encoded)).IsEqualTo(42);
	}

	[Test]
	public async Task ObjectAndCollection_RoundTrip()
	{
		Person value = new("Ada", [1, 2, 3]);

		byte[] encoded = this.serializer.Serialize(value);
		Person? actual = this.serializer.Deserialize<Person>(encoded);

		await Assert.That(encoded[0]).IsEqualTo((byte)0x82);
		await Assert.That(actual?.Name).IsEqualTo(value.Name);
		await Assert.That(actual?.Values.SequenceEqual(value.Values)).IsTrue();
	}

	[Test]
	public async Task ExtendedNumbersAndTime_RoundTrip()
	{
		Scalars value = new(
			Int128.MaxValue,
			UInt128.MaxValue,
			decimal.MaxValue,
			BigInteger.Parse("123456789012345678901234567890"),
			TimeSpan.FromDays(42),
			new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc));

		Scalars? actual = this.serializer.Deserialize<Scalars>(this.serializer.Serialize(value));

		await Assert.That(actual).IsEqualTo(value);
	}

	[Test]
	public async Task DynamicBinary_RoundTripsAsBinary()
	{
		ShapeShiftValue value = new ShapeShiftBinary([1, 2, 3, 4]);

		byte[] encoded = this.serializer.Serialize(value);
		ShapeShiftValue? actual = this.serializer.Deserialize<ShapeShiftValue>(encoded);

		await Assert.That(encoded.AsSpan().SequenceEqual(new byte[] { 0xc4, 0x04, 1, 2, 3, 4 })).IsTrue();
		await Assert.That(actual).IsTypeOf<ShapeShiftBinary>();
		ShapeShiftBinary binary = actual as ShapeShiftBinary ?? throw new InvalidOperationException("Expected binary data.");
		await Assert.That(binary.Value.Span.SequenceEqual(new byte[] { 1, 2, 3, 4 })).IsTrue();
	}

	[Test]
	public async Task DuplicateMapProperty_IsRejected()
	{
		byte[] malformed = [0x82, 0xa4, (byte)'N', (byte)'a', (byte)'m', (byte)'e', 0xa1, (byte)'a', 0xa4, (byte)'N', (byte)'a', (byte)'m', (byte)'e', 0xa1, (byte)'b'];
		Func<Person?> deserialize = () => this.serializer.Deserialize<Person>(malformed);

		await Assert.That(deserialize).Throws<ShapeShiftSerializationException>();
	}

	[Test]
	public async Task TruncatedPayload_IsRejected()
	{
		Func<string?> deserialize = () => this.serializer.Deserialize<string, Witness>([0xd9, 0x05, (byte)'a']);

		await Assert.That(deserialize).Throws<DecoderException>();
	}

	[Test]
	public async Task ByteArray_UsesBinaryEncoding()
	{
		byte[] value = [1, 2, 3];

		byte[] encoded = this.serializer.Serialize<byte[], Witness>(value);
		byte[]? actual = this.serializer.Deserialize<byte[], Witness>(encoded);

		await Assert.That(encoded.AsSpan().SequenceEqual(new byte[] { 0xc4, 3, 1, 2, 3 })).IsTrue();
		await Assert.That(actual?.SequenceEqual(value)).IsTrue();
	}

	[Test]
	public async Task SegmentedSequence_IsAccepted()
	{
		byte[] encoded = this.serializer.Serialize(new Person("Ada", [1, 2, 3]));
		ReadOnlySequence<byte> sequence = CreateSequence(encoded[..3], encoded[3..]);

		Person? actual = this.serializer.Deserialize<Person>(sequence);

		await Assert.That(actual?.Name).IsEqualTo("Ada");
		await Assert.That(actual?.Values.SequenceEqual([1, 2, 3])).IsTrue();
	}

	private static ReadOnlySequence<byte> CreateSequence(byte[] first, byte[] second)
	{
		Segment firstSegment = new(first);
		Segment secondSegment = firstSegment.Append(second);
		return new(firstSegment, 0, secondSegment, second.Length);
	}

	[GenerateShape]
	internal partial record Person(string Name, List<int> Values);

	[GenerateShape]
	internal partial record Scalars(
		Int128 Signed,
		UInt128 Unsigned,
		decimal Decimal,
		BigInteger BigInteger,
		TimeSpan TimeSpan,
		DateTime DateTime);

	[GenerateShapeFor<int>]
	[GenerateShapeFor<string>]
	[GenerateShapeFor<byte[]>]
	private partial class Witness;

	private sealed class Segment : ReadOnlySequenceSegment<byte>
	{
		internal Segment(ReadOnlyMemory<byte> memory)
		{
			this.Memory = memory;
		}

		internal Segment Append(ReadOnlyMemory<byte> memory)
		{
			Segment segment = new(memory) { RunningIndex = this.RunningIndex + this.Memory.Length };
			this.Next = segment;
			return segment;
		}
	}
}
