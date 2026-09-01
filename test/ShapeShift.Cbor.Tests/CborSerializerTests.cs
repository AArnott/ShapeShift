// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Numerics;
using PolyType;
using ShapeShift.Tests;

namespace ShapeShift.Cbor.Tests;

public partial class CborSerializerTests : TestBase
{
	private readonly CborSerializer serializer = new();

	[Test]
	public async Task PrimitiveInteger_UsesCborInteger()
	{
		byte[] encoded = this.serializer.Serialize<int, Witness>(42);

		await Assert.That(encoded.SequenceEqual(new byte[] { 0x18, 42 })).IsTrue();
		await Assert.That(this.serializer.Deserialize<int, Witness>(encoded)).IsEqualTo(42);
	}

	[Test]
	public async Task ObjectAndCollection_RoundTrip()
	{
		Person value = new("Ada", [1, 2, 3]);

		byte[] encoded = this.serializer.Serialize(value);
		Person? actual = this.serializer.Deserialize<Person>(encoded);

		await Assert.That(encoded[0]).IsEqualTo((byte)0xa2);
		await Assert.That(actual?.Name).IsEqualTo(value.Name);
		await Assert.That(actual?.Values.SequenceEqual(value.Values)).IsTrue();
	}

	[Test]
	public async Task ExtendedScalars_RoundTrip()
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
	public async Task ByteArray_UsesCborByteString()
	{
		byte[] value = [1, 2, 3];
		byte[] encoded = this.serializer.Serialize<byte[], Witness>(value);

		await Assert.That(encoded.SequenceEqual(new byte[] { 0x43, 1, 2, 3 })).IsTrue();
		await Assert.That(this.serializer.Deserialize<byte[], Witness>(encoded)?.SequenceEqual(value)).IsTrue();
	}

	[Test]
	public async Task TrailingData_IsRejected()
	{
		Func<int?> deserialize = () => this.serializer.Deserialize<int, Witness>(new byte[] { 1, 2 });

		await Assert.That(deserialize).Throws<DecoderException>();
	}

	[Test]
	public async Task DateTime_RejectsNonDateTimeTag()
	{
		Func<DateTime?> deserialize = () => this.serializer.Deserialize<DateTime, Witness>(new byte[] { 0xc1, 0x63, (byte)'a', (byte)'b', (byte)'c' });

		await Assert.That(deserialize).Throws<DecoderException>();
	}

	[Test]
	public async Task DynamicBigInteger_RoundTrips()
	{
		ShapeShiftValue value = new ShapeShiftBigInteger(BigInteger.Parse("123456789012345678901234567890"));

		ShapeShiftValue? actual = this.serializer.Deserialize<ShapeShiftValue>(this.serializer.Serialize(value));

		await Assert.That(actual).IsEqualTo(value);
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
	[GenerateShapeFor<byte[]>]
	[GenerateShapeFor<DateTime>]
	private partial class Witness;
}
