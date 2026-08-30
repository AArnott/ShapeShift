// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Conformance.Suites;

/// <summary>
/// Verifies the container state machine: start and end tokens pair up, counts are honest,
/// and the decoder is left where the next read expects it.
/// </summary>
/// <typeparam name="TEncoder">The format's encoder type.</typeparam>
/// <typeparam name="TDecoder">The format's decoder type.</typeparam>
internal sealed class StateSuite<TEncoder, TDecoder> : IConformanceSuite<TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public ConformanceCategory Category => ConformanceCategory.State;

	/// <inheritdoc/>
	public void AddTests(ConformanceTestCollector<TEncoder, TDecoder> collector)
	{
		Requires.NotNull(collector);

		collector.AddIf(
			"EmptyMap",
			collector.Options.SupportsEmptyMaps,
			"The format cannot represent an empty map.",
			adapter =>
			{
				byte[] payload = adapter.Encode(static (ref TEncoder encoder) =>
				{
					encoder.WriteStartMap(0);
					encoder.WriteEndMap();
				});

				adapter.Decode(payload, (ref TDecoder decoder) =>
				{
					int? count = decoder.ReadStartMap();
					AssertCount(adapter, count, 0, "an empty map");
					ConformanceAssert.NextToken(TokenType.EndMap, ref decoder, "immediately inside an empty map");
					decoder.ReadEndMap();
				});
			});

		collector.AddIf(
			"EmptyVector",
			collector.Options.SupportsEmptyVectors,
			"The format cannot represent an empty vector.",
			adapter =>
			{
				byte[] payload = RootHarness.EncodeVector(adapter, static (ref TEncoder encoder) =>
				{
					encoder.WriteStartVector(0);
					encoder.WriteEndVector();
				});

				RootHarness.DecodeVector(adapter, payload, (ref TDecoder decoder) =>
				{
					int? count = decoder.ReadStartVector();
					AssertCount(adapter, count, 0, "an empty vector");
					ConformanceAssert.NextToken(TokenType.EndVector, ref decoder, "immediately inside an empty vector");
					decoder.ReadEndVector();
				});
			});

		collector.Add("MapEntriesAreOrdered", adapter =>
		{
			byte[] payload = adapter.Encode(static (ref TEncoder encoder) =>
			{
				encoder.WriteStartMap(3);
				encoder.WritePropertyName("one");
				encoder.WriteValue(1L);
				encoder.WritePropertyName("two");
				encoder.WriteValue(2L);
				encoder.WritePropertyName("three");
				encoder.WriteValue(3L);
				encoder.WriteEndMap();
			});

			adapter.Decode(payload, (ref TDecoder decoder) =>
			{
				int? count = decoder.ReadStartMap();
				AssertCount(adapter, count, 3, "a three-entry map");

				string[] expectedNames = ["one", "two", "three"];
				for (int i = 0; i < expectedNames.Length; i++)
				{
					ConformanceAssert.NextToken(TokenType.PropertyName, ref decoder, $"map key {i}");
					ConformanceAssert.Equal(expectedNames[i], decoder.ReadPropertyName().ToString(), $"map key {i}");
					ConformanceAssert.Equal(i + 1L, decoder.ReadInt64(), $"map value {i}");
				}

				ConformanceAssert.NextToken(TokenType.EndMap, ref decoder, "after every map entry is read");
				decoder.ReadEndMap();
			});
		});

		collector.Add("VectorElementsAreOrdered", adapter =>
		{
			byte[] payload = RootHarness.EncodeVector(adapter, static (ref TEncoder encoder) =>
			{
				encoder.WriteStartVector(3);
				encoder.WriteValue("a");
				encoder.WriteValue("b");
				encoder.WriteValue("c");
				encoder.WriteEndVector();
			});

			RootHarness.DecodeVector(adapter, payload, (ref TDecoder decoder) =>
			{
				int? count = decoder.ReadStartVector();
				AssertCount(adapter, count, 3, "a three-element vector");

				string[] expected = ["a", "b", "c"];
				for (int i = 0; i < expected.Length; i++)
				{
					ConformanceAssert.Equal(expected[i], decoder.ReadString(), $"vector element {i}");
				}

				ConformanceAssert.NextToken(TokenType.EndVector, ref decoder, "after every vector element is read");
				decoder.ReadEndVector();
			});
		});

		collector.Add("MapInsideMap", adapter =>
		{
			byte[] payload = adapter.Encode(static (ref TEncoder encoder) =>
			{
				encoder.WriteStartMap(2);
				encoder.WritePropertyName("inner");
				encoder.WriteStartMap(1);
				encoder.WritePropertyName("leaf");
				encoder.WriteValue(1L);
				encoder.WriteEndMap();
				encoder.WritePropertyName("after");
				encoder.WriteValue(2L);
				encoder.WriteEndMap();
			});

			adapter.Decode(payload, static (ref TDecoder decoder) =>
			{
				decoder.ReadStartMap();
				ConformanceAssert.Equal("inner", decoder.ReadPropertyName().ToString(), "the outer map's first key");
				decoder.ReadStartMap();
				ConformanceAssert.Equal("leaf", decoder.ReadPropertyName().ToString(), "the inner map's only key");
				ConformanceAssert.Equal(1L, decoder.ReadInt64(), "the inner map's only value");
				ConformanceAssert.NextToken(TokenType.EndMap, ref decoder, "the end of the inner map");
				decoder.ReadEndMap();
				ConformanceAssert.Equal("after", decoder.ReadPropertyName().ToString(), "the key following a nested map");
				ConformanceAssert.Equal(2L, decoder.ReadInt64(), "the value following a nested map");
				decoder.ReadEndMap();
			});
		});

		collector.Add("MapInsideVector", adapter =>
		{
			byte[] payload = RootHarness.EncodeVector(adapter, static (ref TEncoder encoder) =>
			{
				encoder.WriteStartVector(2);
				encoder.WriteStartMap(1);
				encoder.WritePropertyName("n");
				encoder.WriteValue(1L);
				encoder.WriteEndMap();
				encoder.WriteStartMap(1);
				encoder.WritePropertyName("n");
				encoder.WriteValue(2L);
				encoder.WriteEndMap();
				encoder.WriteEndVector();
			});

			RootHarness.DecodeVector(adapter, payload, static (ref TDecoder decoder) =>
			{
				decoder.ReadStartVector();
				for (int i = 1; i <= 2; i++)
				{
					ConformanceAssert.NextToken(TokenType.StartMap, ref decoder, $"vector element {i - 1}");
					decoder.ReadStartMap();
					ConformanceAssert.Equal("n", decoder.ReadPropertyName().ToString(), $"the key of vector element {i - 1}");
					ConformanceAssert.Equal((long)i, decoder.ReadInt64(), $"the value of vector element {i - 1}");
					decoder.ReadEndMap();
				}

				ConformanceAssert.NextToken(TokenType.EndVector, ref decoder, "the end of a vector of maps");
				decoder.ReadEndVector();
			});
		});

		collector.AddIf(
			"VectorInsideVector",
			collector.Options.SupportsNestedVectors,
			"The format cannot nest a vector directly inside a vector.",
			adapter =>
			{
				byte[] payload = RootHarness.EncodeVector(adapter, static (ref TEncoder encoder) =>
				{
					encoder.WriteStartVector(2);
					encoder.WriteStartVector(1);
					encoder.WriteValue(1L);
					encoder.WriteEndVector();
					encoder.WriteStartVector(2);
					encoder.WriteValue(2L);
					encoder.WriteValue(3L);
					encoder.WriteEndVector();
					encoder.WriteEndVector();
				});

				RootHarness.DecodeVector(adapter, payload, static (ref TDecoder decoder) =>
				{
					decoder.ReadStartVector();
					decoder.ReadStartVector();
					ConformanceAssert.Equal(1L, decoder.ReadInt64(), "the only element of the first inner vector");
					decoder.ReadEndVector();
					decoder.ReadStartVector();
					ConformanceAssert.Equal(2L, decoder.ReadInt64(), "the first element of the second inner vector");
					ConformanceAssert.Equal(3L, decoder.ReadInt64(), "the second element of the second inner vector");
					decoder.ReadEndVector();
					ConformanceAssert.NextToken(TokenType.EndVector, ref decoder, "the end of the outer vector");
					decoder.ReadEndVector();
				});
			});

		collector.Add("DeepNesting", adapter =>
		{
			int depth = Math.Max(2, adapter.Options.MaxTestedNestingDepth);
			byte[] payload = adapter.Encode((ref TEncoder encoder) =>
			{
				for (int i = 0; i < depth; i++)
				{
					encoder.WriteStartMap(1);
					encoder.WritePropertyName("n");
				}

				encoder.WriteValue(1L);

				for (int i = 0; i < depth; i++)
				{
					encoder.WriteEndMap();
				}
			});

			adapter.Decode(payload, (ref TDecoder decoder) =>
			{
				for (int i = 0; i < depth; i++)
				{
					decoder.ReadStartMap();
					ConformanceAssert.Equal("n", decoder.ReadPropertyName().ToString(), $"the key at depth {i}");
				}

				ConformanceAssert.Equal(1L, decoder.ReadInt64(), $"the value at depth {depth}");

				for (int i = 0; i < depth; i++)
				{
					decoder.ReadEndMap();
				}
			});
		});

		collector.Add("MismatchedEndTokenIsRejected", adapter =>
		{
			byte[] payload = adapter.Encode(static (ref TEncoder encoder) =>
			{
				encoder.WriteStartMap(1);
				encoder.WritePropertyName("a");
				encoder.WriteValue(1L);
				encoder.WriteEndMap();
			});

			ConformanceAssert.FailsCleanly(
				() => adapter.Decode(payload, static (ref TDecoder decoder) =>
				{
					decoder.ReadStartMap();
					_ = decoder.ReadPropertyName();
					_ = decoder.ReadInt64();
					decoder.ReadEndVector();
					return 0;
				}),
				"closing a map with ReadEndVector");
		});

		collector.Add("ReadingPastTheEndIsRejected", adapter =>
		{
			byte[] payload = RootHarness.EncodeScalar(adapter, static (ref TEncoder encoder) => encoder.WriteValue(1L));
			ConformanceAssert.FailsCleanly(
				() => RootHarness.DecodeScalar(adapter, payload, static (ref TDecoder decoder) =>
				{
					_ = decoder.ReadInt64();
					return decoder.ReadInt64();
				}),
				"reading a second value from a single-value document");
		});
	}

	private static void AssertCount(FormatConformanceAdapter<TEncoder, TDecoder> adapter, int? actual, int expected, string context)
	{
		if (adapter.Options.ReportsContainerCounts)
		{
			ConformanceAssert.Equal(expected, actual, $"the reported element count of {context}");
		}
		else if (actual is not null)
		{
			ConformanceAssert.Equal(expected, actual.Value, $"the reported element count of {context} (the format returned a count even though it does not declare ReportsContainerCounts, so the count must still be correct)");
		}
	}
}
