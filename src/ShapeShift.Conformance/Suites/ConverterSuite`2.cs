// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Conformance.Suites;

/// <summary>
/// Verifies that the format composes with the serializer features that build on top of it:
/// user converters, reference preservation, naming, and default-value policies.
/// </summary>
/// <typeparam name="TEncoder">The format's encoder type.</typeparam>
/// <typeparam name="TDecoder">The format's decoder type.</typeparam>
internal sealed class ConverterSuite<TEncoder, TDecoder> : IConformanceSuite<TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public ConformanceCategory Category => ConformanceCategory.Converters;

	/// <inheritdoc/>
	public void AddTests(ConformanceTestCollector<TEncoder, TDecoder> collector)
	{
		Requires.NotNull(collector);

		collector.Add("ObjectRoundtrip", adapter =>
		{
			ShapeShiftSerializer<TEncoder, TDecoder> serializer = adapter.CreateSerializer();
			ConformancePerson original = new("Ada", 36, [1, 2, 3]);
			ConformancePerson? roundtripped = adapter.Roundtrip(serializer, original, Shapes.Of<ConformancePerson>());
			ConformanceAssert.Equal(original, roundtripped, "an object round-tripped through the default converters");
		});

		collector.Add("DictionaryRoundtrip", adapter =>
		{
			ShapeShiftSerializer<TEncoder, TDecoder> serializer = adapter.CreateSerializer();
			Dictionary<string, int> original = new(StringComparer.Ordinal)
			{
				["one"] = 1,
				["two"] = 2,
			};

			Dictionary<string, int>? roundtripped = adapter.Roundtrip(serializer, original, Shapes.Of<Dictionary<string, int>, ConformanceWitness>());
			ConformanceAssert.Equal(2, roundtripped?.Count ?? -1, "the entry count of a round-tripped dictionary");
			ConformanceAssert.Equal(1, roundtripped?["one"] ?? -1, "the \"one\" entry of a round-tripped dictionary");
			ConformanceAssert.Equal(2, roundtripped?["two"] ?? -1, "the \"two\" entry of a round-tripped dictionary");
		});

		collector.Add("CustomConverterIsUsed", adapter =>
		{
			ShapeShiftSerializer<TEncoder, TDecoder> serializer = adapter.CreateSerializer() with
			{
				Converters = ConverterCollection.Create<TEncoder, TDecoder>([new UpperCaseStringConverter()]),
			};

			byte[] payload = adapter.Serialize(serializer, "abc", Shapes.Of<string, ConformanceWitness>());
			string? raw = adapter.Deserialize(adapter.CreateSerializer(), payload, Shapes.Of<string, ConformanceWitness>());
			ConformanceAssert.Equal("ABC", raw, "the value a registered custom converter wrote");

			string? roundtripped = adapter.Deserialize(serializer, payload, Shapes.Of<string, ConformanceWitness>());
			ConformanceAssert.Equal("abc", roundtripped, "the value a registered custom converter read back");
		});

		collector.Add("CustomConverterSeesFormatPrimitives", adapter =>
		{
			ShapeShiftSerializer<TEncoder, TDecoder> serializer = adapter.CreateSerializer() with
			{
				Converters = ConverterCollection.Create<TEncoder, TDecoder>([new PairConverter()]),
			};

			ConformancePerson original = new("ignored", 7, [4, 5]);
			ConformancePerson? roundtripped = adapter.Roundtrip(serializer, original, Shapes.Of<ConformancePerson>());
			ConformanceAssert.Equal("pair", roundtripped?.Name, "the name a custom object converter wrote");
			ConformanceAssert.Equal(7, roundtripped?.Age ?? -1, "the age a custom object converter wrote");
		});

		collector.AddIf(
			"ReferencePreservationRoundtrips",
			collector.Options.SupportsReferencePreservation,
			"The format does not support reference preservation.",
			adapter =>
			{
				ShapeShiftSerializer<TEncoder, TDecoder> serializer = adapter.CreateSerializer() with
				{
					PreserveReferences = ReferencePreservationMode.RejectCycles,
				};

				ConformanceSharedNode shared = new() { Label = "shared" };
				ConformanceSharedPair original = new() { First = shared, Second = shared };

				ConformanceSharedPair? roundtripped = adapter.Roundtrip(serializer, original, Shapes.Of<ConformanceSharedPair>());
				ConformanceAssert.True(roundtripped?.First is not null, "The first member should survive the round trip.");
				ConformanceAssert.True(
					ReferenceEquals(roundtripped!.First, roundtripped.Second),
					"Reference preservation should restore two members that pointed at one instance as one instance.");
			});

		collector.AddIf(
			"ReferencePreservationRejectsCycles",
			collector.Options.SupportsReferencePreservation,
			"The format does not support reference preservation.",
			adapter =>
			{
				ShapeShiftSerializer<TEncoder, TDecoder> serializer = adapter.CreateSerializer() with
				{
					PreserveReferences = ReferencePreservationMode.RejectCycles,
				};

				ConformanceNode root = new();
				root.Child = root;

				ConformanceAssert.FailsCleanly(
					() => adapter.Serialize(serializer, root, Shapes.Of<ConformanceNode>()),
					"serializing a cyclic graph under ReferencePreservationMode.RejectCycles");
			});

		collector.AddIf(
			"ReferencePreservationIsRejectedWhenUnsupported",
			!collector.Options.SupportsReferencePreservation,
			"The format supports reference preservation.",
			adapter =>
			{
				ConformanceAssert.Throws<NotSupportedException>(
					() => _ = adapter.CreateSerializer() with { PreserveReferences = ReferencePreservationMode.RejectCycles },
					"enabling reference preservation on a format that does not implement IReferencePreservingSerializer");
			});

		collector.Add("NamingPolicyIsApplied", adapter =>
		{
			ShapeShiftSerializer<TEncoder, TDecoder> serializer = adapter.CreateSerializer() with
			{
				PropertyNamingPolicy = ShapeShiftNamingPolicy.CamelCase,
			};

			byte[] payload = adapter.Serialize(serializer, new ConformanceDefaults { Text = "x", Number = 1 }, Shapes.Of<ConformanceDefaults>());
			List<string> names = ReadPropertyNames(adapter, payload);
			ConformanceAssert.True(names.Contains("text"), $"A camel-case naming policy should emit \"text\" but the names were [{string.Join(", ", names)}].");
			ConformanceAssert.True(names.Contains("number"), $"A camel-case naming policy should emit \"number\" but the names were [{string.Join(", ", names)}].");

			ConformanceDefaults? roundtripped = adapter.Deserialize(serializer, payload, Shapes.Of<ConformanceDefaults>());
			ConformanceAssert.Equal("x", roundtripped?.Text, "a member read back through a naming policy");
		});

		collector.AddIf(
			"SerializeDefaultValuesPolicyIsApplied",
			collector.Options.SupportsEmptyMaps,
			"The format cannot represent the empty map that omitting every default member produces.",
			adapter =>
			{
			ShapeShiftSerializer<TEncoder, TDecoder> omitting = adapter.CreateSerializer() with
			{
				SerializeDefaultValues = SerializeDefaultValuesPolicy.Never,
			};
			ShapeShiftSerializer<TEncoder, TDecoder> including = adapter.CreateSerializer() with
			{
				SerializeDefaultValues = SerializeDefaultValuesPolicy.Always,
			};

			ConformanceDefaults value = new();
			List<string> omitted = ReadPropertyNames(adapter, adapter.Serialize(omitting, value, Shapes.Of<ConformanceDefaults>()));
			List<string> included = ReadPropertyNames(adapter, adapter.Serialize(including, value, Shapes.Of<ConformanceDefaults>()));

			ConformanceAssert.Equal(0, omitted.Count, $"the number of members written under SerializeDefaultValuesPolicy.Never, which were [{string.Join(", ", omitted)}]");
			ConformanceAssert.Equal(2, included.Count, $"the number of members written under SerializeDefaultValuesPolicy.Always, which were [{string.Join(", ", included)}]");
		});

		collector.AddIf(
			"MissingMembersFallBackToDefaults",
			collector.Options.SupportsEmptyMaps,
			"The format cannot represent an empty map.",
			adapter =>
			{
				ShapeShiftSerializer<TEncoder, TDecoder> serializer = adapter.CreateSerializer();
				byte[] payload = adapter.Encode(static (ref TEncoder encoder) =>
				{
					encoder.WriteStartMap(0);
					encoder.WriteEndMap();
				});

				ConformanceDefaults? value = adapter.Deserialize(serializer, payload, Shapes.Of<ConformanceDefaults>());
				ConformanceAssert.True(value is not null, "An empty map should deserialize into an object rather than null.");
				ConformanceAssert.Equal(null, value!.Text, "a member absent from the payload");
				ConformanceAssert.Equal(0, value.Number, "a value-typed member absent from the payload");
			});

		collector.Add("UnknownMembersAreIgnored", adapter =>
		{
			ShapeShiftSerializer<TEncoder, TDecoder> serializer = adapter.CreateSerializer();
			byte[] payload = adapter.Encode(static (ref TEncoder encoder) =>
			{
				encoder.WriteStartMap(3);
				encoder.WritePropertyName("Text");
				encoder.WriteValue("kept");
				encoder.WritePropertyName("Surplus");
				encoder.WriteStartVector(2);
				encoder.WriteValue(1L);
				encoder.WriteValue(2L);
				encoder.WriteEndVector();
				encoder.WritePropertyName("Number");
				encoder.WriteValue(5L);
				encoder.WriteEndMap();
			});

			ConformanceDefaults? value = adapter.Deserialize(serializer, payload, Shapes.Of<ConformanceDefaults>());
			ConformanceAssert.Equal("kept", value?.Text, "a member that precedes an unknown member");
			ConformanceAssert.Equal(5, value?.Number ?? -1, "a member that follows an unknown member");
		});
	}

	private static List<string> ReadPropertyNames(FormatConformanceAdapter<TEncoder, TDecoder> adapter, ReadOnlyMemory<byte> payload)
	{
		List<string> names = [];
		adapter.Decode(payload, (ref TDecoder decoder) =>
		{
			decoder.ReadStartMap();
			while (decoder.NextTokenType != TokenType.EndMap)
			{
				names.Add(decoder.ReadPropertyName().ToString());
				decoder.Skip();
			}

			decoder.ReadEndMap();
		});

		return names;
	}

	/// <summary>
	/// A converter that proves user converters are consulted for a primitive type.
	/// </summary>
	private sealed class UpperCaseStringConverter : ShapeShiftConverter<string, TEncoder, TDecoder>
	{
		/// <inheritdoc/>
		public override string? Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context)
		{
			if (decoder.TryReadNull())
			{
				decoder.ReadNull();
				return null;
			}

			return decoder.ReadString().ToLowerInvariant();
		}

		/// <inheritdoc/>
		public override void Write(ref TEncoder encoder, in string? value, SerializationContext<TEncoder, TDecoder> context)
		{
			if (value is null)
			{
				encoder.WriteNull();
				return;
			}

			encoder.WriteValue(value.ToUpperInvariant());
		}
	}

	/// <summary>
	/// A converter that writes an object with the format's own map primitives, proving that a
	/// user converter can drive the encoder and decoder directly.
	/// </summary>
	private sealed class PairConverter : ShapeShiftConverter<ConformancePerson, TEncoder, TDecoder>
	{
		/// <inheritdoc/>
		public override ConformancePerson? Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context)
		{
			if (decoder.TryReadNull())
			{
				decoder.ReadNull();
				return null;
			}

			context.DepthStep();
			decoder.ReadStartMap();
			string name = "pair";
			int age = 0;
			while (decoder.NextTokenType != TokenType.EndMap)
			{
				ReadOnlySpan<char> propertyName = decoder.ReadPropertyName();
				if (propertyName.SequenceEqual("age"))
				{
					age = checked((int)decoder.ReadInt64());
				}
				else
				{
					decoder.Skip();
				}
			}

			decoder.ReadEndMap();
			return new ConformancePerson(name, age, []);
		}

		/// <inheritdoc/>
		public override void Write(ref TEncoder encoder, in ConformancePerson? value, SerializationContext<TEncoder, TDecoder> context)
		{
			if (value is null)
			{
				encoder.WriteNull();
				return;
			}

			context.DepthStep();
			encoder.WriteStartMap(1);
			encoder.WritePropertyName("age");
			encoder.WriteValue((long)value.Age);
			encoder.WriteEndMap();
		}
	}
}
