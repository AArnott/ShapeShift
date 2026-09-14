// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using ShapeShift.Tests;

namespace ShapeShift.MsgPack.Tests;

/// <summary>
/// Verifies the MessagePack extension type codes ShapeShift reserves, the low-level extension APIs custom
/// converters build on, and the rejection of malformed or conflicting extension payloads.
/// </summary>
public partial class MsgPackExtensionTests : TestBase
{
	private readonly MsgPackSerializer serializer = new();

	[Test]
	public async Task ReservedCodes_AreInTheApplicationSpecificRange()
	{
		// The MessagePack specification reserves negative extension type codes for itself, so every encoding
		// ShapeShift invents lives in the application-specific half of the space.
		sbyte[] reserved = [MsgPackExtensionCodes.Decimal, MsgPackExtensionCodes.Int128, MsgPackExtensionCodes.UInt128, MsgPackExtensionCodes.BigInteger, MsgPackExtensionCodes.TimeSpan, MsgPackExtensionCodes.Reference];

		await Assert.That(reserved.All(code => code >= 0 && MsgPackExtensionCodes.IsReservedByShapeShift(code))).IsTrue();
		await Assert.That(reserved.Distinct().Count()).IsEqualTo(reserved.Length);

		// The timestamp comes from the specification rather than from ShapeShift's reservation.
		sbyte[] notReserved = [MsgPackExtensionCodes.Timestamp, 1, 42];
		await Assert.That(notReserved.Any(MsgPackExtensionCodes.IsReservedByShapeShift)).IsFalse();
	}

	[Test]
	public async Task Decimal_UsesTheReservedCode()
	{
		byte[] encoded = this.serializer.Serialize<decimal, Witness>(1.5m);
		MsgPackDecoder decoder = new(encoded);

		bool isExtension = decoder.TryPeekExtensionHeader(out MsgPackExtensionHeader header);

		await Assert.That(isExtension).IsTrue();
		await Assert.That(header.TypeCode).IsEqualTo(MsgPackExtensionCodes.Decimal);
		await Assert.That(header.Length).IsEqualTo(16);
		await Assert.That(this.serializer.Deserialize<decimal, Witness>(encoded)).IsEqualTo(1.5m);
	}

	[Test]
	public async Task TimeSpan_UsesTheReservedCode()
	{
		byte[] encoded = this.serializer.Serialize<TimeSpan, Witness>(TimeSpan.FromMinutes(3));
		MsgPackDecoder decoder = new(encoded);

		decoder.TryPeekExtensionHeader(out MsgPackExtensionHeader header);

		await Assert.That(header.TypeCode).IsEqualTo(MsgPackExtensionCodes.TimeSpan);
		await Assert.That(header.Length).IsEqualTo(8);
	}

	[Test]
	public async Task BigInteger_UsesTheReservedCodeWithAVariableLengthPayload()
	{
		byte[] encoded = this.serializer.Serialize<BigInteger, Witness>(BigInteger.Pow(2, 200));
		MsgPackDecoder decoder = new(encoded);

		decoder.TryPeekExtensionHeader(out MsgPackExtensionHeader header);

		await Assert.That(header.TypeCode).IsEqualTo(MsgPackExtensionCodes.BigInteger);
		await Assert.That(header.Length).IsGreaterThan(16);
		await Assert.That(this.serializer.Deserialize<BigInteger, Witness>(encoded)).IsEqualTo(BigInteger.Pow(2, 200));
	}

	[Test]
	public async Task Timestamp_UsesTheSpecificationCode()
	{
		byte[] encoded = this.serializer.Serialize<DateTime, Witness>(new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc));
		MsgPackDecoder decoder = new(encoded);

		decoder.TryPeekExtensionHeader(out MsgPackExtensionHeader header);

