// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Conformance.Suites;

/// <summary>
/// Round-trips binary values, which formats without a binary family opt out of.
/// </summary>
/// <typeparam name="TEncoder">The format's encoder type.</typeparam>
/// <typeparam name="TDecoder">The format's decoder type.</typeparam>
internal sealed class BinarySuite<TEncoder, TDecoder> : IConformanceSuite<TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public ConformanceCategory Category => ConformanceCategory.Binary;

	/// <inheritdoc/>
	public void AddTests(ConformanceTestCollector<TEncoder, TDecoder> collector)
	{
		Requires.NotNull(collector);

		bool supported = collector.Options.SupportsBinary;
		string? skipReason = supported ? null : "The format has no binary representation.";

		AddRoundtrip(collector, "Empty", skipReason, []);
		AddRoundtrip(collector, "Small", skipReason, [0, 1, 2, 3, 254, 255]);
		AddRoundtrip(collector, "Medium", skipReason, CreateBytes(4096));
		AddRoundtrip(collector, "Large", skipReason, CreateBytes(70_000));

		collector.Add("BinaryInsideMap", skipReason, adapter =>
		{
			byte[] bytes = CreateBytes(37);
			byte[] payload = adapter.Encode((ref TEncoder encoder) =>
			{
				encoder.WriteStartMap(2);
				encoder.WritePropertyName("blob");
				encoder.WriteValue(bytes.AsSpan());
				encoder.WritePropertyName("after");
				encoder.WriteValue(1L);
				encoder.WriteEndMap();
			});

			adapter.Decode(payload, (ref TDecoder decoder) =>
			{
				decoder.ReadStartMap();
				ConformanceAssert.Equal("blob", decoder.ReadPropertyName().ToString(), "the binary member's key");
				ConformanceAssert.NextToken(adapter.GetExpectedTokenType(ConformanceValueKind.Binary), ref decoder, "a binary map value");
				ConformanceAssert.EqualBytes(bytes, decoder.ReadByteArray(), "a binary map value");
				ConformanceAssert.Equal("after", decoder.ReadPropertyName().ToString(), "the key after a binary value");
				ConformanceAssert.Equal(1L, decoder.ReadInt64(), "the value after a binary value");
				decoder.ReadEndMap();
			});
		});

		collector.Add("BinaryIsSkippable", supported && collector.Options.SupportsSkip && collector.Options.SupportsHeterogeneousVectors ? null : skipReason ?? "The format does not implement Skip.", adapter =>
		{
			byte[] payload = RootHarness.EncodeVector(adapter, static (ref TEncoder encoder) =>
			{
				encoder.WriteStartVector(2);
				encoder.WriteValue(new byte[] { 9, 8, 7 }.AsSpan());
				encoder.WriteValue("tail");
				encoder.WriteEndVector();
			});

			RootHarness.DecodeVector(adapter, payload, static (ref TDecoder decoder) =>
			{
				decoder.ReadStartVector();
				decoder.Skip();
				ConformanceAssert.Equal("tail", decoder.ReadString(), "the element after a skipped binary value");
				decoder.ReadEndVector();
			});
		});

		collector.Add("UnsupportedBinaryThrowsNotSupported", supported ? "The format supports binary values." : null, adapter =>
		{
			ConformanceAssert.Throws<NotSupportedException>(
				() => adapter.Encode(static (ref TEncoder encoder) => encoder.WriteValue(new byte[] { 1 }.AsSpan())),
				"writing a binary value to a format that declares no binary support");
		});

		collector.Add("ByteArrayRoundtripsThroughSerializer", skipReason, adapter =>
		{
			ShapeShiftSerializer<TEncoder, TDecoder> serializer = adapter.CreateSerializer();
			byte[] value = CreateBytes(128);
			byte[]? roundtripped = adapter.Roundtrip(serializer, value, Shapes.Of<byte[], ConformanceWitness>());
			ConformanceAssert.EqualBytes(value, roundtripped ?? [], "a byte array round-tripped through the serializer");
		});
	}

	private static byte[] CreateBytes(int length)
	{
		byte[] bytes = new byte[length];
		for (int i = 0; i < length; i++)
		{
			bytes[i] = unchecked((byte)(i * 31));
		}

		return bytes;
	}

	private static void AddRoundtrip(ConformanceTestCollector<TEncoder, TDecoder> collector, string name, string? skipReason, byte[] value)
	{
		collector.Add(name, skipReason, adapter =>
		{
			byte[] roundtripped = RootHarness.RoundtripScalar(
				adapter,
				(ref TEncoder encoder) => encoder.WriteValue(value.AsSpan()),
				static (ref TDecoder decoder) => decoder.ReadByteArray());
			ConformanceAssert.EqualBytes(value, roundtripped, $"a round-tripped {value.Length}-byte binary value");
		});
	}
}
