// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Conformance.Suites;

/// <summary>
/// Verifies the representation-preserving reads that unknown-data retention depends on.
/// </summary>
/// <typeparam name="TEncoder">The format's encoder type.</typeparam>
/// <typeparam name="TDecoder">The format's decoder type.</typeparam>
internal sealed class DynamicSuite<TEncoder, TDecoder> : IConformanceSuite<TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public ConformanceCategory Category => ConformanceCategory.Dynamic;

	/// <inheritdoc/>
	public void AddTests(ConformanceTestCollector<TEncoder, TDecoder> collector)
	{
		Requires.NotNull(collector);

		string? skipReason = collector.Options.SupportsDynamicValues ? null : "The format does not support dynamic values.";

		collector.Add("ReadDynamicNumber_PositiveInteger", skipReason, adapter =>
		{
			ShapeShiftNumber number = ScalarHarness.Roundtrip(
				adapter,
				static (ref TEncoder encoder) => encoder.WriteValue(123L),
				static (ref TDecoder decoder) => decoder.ReadDynamicNumber());
			ConformanceAssert.Equal(123m, ToDecimal(number), "a dynamically read positive integer");
		});

		collector.Add("ReadDynamicNumber_NegativeInteger", skipReason, adapter =>
		{
			ShapeShiftNumber number = ScalarHarness.Roundtrip(
				adapter,
				static (ref TEncoder encoder) => encoder.WriteValue(-456L),
				static (ref TDecoder decoder) => decoder.ReadDynamicNumber());
			ConformanceAssert.Equal(-456m, ToDecimal(number), "a dynamically read negative integer");
		});

		collector.Add("ReadDynamicNumber_Fractional", skipReason, adapter =>
		{
			ShapeShiftNumber number = ScalarHarness.Roundtrip(
				adapter,
				static (ref TEncoder encoder) => encoder.WriteValue(1.5d),
				static (ref TDecoder decoder) => decoder.ReadDynamicNumber());
			ConformanceAssert.Equal(1.5m, ToDecimal(number), "a dynamically read fractional number");
		});

		collector.Add("ReadDynamicNumberConsumesTheToken", skipReason, adapter =>
		{
			byte[] payload = adapter.Encode(static (ref TEncoder encoder) =>
			{
				encoder.WriteStartVector(2);
				encoder.WriteValue(1L);
				encoder.WriteValue("tail");
				encoder.WriteEndVector();
			});

			adapter.Decode(payload, static (ref TDecoder decoder) =>
			{
				decoder.ReadStartVector();
				_ = decoder.ReadDynamicNumber();
				ConformanceAssert.Equal("tail", decoder.ReadString(), "the element after a dynamically read number");
				decoder.ReadEndVector();
			});
		});

		collector.Add("ShapeShiftValueScalarsRoundtrip", skipReason, adapter =>
		{
			ShapeShiftSerializer<TEncoder, TDecoder> serializer = adapter.CreateSerializer();
			ITypeShape<ShapeShiftValue> shape = Shapes.Of<ShapeShiftValue, ConformanceWitness>();

			ConformanceAssert.Equal(ShapeShiftValue.Null, adapter.Roundtrip(serializer, ShapeShiftValue.Null, shape), "a dynamic null");
			ConformanceAssert.Equal((ShapeShiftValue)"text", adapter.Roundtrip(serializer, (ShapeShiftValue)"text", shape), "a dynamic string");
			ConformanceAssert.Equal((ShapeShiftValue)true, adapter.Roundtrip(serializer, (ShapeShiftValue)true, shape), "a dynamic boolean");
		});

		collector.Add("ShapeShiftValueTreeRoundtrips", skipReason, adapter =>
		{
			ShapeShiftSerializer<TEncoder, TDecoder> serializer = adapter.CreateSerializer();
			ITypeShape<ShapeShiftValue> shape = Shapes.Of<ShapeShiftValue, ConformanceWitness>();

			ShapeShiftValue original = new ShapeShiftMap(new Dictionary<string, ShapeShiftValue>(StringComparer.Ordinal)
			{
				["text"] = "value",
				["nested"] = new ShapeShiftMap(new Dictionary<string, ShapeShiftValue>(StringComparer.Ordinal)
				{
					["flag"] = true,
				}),
				["items"] = new ShapeShiftArray([ShapeShiftValue.Null, "one"]),
			});

			ShapeShiftValue? roundtripped = adapter.Roundtrip(serializer, original, shape);
			ConformanceAssert.True(roundtripped is ShapeShiftMap, "A dynamic map should read back as a map.");
			ShapeShiftMap map = (ShapeShiftMap)roundtripped!;
			ConformanceAssert.Equal(3, map.Properties.Count, "the property count of a round-tripped dynamic map");
			ConformanceAssert.Equal((ShapeShiftValue)"value", map.Properties["text"], "a dynamic map's string member");
			ConformanceAssert.True(map.Properties["nested"] is ShapeShiftMap, "A nested dynamic map should read back as a map.");
			ConformanceAssert.True(map.Properties["items"] is ShapeShiftArray, "A nested dynamic array should read back as an array.");
		});

		string? binarySkipReason = collector.Options.SupportsDynamicValues && collector.Options.SupportsBinary
			? null
			: skipReason ?? "The format has no binary representation.";
		collector.Add(
			"ShapeShiftValueBinaryRoundtrips",
			binarySkipReason,
			adapter =>
			{
				ShapeShiftSerializer<TEncoder, TDecoder> serializer = adapter.CreateSerializer();
				ITypeShape<ShapeShiftValue> shape = Shapes.Of<ShapeShiftValue, ConformanceWitness>();

				ShapeShiftValue original = new ShapeShiftBinary(new byte[] { 1, 2, 3, 4 });
				ShapeShiftValue? roundtripped = adapter.Roundtrip(serializer, original, shape);
				ConformanceAssert.True(roundtripped is ShapeShiftBinary, "A dynamic binary value should read back as binary.");
				ConformanceAssert.EqualBytes(new byte[] { 1, 2, 3, 4 }, ((ShapeShiftBinary)roundtripped!).Value.Span, "a round-tripped dynamic binary value");
			});
	}

	private static decimal ToDecimal(ShapeShiftNumber number) => number switch
	{
		ShapeShiftInteger integer => integer.Value,
		ShapeShiftUnsignedInteger unsigned => unsigned.Value,
		ShapeShiftBigInteger big => (decimal)big.Value,
		ShapeShiftDecimal dec => dec.Value,
		ShapeShiftFloat f => (decimal)f.Value,
		_ => throw new ConformanceAssertionException($"Unrecognized dynamic number kind {number.GetType().Name}."),
	};
}