		await Assert.That(header.TypeCode).IsEqualTo(MsgPackExtensionCodes.Timestamp);
	}

	[Test]
	public async Task WrongExtensionPayloadLength_IsRejected()
	{
		// fixext8 carrying the decimal type code: the code is right but the payload is half as long as it must be.
		byte[] malformed = [0xd7, unchecked((byte)MsgPackExtensionCodes.Decimal), 0, 0, 0, 0, 0, 0, 0, 0];

		Func<decimal> deserialize = () => this.serializer.Deserialize<decimal, Witness>(malformed);

		await Assert.That(deserialize).Throws<DecoderException>();
	}

	[Test]
	public async Task WrongExtensionTypeCode_IsRejected()
	{
		// A 16-byte extension of the right shape, but carrying the Int128 code where a decimal is expected.
		byte[] malformed = [0xd8, unchecked((byte)MsgPackExtensionCodes.Int128), .. new byte[16]];

		Func<decimal> deserialize = () => this.serializer.Deserialize<decimal, Witness>(malformed);
		DecoderException? caught = null;
		try
		{
			deserialize();
		}
		catch (DecoderException ex)
		{
			caught = ex;
		}

		await Assert.That(caught).IsNotNull();
		await Assert.That(caught!.Message).Contains("Int128");
	}

	[Test]
	public async Task TruncatedExtensionPayload_IsRejected()
	{
		// ext8 promising 200 bytes but supplying none.
		byte[] malformed = [0xc7, 200, unchecked((byte)MsgPackExtensionCodes.BigInteger)];

		Func<BigInteger> deserialize = () => this.serializer.Deserialize<BigInteger, Witness>(malformed);

		await Assert.That(deserialize).Throws<DecoderException>();
	}

	[Test]
	public async Task ReservedExtensionInTheWrongPlace_IsRejectedWithAnActionableMessage()
	{
		// A reference extension where an object was expected: a payload written with reference preservation
		// enabled, being read by a serializer that has it turned off.
		byte[] payload = [0x82, 0xa4, (byte)'N', (byte)'a', (byte)'m', (byte)'e', 0xa1, (byte)'a', 0xa4, (byte)'S', (byte)'e', (byte)'l', (byte)'f', 0xd4, unchecked((byte)MsgPackExtensionCodes.Reference), 0];

		DecoderException? caught = Capture(() => this.serializer.Deserialize<Node>(payload));

		await Assert.That(caught).IsNotNull();
		await Assert.That(caught!.Message).Contains("object reference");
	}

	[Test]
	public async Task UnreservedExtension_SurvivesAsOpaqueBinary()
	{
		// An application's own extension (code 7) must not be mistaken for one of ShapeShift's, and must be
		// skippable so that unknown data in a payload does not break a reader.
		ArrayBufferWriter<byte> buffer = new();
		MsgPackEncoder encoder = new(buffer);
		encoder.WriteMapHeader(2);
		encoder.WritePropertyName("Name");
		encoder.WriteValue("Ada");
		encoder.WritePropertyName("Unknown");
		encoder.WriteExtension(7, [1, 2, 3]);

		Node? value = this.serializer.Deserialize<Node>(buffer.WrittenSpan);

		await Assert.That(value?.Name).IsEqualTo("Ada");
	}

	[Test]
	public async Task CustomExtension_RoundTripsThroughTheLowLevelApis()
	{
		ArrayBufferWriter<byte> buffer = new();
		MsgPackEncoder encoder = new(buffer);
		encoder.WriteExtension(42, [9, 8, 7, 6]);

		(bool IsExtension, MsgPackExtensionHeader Header, byte[] Payload) result = Read(buffer.WrittenSpan);

		await Assert.That(result.IsExtension).IsTrue();
		await Assert.That(result.Header).IsEqualTo(new MsgPackExtensionHeader(42, 4));
		await Assert.That(result.Payload.AsSpan().SequenceEqual(new byte[] { 9, 8, 7, 6 })).IsTrue();

		static (bool IsExtension, MsgPackExtensionHeader Header, byte[] Payload) Read(ReadOnlySpan<byte> encoded)
		{
			MsgPackDecoder decoder = new(encoded);
			bool isExtension = decoder.TryPeekExtensionHeader(out MsgPackExtensionHeader header);
			byte[] payload = decoder.ReadExtension(42);
			decoder.EnsureEndOfDocument();
			return (isExtension, header, payload);
		}
	}

	[Test]
	public async Task ReadExtension_RejectsATooSmallDestination()
	{
		ArrayBufferWriter<byte> buffer = new();
		MsgPackEncoder encoder = new(buffer);
		encoder.WriteExtension(42, new byte[16]);

		byte[] encoded = buffer.WrittenSpan.ToArray();
		Action read = () =>
		{
			MsgPackDecoder decoder = new(encoded);
			Span<byte> tooSmall = stackalloc byte[8];
			decoder.ReadExtension(42, tooSmall);
		};

		await Assert.That(read).Throws<DecoderException>();
	}

	[Test]
	public async Task WriteRaw_CopiesPreEncodedBytes()
	{
		byte[] inner = this.serializer.Serialize<int, Witness>(7);
		ArrayBufferWriter<byte> buffer = new();
		MsgPackEncoder encoder = new(buffer);
		encoder.WriteArrayHeader(2);
		encoder.WriteRaw(inner);
		encoder.WriteRaw(inner);

		int[]? values = this.serializer.Deserialize<int[], Witness>(buffer.WrittenSpan);

		await Assert.That(values?.SequenceEqual([7, 7])).IsTrue();
	}

	[Test]
	public async Task ReferenceExtensionWithBadPayloadLength_IsRejected()
	{
		MsgPackSerializer preserving = this.serializer with { PreserveReferences = ReferencePreservationMode.RejectCycles };

		// ext8 with a 3-byte payload: a length no reference identifier ever uses.
		byte[] malformed = [0x92, 0x81, 0xa4, (byte)'N', (byte)'a', (byte)'m', (byte)'e', 0xa1, (byte)'a', 0xc7, 3, unchecked((byte)MsgPackExtensionCodes.Reference), 0, 0, 0];

		DecoderException? caught = Capture(() => preserving.Deserialize<Node[], Witness>(malformed));

		await Assert.That(caught).IsNotNull();
		await Assert.That(caught!.Message).Contains("1, 2, or 4 bytes");
	}

	[Test]
	public async Task ExtensionCodesAreStable()
	{
		// These values are a wire contract. Changing one silently breaks every previously written payload.
		sbyte[] actual = [
			MsgPackExtensionCodes.Timestamp,
			MsgPackExtensionCodes.Decimal,
			MsgPackExtensionCodes.Int128,
			MsgPackExtensionCodes.UInt128,
			MsgPackExtensionCodes.BigInteger,
			MsgPackExtensionCodes.TimeSpan,
			MsgPackExtensionCodes.Reference,
		];
		sbyte[] expected = [-1, 100, 101, 102, 103, 104, 105];

		await Assert.That(actual.SequenceEqual(expected)).IsTrue();
	}

	[Test]
	public async Task ExtensionPayloadShapesAreStable()
	{
		byte[] money = this.serializer.Serialize<decimal, Witness>(-1.25m);
		byte[] signed = this.serializer.Serialize<Int128, Witness>(Int128.MinValue);
		byte[] duration = this.serializer.Serialize<TimeSpan, Witness>(TimeSpan.FromTicks(123456789));

		await Assert.That(money[0]).IsEqualTo((byte)0xd8);
		await Assert.That(money[1]).IsEqualTo(unchecked((byte)MsgPackExtensionCodes.Decimal));
		await Assert.That(signed[0]).IsEqualTo((byte)0xd8);
		await Assert.That(BinaryPrimitives.ReadInt128BigEndian(signed.AsSpan(2))).IsEqualTo(Int128.MinValue);
		await Assert.That(duration[0]).IsEqualTo((byte)0xd7);
		await Assert.That(BinaryPrimitives.ReadInt64BigEndian(duration.AsSpan(2))).IsEqualTo(123456789L);
	}

	/// <summary>
	/// Runs an operation and returns the <see cref="DecoderException"/> it failed with, wherever that exception
	/// appears in the chain of wrappers that add path breadcrumbs to it.
	/// </summary>
	private static DecoderException? Capture(Action action)
	{
		try
		{
			action();
			return null;
		}
		catch (Exception ex)
		{
			for (Exception? candidate = ex; candidate is not null; candidate = candidate.InnerException)
			{
				if (candidate is DecoderException decoderException)
				{
					return decoderException;
				}
			}

			throw;
		}
	}

	[GenerateShape]
	internal partial class Node
	{
		public string? Name { get; set; }

		public Node? Self { get; set; }
	}

	[GenerateShapeFor<int>]
	[GenerateShapeFor<int[]>]
	[GenerateShapeFor<decimal>]
	[GenerateShapeFor<Int128>]
	[GenerateShapeFor<TimeSpan>]
	[GenerateShapeFor<DateTime>]
	[GenerateShapeFor<BigInteger>]
	[GenerateShapeFor<Node[]>]
	private partial class Witness;
}
