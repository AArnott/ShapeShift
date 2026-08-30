// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Conformance.Suites;

/// <summary>
/// Verifies that the security limits carried on <see cref="SerializationContext{TEncoder, TDecoder}"/>
/// are honored for this format.
/// </summary>
/// <typeparam name="TEncoder">The format's encoder type.</typeparam>
/// <typeparam name="TDecoder">The format's decoder type.</typeparam>
/// <remarks>
/// The limits are enforced by the format-neutral converters, so a format normally gets them for free.
/// These cases exist to catch a format whose own converters or optimized hooks read a length-prefixed
/// value before the shared code gets a chance to reject it.
/// </remarks>
internal sealed class LimitsSuite<TEncoder, TDecoder> : IConformanceSuite<TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public ConformanceCategory Category => ConformanceCategory.Limits;

	/// <inheritdoc/>
	public void AddTests(ConformanceTestCollector<TEncoder, TDecoder> collector)
	{
		Requires.NotNull(collector);

		collector.Add("MaxDepthIsEnforcedOnRead", adapter =>
		{
			const int Depth = 12;
			ShapeShiftSerializer<TEncoder, TDecoder> serializer = WithLimits(adapter, maxDepth: Depth / 2);
			byte[] payload = adapter.Encode(static (ref TEncoder encoder) =>
			{
				for (int i = 0; i < Depth; i++)
				{
					encoder.WriteStartMap(1);
					encoder.WritePropertyName("Child");
				}

				encoder.WriteNull();

				for (int i = 0; i < Depth; i++)
				{
					encoder.WriteEndMap();
				}
			});

			ConformanceAssert.FailsCleanly(
				() => adapter.Deserialize(serializer, payload, Shapes.Of<ConformanceNode>()),
				$"deserializing a {Depth}-deep document with MaxDepth set to {Depth / 2}");
		});

		collector.Add("MaxDepthIsEnforcedOnWrite", adapter =>
		{
			const int Depth = 12;
			ShapeShiftSerializer<TEncoder, TDecoder> serializer = WithLimits(adapter, maxDepth: Depth / 2);

			ConformanceNode root = new();
			ConformanceNode current = root;
			for (int i = 1; i < Depth; i++)
			{
				current.Child = new ConformanceNode();
				current = current.Child;
			}

			ConformanceAssert.FailsCleanly(
				() => adapter.Serialize(serializer, root, Shapes.Of<ConformanceNode>()),
				$"serializing a {Depth}-deep graph with MaxDepth set to {Depth / 2}");
		});

		collector.Add("MaxCollectionLengthIsEnforcedOnRead", adapter =>
		{
			const int Count = 64;
			ShapeShiftSerializer<TEncoder, TDecoder> serializer = WithLimits(adapter, maxCollectionLength: Count / 2);
			byte[] payload = adapter.Encode(static (ref TEncoder encoder) =>
			{
				encoder.WriteStartVector(Count);
				for (int i = 0; i < Count; i++)
				{
					encoder.WriteValue((long)i);
				}

				encoder.WriteEndVector();
			});

			ConformanceAssert.FailsCleanly(
				() => adapter.Deserialize(serializer, payload, Shapes.Of<List<int>, ConformanceWitness>()),
				$"deserializing a {Count}-element vector with MaxCollectionLength set to {Count / 2}");
		});

		collector.Add("MaxCollectionLengthIsEnforcedOnWrite", adapter =>
		{
			const int Count = 64;
			ShapeShiftSerializer<TEncoder, TDecoder> serializer = WithLimits(adapter, maxCollectionLength: Count / 2);
			List<int> values = [.. Enumerable.Range(0, Count)];

			ConformanceAssert.FailsCleanly(
				() => adapter.Serialize(serializer, values, Shapes.Of<List<int>, ConformanceWitness>()),
				$"serializing a {Count}-element list with MaxCollectionLength set to {Count / 2}");
		});

		collector.Add("MaxStringLengthIsEnforcedOnRead", adapter =>
		{
			const int Length = 512;
			ShapeShiftSerializer<TEncoder, TDecoder> serializer = WithLimits(adapter, maxStringLength: Length / 2);
			byte[] payload = ScalarHarness.Encode(adapter, static (ref TEncoder encoder) => encoder.WriteValue(new string('x', Length)));

			ConformanceAssert.FailsCleanly(
				() => adapter.Deserialize(serializer, payload, Shapes.Of<string, ConformanceWitness>()),
				$"deserializing a {Length}-character string with MaxStringLength set to {Length / 2}");
		});

		collector.Add(
			"MaxBinaryLengthIsEnforcedOnRead",
			collector.Options.SupportsBinary ? null : "The format has no binary representation.",
			adapter =>
			{
				const int Length = 512;
				ShapeShiftSerializer<TEncoder, TDecoder> serializer = WithLimits(adapter, maxBinaryLength: Length / 2);
				byte[] payload = ScalarHarness.Encode(adapter, static (ref TEncoder encoder) => encoder.WriteValue(new byte[Length].AsSpan()));

				ConformanceAssert.FailsCleanly(
					() => adapter.Deserialize(serializer, payload, Shapes.Of<byte[], ConformanceWitness>()),
					$"deserializing a {Length}-byte binary value with MaxBinaryLength set to {Length / 2}");
			});

		collector.Add("LimitsPermitValuesAtTheBoundary", adapter =>
		{
			const int Count = 8;
			ShapeShiftSerializer<TEncoder, TDecoder> serializer = WithLimits(adapter, maxDepth: 8, maxCollectionLength: Count, maxStringLength: 4);

			List<string> values = [.. Enumerable.Repeat("abcd", Count)];
			List<string>? roundtripped = adapter.Roundtrip(serializer, values, Shapes.Of<List<string>, ConformanceWitness>());
			ConformanceAssert.Equal(Count, roundtripped?.Count ?? -1, "the element count of a collection exactly at MaxCollectionLength");
		});
	}

	private static ShapeShiftSerializer<TEncoder, TDecoder> WithLimits(
		FormatConformanceAdapter<TEncoder, TDecoder> adapter,
		int? maxDepth = null,
		int? maxCollectionLength = null,
		int? maxStringLength = null,
		int? maxBinaryLength = null)
	{
		ShapeShiftSerializer<TEncoder, TDecoder> serializer = adapter.CreateSerializer();
		SerializationContext<TEncoder, TDecoder> context = serializer.StartingContext;

		if (maxDepth is int depth)
		{
			context.MaxDepth = depth;
		}

		if (maxCollectionLength is int collectionLength)
		{
			context.MaxCollectionLength = collectionLength;
		}

		if (maxStringLength is int stringLength)
		{
			context.MaxStringLength = stringLength;
		}

		if (maxBinaryLength is int binaryLength)
		{
			context.MaxBinaryLength = binaryLength;
		}

		return serializer with { StartingContext = context };
	}
}
