// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Conformance.Suites;

/// <summary>
/// Round-trips every scalar the encoder and decoder interfaces name, at the boundaries where
/// width bugs hide.
/// </summary>
/// <typeparam name="TEncoder">The format's encoder type.</typeparam>
/// <typeparam name="TDecoder">The format's decoder type.</typeparam>
internal sealed class PrimitiveSuite<TEncoder, TDecoder> : IConformanceSuite<TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <summary>
	/// Writes one value of a known type to an encoder.
	/// </summary>
	/// <typeparam name="TValue">The type of value to write.</typeparam>
	/// <param name="encoder">The encoder to write to.</param>
	/// <param name="value">The value to write.</param>
	private delegate void WriteValue<in TValue>(ref TEncoder encoder, TValue value);

	/// <inheritdoc/>
	public ConformanceCategory Category => ConformanceCategory.Primitives;

	/// <inheritdoc/>
	public void AddTests(ConformanceTestCollector<TEncoder, TDecoder> collector)
	{
		Requires.NotNull(collector);
		FormatConformanceOptions options = collector.Options;

		AddRoundtrip(collector, "BooleanTrue", null, true, static (ref TEncoder e, bool v) => e.WriteValue(v), static (ref TDecoder d) => d.ReadBoolean());
		AddRoundtrip(collector, "BooleanFalse", null, false, static (ref TEncoder e, bool v) => e.WriteValue(v), static (ref TDecoder d) => d.ReadBoolean());

		foreach ((string name, long value) in new (string, long)[]
		{
			("Zero", 0L),
			("One", 1L),
			("MinusOne", -1L),
			("SByteMin", sbyte.MinValue),
			("ByteMax", byte.MaxValue),
			("Int16Min", short.MinValue),
			("UInt16Max", ushort.MaxValue),
			("Int32Min", int.MinValue),
			("UInt32Max", uint.MaxValue),
			("Int64Min", long.MinValue),
			("Int64Max", long.MaxValue),
		})
		{
			AddRoundtrip(collector, $"Int64_{name}", null, value, static (ref TEncoder e, long v) => e.WriteValue(v), static (ref TDecoder d) => d.ReadInt64());
		}

		AddRoundtrip(collector, "UInt64_Zero", null, 0UL, static (ref TEncoder e, ulong v) => e.WriteValue(v), static (ref TDecoder d) => d.ReadUInt64());
		AddRoundtrip(
			collector,
			"UInt64_Max",
			options.SupportsUnsignedIntegers ? null : "The format cannot represent unsigned integers above long.MaxValue.",
			ulong.MaxValue,
			static (ref TEncoder e, ulong v) => e.WriteValue(v),
			static (ref TDecoder d) => d.ReadUInt64());

		string? int128Skip = options.SupportsInt128 ? null : "The format cannot represent 128-bit integers.";
		AddRoundtrip(collector, "Int128_Min", int128Skip, Int128.MinValue, static (ref TEncoder e, Int128 v) => e.WriteValue(v), static (ref TDecoder d) => d.ReadInt128());
		AddRoundtrip(collector, "Int128_Max", int128Skip, Int128.MaxValue, static (ref TEncoder e, Int128 v) => e.WriteValue(v), static (ref TDecoder d) => d.ReadInt128());
		AddRoundtrip(collector, "UInt128_Max", int128Skip, UInt128.MaxValue, static (ref TEncoder e, UInt128 v) => e.WriteValue(v), static (ref TDecoder d) => d.ReadUInt128());

		string? bigIntegerSkip = options.SupportsBigInteger ? null : "The format cannot represent arbitrary-precision integers.";
		AddRoundtrip(
			collector,
			"BigInteger_Wide",
			bigIntegerSkip,
			BigInteger.Pow(new BigInteger(10), 40) + BigInteger.One,
			static (ref TEncoder e, BigInteger v) => e.WriteValue(v),
			static (ref TDecoder d) => d.ReadBigInteger());
		AddRoundtrip(
			collector,
			"BigInteger_Negative",
			bigIntegerSkip,
			-BigInteger.Pow(new BigInteger(2), 200),
			static (ref TEncoder e, BigInteger v) => e.WriteValue(v),
			static (ref TDecoder d) => d.ReadBigInteger());

		string? halfSkip = options.SupportsHalf ? null : "The format cannot represent a 16-bit float.";
		AddRoundtrip(collector, "Half", halfSkip, (Half)1.5f, static (ref TEncoder e, Half v) => e.WriteValue(v), static (ref TDecoder d) => d.ReadHalf());

		AddRoundtrip(collector, "Single", null, 1.25f, static (ref TEncoder e, float v) => e.WriteValue(v), static (ref TDecoder d) => d.ReadSingle());
		AddRoundtrip(collector, "Single_Negative", null, -3.5f, static (ref TEncoder e, float v) => e.WriteValue(v), static (ref TDecoder d) => d.ReadSingle());
		AddRoundtrip(collector, "Double", null, 1.0 / 3.0, static (ref TEncoder e, double v) => e.WriteValue(v), static (ref TDecoder d) => d.ReadDouble());
		AddRoundtrip(collector, "Double_Epsilon", null, double.Epsilon, static (ref TEncoder e, double v) => e.WriteValue(v), static (ref TDecoder d) => d.ReadDouble());
		AddRoundtrip(collector, "Double_MaxValue", null, double.MaxValue, static (ref TEncoder e, double v) => e.WriteValue(v), static (ref TDecoder d) => d.ReadDouble());

		string? nonFiniteSkip = options.SupportsNonFiniteFloats ? null : "The format cannot represent NaN or infinity.";
		AddRoundtrip(collector, "Double_NaN", nonFiniteSkip, double.NaN, static (ref TEncoder e, double v) => e.WriteValue(v), static (ref TDecoder d) => d.ReadDouble());
		AddRoundtrip(collector, "Double_PositiveInfinity", nonFiniteSkip, double.PositiveInfinity, static (ref TEncoder e, double v) => e.WriteValue(v), static (ref TDecoder d) => d.ReadDouble());
		AddRoundtrip(collector, "Double_NegativeInfinity", nonFiniteSkip, double.NegativeInfinity, static (ref TEncoder e, double v) => e.WriteValue(v), static (ref TDecoder d) => d.ReadDouble());

		string? decimalSkip = options.SupportsDecimal ? null : "The format cannot represent a decimal without loss.";
		AddRoundtrip(collector, "Decimal", decimalSkip, 12345.6789m, static (ref TEncoder e, decimal v) => e.WriteValue(v), static (ref TDecoder d) => d.ReadDecimal());
		AddRoundtrip(collector, "Decimal_MaxValue", decimalSkip, decimal.MaxValue, static (ref TEncoder e, decimal v) => e.WriteValue(v), static (ref TDecoder d) => d.ReadDecimal());
		AddRoundtrip(collector, "Decimal_MinValue", decimalSkip, decimal.MinValue, static (ref TEncoder e, decimal v) => e.WriteValue(v), static (ref TDecoder d) => d.ReadDecimal());

		string? dateTimeSkip = options.SupportsDateTime ? null : "The format cannot represent a date and time.";
		AddRoundtrip(
			collector,
			"DateTime_Utc",
			dateTimeSkip,
			new DateTime(2024, 6, 15, 13, 45, 30, DateTimeKind.Utc),
			static (ref TEncoder e, DateTime v) => e.WriteValue(v),
			static (ref TDecoder d) => d.ReadDateTime().ToUniversalTime());
		AddRoundtrip(
			collector,
			"DateTime_Epoch",
			dateTimeSkip,
			DateTime.UnixEpoch,
			static (ref TEncoder e, DateTime v) => e.WriteValue(v),
			static (ref TDecoder d) => d.ReadDateTime().ToUniversalTime());

		string? timeSpanSkip = options.SupportsTimeSpan ? null : "The format cannot represent a duration.";
		AddRoundtrip(collector, "TimeSpan", timeSpanSkip, TimeSpan.FromMinutes(90), static (ref TEncoder e, TimeSpan v) => e.WriteValue(v), static (ref TDecoder d) => d.ReadTimeSpan());
		AddRoundtrip(collector, "TimeSpan_Negative", timeSpanSkip, TimeSpan.FromSeconds(-5), static (ref TEncoder e, TimeSpan v) => e.WriteValue(v), static (ref TDecoder d) => d.ReadTimeSpan());
		AddRoundtrip(collector, "TimeSpan_Zero", timeSpanSkip, TimeSpan.Zero, static (ref TEncoder e, TimeSpan v) => e.WriteValue(v), static (ref TDecoder d) => d.ReadTimeSpan());

		AddRoundtrip(collector, "String_Ascii", null, "hello", static (ref TEncoder e, string v) => e.WriteValue(v), static (ref TDecoder d) => d.ReadString());
		AddRoundtrip(
			collector,
			"String_Empty",
			options.SupportsEmptyStrings ? null : "The format cannot represent an empty string.",
			string.Empty,
			static (ref TEncoder e, string v) => e.WriteValue(v),
			static (ref TDecoder d) => d.ReadString());
		AddRoundtrip(collector, "String_NonAscii", null, "caf\u00e9 \u4e2d\u6587 \u0440\u0443\u0441", static (ref TEncoder e, string v) => e.WriteValue(v), static (ref TDecoder d) => d.ReadString());
		AddRoundtrip(
			collector,
			"String_SurrogatePair",
			options.SupportsSurrogatePairs ? null : "The format cannot represent astral-plane characters.",
			"emoji \ud83d\ude00 tail",
			static (ref TEncoder e, string v) => e.WriteValue(v),
			static (ref TDecoder d) => d.ReadString());
		AddRoundtrip(
			collector,
			"String_ControlCharacters",
			options.SupportsControlCharactersInStrings ? null : "The format cannot represent control characters in strings.",
			"line1\nline2\ttabbed\r\nquote\"backslash\\",
			static (ref TEncoder e, string v) => e.WriteValue(v),
			static (ref TDecoder d) => d.ReadString());
		AddRoundtrip(collector, "String_Long", null, new string('x', 4096), static (ref TEncoder e, string v) => e.WriteValue(v), static (ref TDecoder d) => d.ReadString());

		string? ambiguousSkip = options.PreservesAmbiguousStrings ? null : "The format cannot distinguish a string from a scalar its text resembles.";
		AddRoundtrip(collector, "String_LooksLikeNumber", ambiguousSkip, "42", static (ref TEncoder e, string v) => e.WriteValue(v), static (ref TDecoder d) => d.ReadString());
		AddRoundtrip(collector, "String_LooksLikeBoolean", ambiguousSkip, "true", static (ref TEncoder e, string v) => e.WriteValue(v), static (ref TDecoder d) => d.ReadString());
		AddRoundtrip(collector, "String_LooksLikeNull", ambiguousSkip, "null", static (ref TEncoder e, string v) => e.WriteValue(v), static (ref TDecoder d) => d.ReadString());

		collector.Add("CharSpanMatchesString", adapter =>
		{
			const string Value = "span and string agree";
			string roundtripped = ScalarHarness.Roundtrip(
				adapter,
				static (ref TEncoder encoder) => encoder.WriteValue(Value.AsSpan()),
				static (ref TDecoder decoder) => decoder.ReadCharSpan().ToString());
			ConformanceAssert.Equal(Value, roundtripped, "a string written as a span and read as a span");
		});

		collector.Add("NarrowIntegerExtensions", adapter =>
		{
			byte[] payload = adapter.Encode(static (ref TEncoder encoder) =>
			{
				encoder.WriteStartVector(6);
				encoder.WriteValue((long)sbyte.MinValue);
				encoder.WriteValue((long)short.MinValue);
				encoder.WriteValue((long)int.MinValue);
				encoder.WriteValue((ulong)byte.MaxValue);
				encoder.WriteValue((ulong)ushort.MaxValue);
				encoder.WriteValue((ulong)uint.MaxValue);
				encoder.WriteEndVector();
			});

			adapter.Decode(payload, static (ref TDecoder decoder) =>
			{
				decoder.ReadStartVector();
				ConformanceAssert.Equal(sbyte.MinValue, checked((sbyte)decoder.ReadInt64()), "an sbyte read through ReadInt64");
				ConformanceAssert.Equal(short.MinValue, checked((short)decoder.ReadInt64()), "a short read through ReadInt64");
				ConformanceAssert.Equal(int.MinValue, checked((int)decoder.ReadInt64()), "an int read through ReadInt64");
				ConformanceAssert.Equal(byte.MaxValue, checked((byte)decoder.ReadUInt64()), "a byte read through ReadUInt64");
				ConformanceAssert.Equal(ushort.MaxValue, checked((ushort)decoder.ReadUInt64()), "a ushort read through ReadUInt64");
				ConformanceAssert.Equal(uint.MaxValue, checked((uint)decoder.ReadUInt64()), "a uint read through ReadUInt64");
				decoder.ReadEndVector();
			});
		});

		collector.Add("PropertyNamesRoundtrip", adapter =>
		{
			string[] names = ["simple", "with space", "with\"quote", "caf\u00e9", "1", string.Empty];
			byte[] payload = adapter.Encode((ref TEncoder encoder) =>
			{
				encoder.WriteStartMap(names.Length);
				for (int i = 0; i < names.Length; i++)
				{
					encoder.WritePropertyName(names[i]);
					encoder.WriteValue((long)i);
				}

				encoder.WriteEndMap();
			});

			adapter.Decode(payload, (ref TDecoder decoder) =>
			{
				decoder.ReadStartMap();
				for (int i = 0; i < names.Length; i++)
				{
					ConformanceAssert.Equal(names[i], decoder.ReadPropertyName().ToString(), $"property name {i}");
					ConformanceAssert.Equal((long)i, decoder.ReadInt64(), $"the value of property {i}");
				}

				decoder.ReadEndMap();
			});
		});
	}

	private static void AddRoundtrip<TValue>(
		ConformanceTestCollector<TEncoder, TDecoder> collector,
		string name,
		string? skipReason,
		TValue value,
		WriteValue<TValue> write,
		DecodeFunc<TDecoder, TValue> read)
	{
		collector.Add(name, skipReason, adapter =>
		{
			TValue roundtripped = ScalarHarness.Roundtrip(
				adapter,
				(ref TEncoder encoder) => write(ref encoder, value),
				read);
			ConformanceAssert.Equal(value, roundtripped, $"a round-tripped {typeof(TValue).Name}");
		});
	}
}
