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
	public async Task UnixTimeTag_ReadsEpochSeconds()
	{
		await AssertEpoch(new byte[] { 0xc1, 0x00 }, 0);
		await AssertEpoch(new byte[] { 0xc1, 0x01 }, 1);
		await AssertEpoch(new byte[] { 0xc1, 0x20 }, -1);
		await AssertEpoch(new byte[] { 0xc1, 0xf9, 0x3c, 0x00 }, 1);
		await AssertEpoch(new byte[] { 0xc1, 0xfb, 0x3f, 0xf8, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, 1.5);

		async Task AssertEpoch(byte[] encoded, double seconds)
		{
			DateTime? actual = this.serializer.Deserialize<DateTime, Witness>(encoded);
			await Assert.That(actual).IsEqualTo(DateTime.UnixEpoch.AddSeconds(seconds));
		}
	}

	[Test]
	public async Task UnixTimeTag_DynamicValueIsTheNumericOffset()
	{
		await Assert.That(this.serializer.Deserialize<ShapeShiftValue>(new byte[] { 0xc1, 0x00 })).IsEqualTo(new ShapeShiftUnsignedInteger(0));
		await Assert.That(this.serializer.Deserialize<ShapeShiftValue>(new byte[] { 0xc1, 0x20 })).IsEqualTo(new ShapeShiftInteger(-1));
	}

	[Test]
	public async Task UnixTimeTag_RejectsNestedTags()
	{
		Func<ShapeShiftValue?> nested = () => this.serializer.Deserialize<ShapeShiftValue>(new byte[] { 0xc1, 0xc1, 0x00 });
		await Assert.That(nested).Throws<DecoderException>();

		byte[] repeated = new byte[4096];
		Array.Fill(repeated, (byte)0xc1);
		repeated[^1] = 0x00;
		Func<ShapeShiftValue?> deep = () => this.serializer.Deserialize<ShapeShiftValue>(repeated);
		await Assert.That(deep).Throws<DecoderException>();
	}

	[Test]
	public async Task UnixTimeTag_RejectsValuesOutsideDateTime()
	{
		byte[] signed = [0xc1, 0x1b, 0x7f, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff];
		byte[] unsigned = [0xc1, 0x1b, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff];
		byte[] floating = [0xc1, 0xfb, 0x44, 0x15, 0xaf, 0x1d, 0x78, 0xb5, 0x8c, 0x40];
		Func<DateTime?> deserializeSigned = () => this.serializer.Deserialize<DateTime, Witness>(signed);
		Func<DateTime?> deserializeUnsigned = () => this.serializer.Deserialize<DateTime, Witness>(unsigned);
		Func<DateTime?> deserializeFloating = () => this.serializer.Deserialize<DateTime, Witness>(floating);

		await Assert.That(deserializeSigned).Throws<DecoderException>();
		await Assert.That(deserializeUnsigned).Throws<DecoderException>();
		await Assert.That(deserializeFloating).Throws<DecoderException>();
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
