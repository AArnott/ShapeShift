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
/// <see cref="IDecoder.TryReadNull"/> is a <em>peek</em>: it reports whether the next token is
/// <see cref="TokenType.Null"/> and leaves the decoder exactly where it was, whatever the answer.
/// Every converter in ShapeShift relies on that, because the common shape of a nullable read is to
/// ask, then hand the still-unconsumed token to whichever code path the answer selects.
/// </para>
/// <para>
/// A decoder that consumed the token on a <see langword="true" /> answer would make the very common
/// <c>if (decoder.TryReadNull()) { return null; }</c> pattern silently correct while breaking any
/// converter that peeks first and delegates second. <see cref="IDecoder.ReadNull"/> is the consuming
/// counterpart, and a decoder must override it, because the default interface implementation only
/// validates.
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

		collector.Add("TryReadNullDoesNotConsume", adapter =>
		{
			byte[] payload = RootHarness.EncodeScalar(adapter, static (ref TEncoder encoder) => encoder.WriteNull());
			RootHarness.DecodeScalar(adapter, payload, (ref TDecoder decoder) =>
			{
				ConformanceAssert.True(decoder.TryReadNull(), "TryReadNull should report true for a null token.");
				ConformanceAssert.True(decoder.TryReadNull(), "TryReadNull must not consume the null token, so a second call should also report true.");
				ConformanceAssert.NextToken(TokenType.Null, ref decoder, "after two TryReadNull calls");
				return 0;
			});
		});

		collector.Add("ReadNullConsumes", adapter =>
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

		collector.Add("NullMapValue", adapter =>
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
				decoder.ReadNull();
				ConformanceAssert.Equal("present", decoder.ReadPropertyName().ToString(), "the second map key");
				ConformanceAssert.False(decoder.TryReadNull(), "The second map value is not null.");
				ConformanceAssert.Equal("here", decoder.ReadString(), "the second map value");
				decoder.ReadEndMap();
			});
		});

		collector.AddIf(
			"NullIsSkippable",
			collector.Options.SupportsSkip,
			"The format does not implement Skip.",
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

		collector.Add("NullContainerMemberDeserializesAsNull", adapter =>
		{
			ShapeShiftSerializer<TEncoder, TDecoder> serializer = adapter.CreateSerializer();
			byte[] payload = adapter.Encode(static (ref TEncoder encoder) => encoder.WriteNull());
			ConformancePerson? person = adapter.Deserialize(serializer, payload, Shapes.Of<ConformancePerson>());
			ConformanceAssert.True(person is null, "A null document should deserialize into a null reference.");
		});
	}
}
