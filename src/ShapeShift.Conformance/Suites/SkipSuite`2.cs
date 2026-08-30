// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Conformance.Suites;

/// <summary>
/// Verifies <see cref="IDecoder.Skip"/>, which unknown-property retention, positional contracts,
/// and path traversal all build on.
/// </summary>
/// <typeparam name="TEncoder">The format's encoder type.</typeparam>
/// <typeparam name="TDecoder">The format's decoder type.</typeparam>
internal sealed class SkipSuite<TEncoder, TDecoder> : IConformanceSuite<TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public ConformanceCategory Category => ConformanceCategory.Skip;

	/// <inheritdoc/>
	public void AddTests(ConformanceTestCollector<TEncoder, TDecoder> collector)
	{
		Requires.NotNull(collector);

		string? skipReason = collector.Options.SupportsSkip ? null : "The format does not implement Skip.";

		collector.Add("SkipScalarLeavesSuccessor", skipReason, adapter =>
		{
			byte[] payload = adapter.Encode(static (ref TEncoder encoder) =>
			{
				encoder.WriteStartVector(2);
				encoder.WriteValue("skipped");
				encoder.WriteValue("kept");
				encoder.WriteEndVector();
			});

			adapter.Decode(payload, static (ref TDecoder decoder) =>
			{
				decoder.ReadStartVector();
				decoder.Skip();
				ConformanceAssert.Equal("kept", decoder.ReadString(), "the element after a skipped scalar");
				decoder.ReadEndVector();
			});
		});

		collector.Add("SkipMapValue", skipReason, adapter =>
		{
			byte[] payload = adapter.Encode(static (ref TEncoder encoder) =>
			{
				encoder.WriteStartMap(2);
				encoder.WritePropertyName("unknown");
				encoder.WriteStartMap(2);
				encoder.WritePropertyName("a");
				encoder.WriteValue(1L);
				encoder.WritePropertyName("b");
				encoder.WriteStartVector(2);
				encoder.WriteValue(2L);
				encoder.WriteValue(3L);
				encoder.WriteEndVector();
				encoder.WriteEndMap();
				encoder.WritePropertyName("known");
				encoder.WriteValue("value");
				encoder.WriteEndMap();
			});

			adapter.Decode(payload, static (ref TDecoder decoder) =>
			{
				decoder.ReadStartMap();
				ConformanceAssert.Equal("unknown", decoder.ReadPropertyName().ToString(), "the first key");
				decoder.Skip();
				ConformanceAssert.NextToken(TokenType.PropertyName, ref decoder, "after skipping a nested map value");
				ConformanceAssert.Equal("known", decoder.ReadPropertyName().ToString(), "the key after a skipped value");
				ConformanceAssert.Equal("value", decoder.ReadString(), "the value after a skipped value");
				ConformanceAssert.NextToken(TokenType.EndMap, ref decoder, "the end of the map");
				decoder.ReadEndMap();
			});
		});

		collector.Add("SkipVectorElement", skipReason, adapter =>
		{
			byte[] payload = adapter.Encode(static (ref TEncoder encoder) =>
			{
				encoder.WriteStartVector(2);
				encoder.WriteStartMap(1);
				encoder.WritePropertyName("a");
				encoder.WriteValue(1L);
				encoder.WriteEndMap();
				encoder.WriteValue("last");
				encoder.WriteEndVector();
			});

			adapter.Decode(payload, static (ref TDecoder decoder) =>
			{
				decoder.ReadStartVector();
				decoder.Skip();
				ConformanceAssert.Equal("last", decoder.ReadString(), "the element after a skipped map");
				decoder.ReadEndVector();
			});
		});

		collector.Add("SkipEveryRemainingEntry", skipReason, adapter =>
		{
			byte[] payload = adapter.Encode(static (ref TEncoder encoder) =>
			{
				encoder.WriteStartMap(3);
				encoder.WritePropertyName("a");
				encoder.WriteValue(1L);
				encoder.WritePropertyName("b");
				encoder.WriteValue("two");
				encoder.WritePropertyName("c");
				encoder.WriteNull();
				encoder.WriteEndMap();
			});

			adapter.Decode(payload, static (ref TDecoder decoder) =>
			{
				decoder.ReadStartMap();
				int skipped = 0;
				while (decoder.NextTokenType != TokenType.EndMap)
				{
					_ = decoder.ReadPropertyName();
					decoder.Skip();
					skipped++;
				}

				ConformanceAssert.Equal(3, skipped, "the number of entries skipped in a three-entry map");
				decoder.ReadEndMap();
			});
		});

		collector.Add("SkipNestedContainers", skipReason, adapter =>
		{
			int depth = Math.Max(2, Math.Min(8, adapter.Options.MaxTestedNestingDepth));
			byte[] payload = adapter.Encode((ref TEncoder encoder) =>
			{
				encoder.WriteStartVector(2);
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

				encoder.WriteValue("tail");
				encoder.WriteEndVector();
			});

			adapter.Decode(payload, static (ref TDecoder decoder) =>
			{
				decoder.ReadStartVector();
				decoder.Skip();
				ConformanceAssert.Equal("tail", decoder.ReadString(), "the element after a deeply nested skipped value");
				decoder.ReadEndVector();
			});
		});

		collector.Add("SkipWholeDocument", skipReason, adapter =>
		{
			byte[] payload = adapter.Encode(static (ref TEncoder encoder) =>
			{
				encoder.WriteStartMap(1);
				encoder.WritePropertyName("a");
				encoder.WriteStartVector(2);
				encoder.WriteValue(1L);
				encoder.WriteValue(2L);
				encoder.WriteEndVector();
				encoder.WriteEndMap();
			});

			adapter.Decode(payload, (ref TDecoder decoder) =>
			{
				decoder.Skip();
				if (adapter.Options.ReportsEndDocument)
				{
					ConformanceAssert.NextToken(TokenType.EndDocument, ref decoder, "after skipping the whole document");
				}
			});
		});
	}
}
