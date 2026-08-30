// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Conformance.Suites;

/// <summary>
/// Verifies the <see cref="TokenType"/> a decoder reports before each kind of value is read,
/// which is what every converter dispatches on.
/// </summary>
/// <typeparam name="TEncoder">The format's encoder type.</typeparam>
/// <typeparam name="TDecoder">The format's decoder type.</typeparam>
internal sealed class TokenSuite<TEncoder, TDecoder> : IConformanceSuite<TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public ConformanceCategory Category => ConformanceCategory.Tokens;

	/// <inheritdoc/>
	public void AddTests(ConformanceTestCollector<TEncoder, TDecoder> collector)
	{
		Requires.NotNull(collector);

		AddScalar(collector, "Null", ConformanceValueKind.Null, static (ref TEncoder e) => e.WriteNull(), null);
		AddScalar(collector, "Boolean", ConformanceValueKind.Boolean, static (ref TEncoder e) => e.WriteValue(true), null);
		AddScalar(collector, "Integer", ConformanceValueKind.Integer, static (ref TEncoder e) => e.WriteValue(42L), null);
		AddScalar(collector, "Float", ConformanceValueKind.Float, static (ref TEncoder e) => e.WriteValue(1.5d), null);
		AddScalar(collector, "String", ConformanceValueKind.String, static (ref TEncoder e) => e.WriteValue("text"), null);
		AddScalar(
			collector,
			"Binary",
			ConformanceValueKind.Binary,
			static (ref TEncoder e) => e.WriteValue(new byte[] { 1, 2, 3 }.AsSpan()),
			collector.Options.SupportsBinary ? null : "The format has no binary representation.");
		AddScalar(
			collector,
			"DateTime",
			ConformanceValueKind.DateTime,
			static (ref TEncoder e) => e.WriteValue(new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc)),
			collector.Options.SupportsDateTime ? null : "The format cannot represent a date and time.");
		AddScalar(
			collector,
			"TimeSpan",
			ConformanceValueKind.TimeSpan,
			static (ref TEncoder e) => e.WriteValue(TimeSpan.FromMinutes(90)),
			collector.Options.SupportsTimeSpan ? null : "The format cannot represent a duration.");

		collector.Add("MapStartAndEnd", adapter =>
		{
			byte[] payload = adapter.Encode(static (ref TEncoder encoder) =>
			{
				encoder.WriteStartMap(1);
				encoder.WritePropertyName("a");
				encoder.WriteValue(1L);
				encoder.WriteEndMap();
			});

			adapter.Decode(payload, (ref TDecoder decoder) =>
			{
				ConformanceAssert.NextToken(adapter.GetExpectedTokenType(ConformanceValueKind.Map), ref decoder, "the start of a map");
				decoder.ReadStartMap();
				ConformanceAssert.NextToken(TokenType.PropertyName, ref decoder, "the first map key");
				ConformanceAssert.Equal("a", decoder.ReadPropertyName().ToString(), "the first map key");
				ConformanceAssert.NextToken(adapter.GetExpectedTokenType(ConformanceValueKind.Integer), ref decoder, "the first map value");
				ConformanceAssert.Equal(1L, decoder.ReadInt64(), "the first map value");
				ConformanceAssert.NextToken(TokenType.EndMap, ref decoder, "the end of a one-entry map");
				decoder.ReadEndMap();
			});
		});

		collector.Add("VectorStartAndEnd", adapter =>
		{
			byte[] payload = RootHarness.EncodeVector(adapter, static (ref TEncoder encoder) =>
			{
				encoder.WriteStartVector(1);
				encoder.WriteValue(7L);
				encoder.WriteEndVector();
			});

			RootHarness.DecodeVector(adapter, payload, (ref TDecoder decoder) =>
			{
				ConformanceAssert.NextToken(adapter.GetExpectedTokenType(ConformanceValueKind.Vector), ref decoder, "the start of a vector");
				decoder.ReadStartVector();
				ConformanceAssert.NextToken(adapter.GetExpectedTokenType(ConformanceValueKind.Integer), ref decoder, "the first vector element");
				ConformanceAssert.Equal(7L, decoder.ReadInt64(), "the first vector element");
				ConformanceAssert.NextToken(TokenType.EndVector, ref decoder, "the end of a one-element vector");
				decoder.ReadEndVector();
			});
		});

		collector.AddIf(
			"EndDocumentAfterRootValue",
			collector.Options.ReportsEndDocument,
			"The format does not report an end-of-document token.",
			adapter =>
			{
				byte[] payload = adapter.Encode(static (ref TEncoder encoder) =>
				{
					encoder.WriteStartMap(1);
					encoder.WritePropertyName("a");
					encoder.WriteValue(1L);
					encoder.WriteEndMap();
				});

				adapter.Decode(payload, static (ref TDecoder decoder) =>
				{
					decoder.ReadStartMap();
					_ = decoder.ReadPropertyName();
					_ = decoder.ReadInt64();
					decoder.ReadEndMap();
					ConformanceAssert.NextToken(TokenType.EndDocument, ref decoder, "after the root map is consumed");
				});
			});

		collector.Add("NextTokenTypeIsRepeatable", adapter =>
		{
			byte[] payload = RootHarness.EncodeScalar(adapter, static (ref TEncoder encoder) => encoder.WriteValue("text"));
			RootHarness.DecodeScalar(adapter, payload, (ref TDecoder decoder) =>
			{
				TokenType first = decoder.NextTokenType;
				TokenType second = decoder.NextTokenType;
				TokenType third = decoder.NextTokenType;
				ConformanceAssert.Equal(first, second, "the second read of NextTokenType");
				ConformanceAssert.Equal(first, third, "the third read of NextTokenType");
				ConformanceAssert.Equal(adapter.GetExpectedTokenType(ConformanceValueKind.String), first, "the token type of a string");
				return 0;
			});
		});
	}

	private static void AddScalar(
		ConformanceTestCollector<TEncoder, TDecoder> collector,
		string name,
		ConformanceValueKind kind,
		EncodeAction<TEncoder> write,
		string? skipReason)
	{
		collector.Add(name, skipReason, adapter =>
		{
			byte[] payload = RootHarness.EncodeScalar(adapter, write);
			RootHarness.DecodeScalar(adapter, payload, (ref TDecoder decoder) =>
			{
				ConformanceAssert.NextToken(adapter.GetExpectedTokenType(kind), ref decoder, $"a {kind} value");
				return 0;
			});
		});
	}
}
