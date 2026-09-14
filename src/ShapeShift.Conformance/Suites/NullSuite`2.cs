// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Conformance.Suites;

/// <summary>
/// Verifies the <see cref="IDecoder.TryReadNull"/> contract.
/// </summary>
/// <typeparam name="TEncoder">The format's encoder type.</typeparam>
/// <typeparam name="TDecoder">The format's decoder type.</typeparam>
/// <remarks>
/// <para>
/// <see cref="IDecoder.TryReadNull"/> has conventional <c>Try</c> semantics: when the next token is
/// <see cref="TokenType.Null"/> it consumes it and reports <see langword="true" />, and when it is
/// anything else it reports <see langword="false" /> having consumed nothing. It is
/// <see cref="IDecoder.ReadNull"/> without the throw.
/// </para>
/// <para>
/// The peek is <see cref="IDecoder.NextTokenType"/>, which never consumes. A converter that wants to
/// know whether a null is coming <em>and still hand the token to someone else</em> asks that instead.
/// </para>
/// <para>
/// The cases below pin both halves down: a <see langword="true" /> answer must leave the decoder past
/// the null -- including the bookkeeping that lets a length-prefixed container synthesize its end
/// token -- and a <see langword="false" /> answer must leave it exactly where it was.
/// </para>
/// </remarks>
internal sealed class NullSuite<TEncoder, TDecoder> : IConformanceSuite<TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public ConformanceCategory Category => ConformanceCategory.Null;

	/// <inheritdoc/>
	public void AddTests(ConformanceTestCollector<TEncoder, TDecoder> collector)
	{
		Requires.NotNull(collector);
		string? unsupported = collector.Options.SupportsNull ? null : "The format has no null representation.";

		collector.Add("NextTokenTypeIsThePeekForNull", unsupported, adapter =>
		{
			byte[] payload = RootHarness.EncodeScalar(adapter, static (ref TEncoder encoder) => encoder.WriteNull());
			RootHarness.DecodeScalar(adapter, payload, (ref TDecoder decoder) =>
			{
				ConformanceAssert.NextToken(TokenType.Null, ref decoder, "a null document");
				ConformanceAssert.NextToken(TokenType.Null, ref decoder, "a null document asked a second time");
				ConformanceAssert.True(decoder.TryReadNull(), "TryReadNull should report true for the null NextTokenType just reported twice.");
				return 0;
			});
		});

		collector.Add("TryReadNullConsumesTheNull", unsupported, adapter =>
		{
			byte[] payload = RootHarness.EncodeVector(adapter, static (ref TEncoder encoder) =>
			{
				encoder.WriteStartVector(2);
				encoder.WriteNull();
				encoder.WriteValue(5L);
				encoder.WriteEndVector();
			});

			RootHarness.DecodeVector(adapter, payload, (ref TDecoder decoder) =>
			{
				decoder.ReadStartVector();
				ConformanceAssert.True(decoder.TryReadNull(), "The first element is null.");
				ConformanceAssert.False(decoder.TryReadNull(), "A true TryReadNull must consume the null, so the next element is no longer a null.");
				ConformanceAssert.Equal(5L, decoder.ReadInt64(), "the element following a consumed null");
				decoder.ReadEndVector();
			});
		});

		collector.Add("TryReadNullConsumesTrailingNull", unsupported, adapter =>
		{
			// A null consumed as the last element of a container is where a decoder that forgets to run
			// its per-value bookkeeping shows up: a length-prefixed format must still be able to
			// synthesize the end token it never wrote.
			byte[] payload = RootHarness.EncodeVector(adapter, static (ref TEncoder encoder) =>
			{
				encoder.WriteStartVector(2);
				encoder.WriteValue(5L);
				encoder.WriteNull();
				encoder.WriteEndVector();
			});

			RootHarness.DecodeVector(adapter, payload, (ref TDecoder decoder) =>
			{
				decoder.ReadStartVector();
				ConformanceAssert.Equal(5L, decoder.ReadInt64(), "the first element");
				ConformanceAssert.True(decoder.TryReadNull(), "The trailing element is null.");
				ConformanceAssert.NextToken(TokenType.EndVector, ref decoder, "after a trailing null was consumed by TryReadNull");
				decoder.ReadEndVector();
			});
		});

		collector.Add("ReadNullConsumes", unsupported, adapter =>
		{
			byte[] payload = RootHarness.EncodeVector(adapter, static (ref TEncoder encoder) =>
			{
				encoder.WriteStartVector(2);
				encoder.WriteNull();
				encoder.WriteValue(5L);
				encoder.WriteEndVector();
			});

			RootHarness.DecodeVector(adapter, payload, (ref TDecoder decoder) =>
			{
				decoder.ReadStartVector();
				decoder.ReadNull();
				ConformanceAssert.False(decoder.TryReadNull(), "ReadNull must consume the null token, so the next element is no longer a null.");
				ConformanceAssert.Equal(5L, decoder.ReadInt64(), "the element following a null");
				decoder.ReadEndVector();
			});
		});

		collector.Add("TryReadNullIsFalseForNonNull", adapter =>
		{
			byte[] payload = RootHarness.EncodeScalar(adapter, static (ref TEncoder encoder) => encoder.WriteValue("text"));
			RootHarness.DecodeScalar(adapter, payload, (ref TDecoder decoder) =>
			{
				ConformanceAssert.False(decoder.TryReadNull(), "TryReadNull should report false for a string token.");
				ConformanceAssert.False(decoder.TryReadNull(), "TryReadNull must not consume anything when it reports false.");
				ConformanceAssert.Equal("text", decoder.ReadString(), "the string that follows two false TryReadNull calls");
				return 0;
			});
		});

		collector.Add("FalseTryReadNullLeavesContainersReadable", adapter =>
		{
			// A false answer must not disturb the decoder even when the next token opens a container,
			// which is the shape a nullable object read takes.
			byte[] payload = RootHarness.EncodeVector(adapter, static (ref TEncoder encoder) =>
			{
				encoder.WriteStartVector(1);
				encoder.WriteValue(7L);
				encoder.WriteEndVector();
			});

			RootHarness.DecodeVector(adapter, payload, (ref TDecoder decoder) =>
			{
				ConformanceAssert.False(decoder.TryReadNull(), "TryReadNull should report false for the start of a vector.");
				ConformanceAssert.NextToken(TokenType.StartVector, ref decoder, "after a false TryReadNull");
				decoder.ReadStartVector();
				ConformanceAssert.Equal(7L, decoder.ReadInt64(), "the only element");
				decoder.ReadEndVector();
			});
		});

		collector.Add("ReadNullRejectsNonNull", adapter =>
		{
			byte[] payload = RootHarness.EncodeScalar(adapter, static (ref TEncoder encoder) => encoder.WriteValue("text"));
			ConformanceAssert.FailsCleanly(
				() => RootHarness.DecodeScalar(adapter, payload, static (ref TDecoder decoder) =>
				{
					decoder.ReadNull();
					return 0;
				}),
				"reading a string as a null");
		});

		collector.Add("NullMapValue", unsupported, adapter =>
		{
			byte[] payload = adapter.Encode(static (ref TEncoder encoder) =>
			{
				encoder.WriteStartMap(2);
				encoder.WritePropertyName("missing");
				encoder.WriteNull();
				encoder.WritePropertyName("present");
				encoder.WriteValue("here");
				encoder.WriteEndMap();
			});

			adapter.Decode(payload, static (ref TDecoder decoder) =>
			{
				decoder.ReadStartMap();
				ConformanceAssert.Equal("missing", decoder.ReadPropertyName().ToString(), "the first map key");
				ConformanceAssert.True(decoder.TryReadNull(), "The first map value is null.");
				ConformanceAssert.Equal("present", decoder.ReadPropertyName().ToString(), "the second map key");
				ConformanceAssert.False(decoder.TryReadNull(), "The second map value is not null.");
				ConformanceAssert.Equal("here", decoder.ReadString(), "the second map value");
				decoder.ReadEndMap();
			});
		});

		collector.AddIf(
			"NullIsSkippable",
			collector.Options.SupportsSkip && collector.Options.SupportsNull,
			collector.Options.SupportsNull ? "The format does not implement Skip." : "The format has no null representation.",
			adapter =>
			{
				byte[] payload = RootHarness.EncodeVector(adapter, static (ref TEncoder encoder) =>
				{
					encoder.WriteStartVector(2);
					encoder.WriteNull();
					encoder.WriteValue(9L);
					encoder.WriteEndVector();
				});

				RootHarness.DecodeVector(adapter, payload, static (ref TDecoder decoder) =>
				{
					decoder.ReadStartVector();
					decoder.Skip();
					ConformanceAssert.Equal(9L, decoder.ReadInt64(), "the element after a skipped null");
					decoder.ReadEndVector();
				});
			});

		collector.Add("NullContainerMemberDeserializesAsNull", unsupported, adapter =>
		{
			ShapeShiftSerializer<TEncoder, TDecoder> serializer = adapter.CreateSerializer();
			byte[] payload = adapter.Encode(static (ref TEncoder encoder) => encoder.WriteNull());
			ConformancePerson? person = adapter.Deserialize(serializer, payload, Shapes.Of<ConformancePerson>());
			ConformanceAssert.True(person is null, "A null document should deserialize into a null reference.");
		});
	}
}
