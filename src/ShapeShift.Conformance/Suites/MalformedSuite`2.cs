// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Conformance.Suites;

/// <summary>
/// Feeds a decoder input it should reject and asserts that it rejects it cleanly.
/// </summary>
/// <typeparam name="TEncoder">The format's encoder type.</typeparam>
/// <typeparam name="TDecoder">The format's decoder type.</typeparam>
/// <remarks>
/// A decoder reads attacker-controlled bytes. "Clean" means <see cref="DecoderException"/> or
/// <see cref="ShapeShiftSerializationException"/>. An <see cref="IndexOutOfRangeException"/>,
/// <see cref="ArgumentOutOfRangeException"/>, <see cref="NullReferenceException"/>, or an
/// unbounded allocation instead means a missing length or bounds check.
/// </remarks>
internal sealed class MalformedSuite<TEncoder, TDecoder> : IConformanceSuite<TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public ConformanceCategory Category => ConformanceCategory.Malformed;

	/// <inheritdoc/>
	public void AddTests(ConformanceTestCollector<TEncoder, TDecoder> collector)
	{
		Requires.NotNull(collector);

		string? truncationSkip = collector.Options.DetectsTruncatedInput
			? null
			: "The format cannot detect a truncated payload.";

		collector.Add("TruncatedPayloadFailsCleanly", truncationSkip, adapter =>
		{
			byte[] payload = EncodeRichDocument(adapter);
			ShapeShiftSerializer<TEncoder, TDecoder> serializer = adapter.CreateSerializer();

			for (int length = 0; length < payload.Length; length++)
			{
				ReadOnlyMemory<byte> truncated = payload.AsMemory(0, length);
				ConformanceAssert.FailsCleanlyOrSucceeds(
					() => adapter.Decode(truncated, static (ref TDecoder decoder) =>
					{
						decoder.Skip();
						return 0;
					}),
					$"skipping a payload truncated to {length} of {payload.Length} bytes");

				ConformanceAssert.FailsCleanlyOrSucceeds(
					() => adapter.Deserialize(serializer, truncated, Shapes.Of<ShapeShiftValue, ConformanceWitness>()),
					$"deserializing a payload truncated to {length} of {payload.Length} bytes");
			}
		});

		collector.Add("CorruptedPayloadFailsCleanly", truncationSkip, adapter =>
		{
			byte[] payload = EncodeRichDocument(adapter);
			ShapeShiftSerializer<TEncoder, TDecoder> serializer = adapter.CreateSerializer();

			byte[] mutations = [0x00, 0x01, 0x7F, 0x80, 0xC1, 0xDF, 0xFF];
			for (int index = 0; index < payload.Length; index++)
			{
				foreach (byte mutation in mutations)
				{
					byte[] corrupted = (byte[])payload.Clone();
					if (corrupted[index] == mutation)
					{
						continue;
					}

					corrupted[index] = mutation;
					ConformanceAssert.FailsCleanlyOrSucceeds(
						() => adapter.Deserialize(serializer, corrupted, Shapes.Of<ShapeShiftValue, ConformanceWitness>()),
						$"deserializing a payload whose byte {index} was replaced with 0x{mutation:X2}");
				}
			}
		});

		collector.Add("EmptyPayloadFailsCleanly", adapter =>
		{
			ConformanceAssert.FailsCleanly(
				() => adapter.Decode(ReadOnlyMemory<byte>.Empty, static (ref TDecoder decoder) =>
				{
					decoder.Skip();
					return 0;
				}),
				"skipping an empty payload");
		});

		collector.Add("ReadingAStringAsAMapFailsCleanly", collector.Options.RejectsTypeMismatches ? null : "The format coerces mismatched types.", adapter =>
		{
			byte[] payload = RootHarness.EncodeScalar(adapter, static (ref TEncoder encoder) => encoder.WriteValue("text"));
			ConformanceAssert.FailsCleanly(
				() => RootHarness.DecodeScalar(adapter, payload, static (ref TDecoder decoder) => decoder.ReadStartMap()),
				"reading a string as the start of a map");
		});

		collector.Add("ReadingAMapAsAStringFailsCleanly", collector.Options.RejectsTypeMismatches ? null : "The format coerces mismatched types.", adapter =>
		{
			byte[] payload = adapter.Encode(static (ref TEncoder encoder) =>
			{
				encoder.WriteStartMap(0);
				encoder.WriteEndMap();
			});

			ConformanceAssert.FailsCleanly(
				() => adapter.Decode(payload, static (ref TDecoder decoder) => decoder.ReadString()),
				"reading the start of a map as a string");
		});

		collector.Add("ReadingAStringAsANumberFailsCleanly", collector.Options.RejectsTypeMismatches ? null : "The format coerces mismatched types.", adapter =>
		{
			byte[] payload = RootHarness.EncodeScalar(adapter, static (ref TEncoder encoder) => encoder.WriteValue("not a number"));
			ConformanceAssert.FailsCleanly(
				() => RootHarness.DecodeScalar(adapter, payload, static (ref TDecoder decoder) => decoder.ReadInt64()),
				"reading a non-numeric string as an integer");
		});

		collector.Add("ReadingAPropertyNameOutsideAMapFailsCleanly", collector.Options.RejectsTypeMismatches ? null : "The format coerces mismatched types.", adapter =>
		{
			byte[] payload = RootHarness.EncodeVector(adapter, static (ref TEncoder encoder) =>
			{
				encoder.WriteStartVector(1);
				encoder.WriteValue("element");
				encoder.WriteEndVector();
			});

			ConformanceAssert.FailsCleanly(
				() => RootHarness.DecodeVector(adapter, payload, static (ref TDecoder decoder) =>
				{
					decoder.ReadStartVector();
					return decoder.ReadPropertyName().ToString();
				}),
				"reading a vector element as a property name");
		});

		collector.AddIf(
			"ErrorsCarryThePathToTheFailure",
			collector.Options.ReportsErrorPaths,
			"The format's decoder rejects the malformed member before the converter layer can attribute it.",
			adapter =>
			{
				ShapeShiftSerializer<TEncoder, TDecoder> serializer = adapter.CreateSerializer();
				byte[] payload = adapter.Encode(static (ref TEncoder encoder) =>
				{
					encoder.WriteStartMap(3);
					encoder.WritePropertyName("Name");
					encoder.WriteValue("Ada");
					encoder.WritePropertyName("Age");
					encoder.WriteValue(1L);
					encoder.WritePropertyName("Scores");
					encoder.WriteStartVector(2);
					encoder.WriteValue(1L);
					encoder.WriteValue("not an int");
					encoder.WriteEndVector();
					encoder.WriteEndMap();
				});

				ShapeShiftSerializationException exception = ConformanceAssert.Throws<ShapeShiftSerializationException>(
					() => adapter.Deserialize(serializer, payload, Shapes.Of<ConformancePerson>()),
					"deserializing an object whose vector member holds a wrongly typed element");

				ConformanceAssert.True(
					exception.Path.Count > 0,
					$"A failure inside a member should report the path to it, but the path was empty. Message: {exception.Message}");
			});
	}

	private static byte[] EncodeRichDocument(FormatConformanceAdapter<TEncoder, TDecoder> adapter)
	{
		bool binary = adapter.Options.SupportsBinary;
		return adapter.Encode((ref TEncoder encoder) =>
		{
			encoder.WriteStartMap(binary ? 5 : 4);

			encoder.WritePropertyName("text");
			encoder.WriteValue("a moderately long string value");

			encoder.WritePropertyName("number");
			encoder.WriteValue(1234567890L);

			encoder.WritePropertyName("items");
			encoder.WriteStartVector(3);
			encoder.WriteValue(1L);
			encoder.WriteValue(2L);
			encoder.WriteValue(3L);
			encoder.WriteEndVector();

			encoder.WritePropertyName("nested");
			encoder.WriteStartMap(2);
			encoder.WritePropertyName("flag");
			encoder.WriteValue(true);
			encoder.WritePropertyName("nothing");
			encoder.WriteNull();
			encoder.WriteEndMap();

			if (binary)
			{
				encoder.WritePropertyName("blob");
				encoder.WriteValue(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }.AsSpan());
			}

			encoder.WriteEndMap();
		});
	}
}
