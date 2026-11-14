// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Numerics;
using PolyType;
using ShapeShift.Equality;

namespace ShapeShift.Cbor.Tests;

/// <summary>
/// Verifies CBOR maps whose keys are arbitrary data items.
/// </summary>
public partial class CborDataMapTests
{
	private readonly CborSerializer serializer = new();

	[Test]
	public async Task TextMap_RoundTripsAsStringMap()
	{
		ShapeShiftValue value = new ShapeShiftMap(new Dictionary<string, ShapeShiftValue> { ["a"] = 1UL });

		byte[] encoded = this.serializer.Serialize(value);
		ShapeShiftValue? actual = this.serializer.Deserialize<ShapeShiftValue>(encoded);

		await Assert.That(Convert.ToHexString(encoded)).IsEqualTo("A1616101");
		await Assert.That(actual).IsTypeOf<ShapeShiftMap>();
		ShapeShiftMap map = (ShapeShiftMap)actual!;
		await Assert.That(map.Properties["a"]).IsEqualTo((ShapeShiftValue)1UL);
	}

	[Test]
	public async Task DataMap_WithOnlyTextKeys_ReadsBackAsStringMap()
	{
		ShapeShiftValue value = new ShapeShiftDataMap([new ShapeShiftDataMapEntry("a", 1UL)]);

		byte[] encoded = this.serializer.Serialize(value);
		ShapeShiftValue? actual = this.serializer.Deserialize<ShapeShiftValue>(encoded);

		await Assert.That(Convert.ToHexString(encoded)).IsEqualTo("A1616101");
		await Assert.That(actual).IsTypeOf<ShapeShiftMap>();
	}

	[Test]
	public async Task EmptyDataMap_ReadsBackAsStringMap()
	{
		byte[] encoded = this.serializer.Serialize<ShapeShiftValue>(new ShapeShiftDataMap([]));
		ShapeShiftValue? actual = this.serializer.Deserialize<ShapeShiftValue>(encoded);

		await Assert.That(Convert.ToHexString(encoded)).IsEqualTo("A0");
		await Assert.That(actual).IsTypeOf<ShapeShiftMap>();
		await Assert.That(((ShapeShiftMap)actual!).Properties).IsEmpty();
	}

	[Test]
	public async Task SampleMap_RoundTripsIntegerAndByteStringKeys()
	{
		ShapeShiftValue value = new ShapeShiftDataMap(
		[
			new ShapeShiftDataMapEntry(1UL, "one"),
			new ShapeShiftDataMapEntry(new byte[] { 0x00 }, 2UL),
		]);

		byte[] encoded = this.serializer.Serialize(value);
		ShapeShiftValue? actual = this.serializer.Deserialize<ShapeShiftValue>(encoded);

		await Assert.That(Convert.ToHexString(encoded)).IsEqualTo("A201636F6E65410002");
		ShapeShiftDataMap map = actual as ShapeShiftDataMap ?? throw new InvalidOperationException("Expected a data map.");
		await Assert.That(map.Entries[0].Key).IsEqualTo((ShapeShiftValue)1UL);
		await Assert.That(map.Entries[0].Value).IsEqualTo((ShapeShiftValue)"one");
		await Assert.That(map.Entries[1].Key).IsTypeOf<ShapeShiftBinary>();
		await Assert.That(((ShapeShiftBinary)map.Entries[1].Key).Value.Span.SequenceEqual(new byte[] { 0x00 })).IsTrue();
		await Assert.That(map.Entries[1].Value).IsEqualTo((ShapeShiftValue)2UL);
	}

	[Test]
	public async Task EachKeyKind_RoundTrips()
	{
		await this.RoundTripKey(-3L);
		await this.RoundTripKey(1UL);
		await this.RoundTripKey(0.1d);
		await this.RoundTripKey(true);
		await this.RoundTripKey(false);
		await this.RoundTripKey(ShapeShiftValue.Null);
		await this.RoundTripKey(new byte[] { 0x00 });
		await this.RoundTripKey(Array.Empty<byte>());
		await this.RoundTripKey(new ShapeShiftArray([1UL, 2UL]));
		await this.RoundTripKey(new ShapeShiftMap(new Dictionary<string, ShapeShiftValue> { ["k"] = 1UL }));
		await this.RoundTripKey(new ShapeShiftDataMap([new ShapeShiftDataMapEntry(-3L, 4UL)]));
		await this.RoundTripKey(new ShapeShiftBigInteger(BigInteger.Parse("18446744073709551616", CultureInfo.InvariantCulture)));
		await this.RoundTripKey(double.PositiveInfinity);
		await this.RoundTripKey(double.NaN);
	}

	[Test]
	public async Task PositiveSignedIntegerKey_ReadsBackUnsigned()
	{
		byte[] encoded = this.serializer.Serialize<ShapeShiftValue>(new ShapeShiftDataMap([new ShapeShiftDataMapEntry(1L, "one")]));
		ShapeShiftDataMap map = (ShapeShiftDataMap)this.serializer.Deserialize<ShapeShiftValue>(encoded)!;

		await Assert.That(Convert.ToHexString(encoded)).IsEqualTo("A101636F6E65");
		await Assert.That(map.Entries[0].Key).IsTypeOf<ShapeShiftUnsignedInteger>();
		await Assert.That(map.Entries[0].Key).IsEqualTo((ShapeShiftValue)1UL);
	}

	[Test]
	public async Task HalfPrecisionKeys_ReadAsDouble()
	{
		await this.AssertHalfKey("F98000", -0.0d);
		await this.AssertHalfKey("F97C00", double.PositiveInfinity);
		await this.AssertHalfKey("F9FC00", double.NegativeInfinity);
		await this.AssertHalfKey("F97E00", double.NaN);

		ShapeShiftValue rewritten = this.serializer.Deserialize<ShapeShiftValue>(Convert.FromHexString("A1F97C0080"))!;
		byte[] encoded = this.serializer.Serialize(rewritten);
		await Assert.That(Convert.ToHexString(encoded)).IsEqualTo("A1F97C0080");
		byte[] tenth = this.serializer.Serialize<ShapeShiftValue>(new ShapeShiftDataMap([new ShapeShiftDataMapEntry(0.1d, new ShapeShiftArray([]))]));
		await Assert.That(Convert.ToHexString(tenth)).IsEqualTo("A1FB3FB999999999999A80");
	}

	[Test]
	public async Task SimpleValueKey_ReadsAsItsIntegerCode()
	{
		ShapeShiftDataMap map = (ShapeShiftDataMap)this.serializer.Deserialize<ShapeShiftValue>(Convert.FromHexString("A1F780"))!;

		await Assert.That(map.Entries[0].Key).IsEqualTo((ShapeShiftValue)23UL);
		await Assert.That(map.Entries[0].Value).IsTypeOf<ShapeShiftArray>();
	}

	[Test]
	public async Task Tag1Key_ReadsAsItsNumericContent()
	{
		ShapeShiftDataMap map = (ShapeShiftDataMap)this.serializer.Deserialize<ShapeShiftValue>(Convert.FromHexString("A1C10080"))!;

		await Assert.That(map.Entries[0].Key).IsEqualTo((ShapeShiftValue)0UL);
	}

	[Test]
	public async Task TextKeyAfterIntegerKey_IsAccepted()
	{
		ShapeShiftDataMap map = (ShapeShiftDataMap)this.serializer.Deserialize<ShapeShiftValue>(Convert.FromHexString("A20102616103"))!;

		await Assert.That(map.Entries).Count().IsEqualTo(2);
		await Assert.That(map.Entries[0].Key).IsEqualTo((ShapeShiftValue)1UL);
		await Assert.That(map.Entries[0].Value).IsEqualTo((ShapeShiftValue)2UL);
		await Assert.That(map.Entries[1].Key).IsEqualTo((ShapeShiftValue)"a");
		await Assert.That(map.Entries[1].Value).IsEqualTo((ShapeShiftValue)3UL);
	}

	[Test]
	public async Task NestedTag1Key_IsRejected()
	{
		Func<ShapeShiftValue?> readPair = () => this.serializer.Deserialize<ShapeShiftValue>(new byte[] { 0xa1, 0xc1, 0xc1, 0x00, 0x80 });
		byte[] chain = new byte[10_003];
		chain[0] = 0xa1;
		chain.AsSpan(1, 10_000).Fill(0xc1);
		chain[10_001] = 0x00;
		chain[10_002] = 0x80;
		Func<ShapeShiftValue?> readChain = () => this.serializer.Deserialize<ShapeShiftValue>(chain);

		await Assert.That(readPair).Throws<DecoderException>();
		await Assert.That(readChain).Throws<DecoderException>();
	}

	[Test]
	public async Task UnsignedKeys_ThatShareANarrowHashCode_StayDistinct()
	{
		ShapeShiftDataMapEntry[] entries = new ShapeShiftDataMapEntry[32];
		for (int i = 0; i < entries.Length; i++)
		{
			ulong key = ((ulong)(uint)i << 32) | (uint)i;
			entries[i] = new ShapeShiftDataMapEntry(key, (long)i);
		}

		ShapeShiftValue? actual = this.serializer.Deserialize<ShapeShiftValue>(this.serializer.Serialize<ShapeShiftValue>(new ShapeShiftDataMap(entries)));
		ShapeShiftDataMap map = actual as ShapeShiftDataMap ?? throw new InvalidOperationException("Expected a data map.");

		await Assert.That(map.Entries).Count().IsEqualTo(entries.Length);
		IEqualityComparer<ShapeShiftValue> comparer = StructuralEqualityComparer.CreateCollisionResistant<ShapeShiftValue>();
		await Assert.That(comparer.GetHashCode((ShapeShiftValue)0UL)).IsNotEqualTo(comparer.GetHashCode((ShapeShiftValue)((1UL << 32) | 1UL)));
		for (int i = 0; i < entries.Length; i++)
		{
			await Assert.That(map.Entries[i].Key).IsEqualTo(entries[i].Key);
		}
	}

	[Test]
	public async Task MixedTextAndIntegerKeys_KeepTextKeysInsideTheDataMap()
	{
		byte[] encoded = Convert.FromHexString("A26161010102");
		ShapeShiftDataMap map = (ShapeShiftDataMap)this.serializer.Deserialize<ShapeShiftValue>(encoded)!;

		await Assert.That(map.Entries).Count().IsEqualTo(2);
		await Assert.That(map.Entries[0].Key).IsEqualTo((ShapeShiftValue)"a");
		await Assert.That(map.Entries[0].Value).IsEqualTo((ShapeShiftValue)1UL);
		await Assert.That(map.Entries[1].Key).IsEqualTo((ShapeShiftValue)1UL);
		await Assert.That(map.Entries[1].Value).IsEqualTo((ShapeShiftValue)2UL);
	}

	[Test]
	public async Task IndefiniteMap_WithIntegerKey_RoundTripsThroughTheDynamicModel()
	{
		ShapeShiftDataMap map = (ShapeShiftDataMap)this.serializer.Deserialize<ShapeShiftValue>(Convert.FromHexString("BF01626F6BFF"))!;

		await Assert.That(map.Entries).Count().IsEqualTo(1);
		await Assert.That(map.Entries[0].Key).IsEqualTo((ShapeShiftValue)1UL);
		await Assert.That(map.Entries[0].Value).IsEqualTo((ShapeShiftValue)"ok");
	}

	[Test]
	public async Task DuplicateKeys_AreRejected()
	{
		Func<ShapeShiftValue?> read = () => this.serializer.Deserialize<ShapeShiftValue>(Convert.FromHexString("A2016161016162"));
		ShapeShiftValue duplicate = new ShapeShiftDataMap(
		[
			new ShapeShiftDataMapEntry(1UL, "one"),
			new ShapeShiftDataMapEntry(1UL, "again"),
		]);
		Func<byte[]> write = () => this.serializer.Serialize(duplicate);

		await Assert.That(read).Throws<ShapeShiftSerializationException>();
		await Assert.That(write).Throws<ShapeShiftSerializationException>();
	}

	[Test]
	public async Task TypedDictionaries_KeepTheirExistingWireFormat()
	{
		byte[] textKeys = this.serializer.Serialize<Dictionary<string, int>, DictionaryWitness>(new Dictionary<string, int> { ["a"] = 1 });
		byte[] integerKeys = this.serializer.Serialize<Dictionary<int, string>, DictionaryWitness>(new Dictionary<int, string> { [2] = "two" });

		Dictionary<string, int>? textMap = this.serializer.Deserialize<Dictionary<string, int>, DictionaryWitness>(textKeys);
		Dictionary<int, string>? integerMap = this.serializer.Deserialize<Dictionary<int, string>, DictionaryWitness>(integerKeys);

		await Assert.That(Convert.ToHexString(textKeys)).IsEqualTo("A1616101");
		await Assert.That(textMap!["a"]).IsEqualTo(1);
		await Assert.That(Convert.ToHexString(integerKeys)).IsEqualTo("8182026374776F");
		await Assert.That(integerMap![2]).IsEqualTo("two");
	}

	[Test]
	public async Task Object_RejectsANonTextKey()
	{
		Func<Named?> read = () => this.serializer.Deserialize<Named>(Convert.FromHexString("A1016161"));

		await Assert.That(read).Throws<DecoderException>();
	}

	[Test]
	public async Task Serializer_StillRejectsAMapDeeperThanTheDefaultLimit()
	{
		byte[] encoded = new byte[(65 * 1) + 66];
		encoded.AsSpan(0, 65).Fill(0xa1);
		Func<ShapeShiftValue?> read = () => this.serializer.Deserialize<ShapeShiftValue>(encoded);

		await Assert.That(read).Throws<ShapeShiftSerializationException>();
	}

	[Test]
	public async Task NegativeZeroMap_ReadsAsAFloatKey()
	{
		ShapeShiftDataMap map = (ShapeShiftDataMap)this.serializer.Deserialize<ShapeShiftValue>(Convert.FromHexString("A1F9800080"))!;
		ShapeShiftFloat key = (ShapeShiftFloat)map.Entries[0].Key;

		await Assert.That(map.Entries).Count().IsEqualTo(1);
		await Assert.That(BitConverter.DoubleToInt64Bits(key.Value)).IsEqualTo(BitConverter.DoubleToInt64Bits(-0.0d));
		await Assert.That(map.Entries[0].Value).IsTypeOf<ShapeShiftArray>();
		await Assert.That(((ShapeShiftArray)map.Entries[0].Value).Items).IsEmpty();
	}

	[Test]
	public async Task ComplianceVectors_WithNonTextKeys_AreAccepted()
	{
		await Assert.That(Accept("A1F9800080")).IsNotNull();
		await Assert.That(Accept("B81A808081008081808081810080F580F480F680F7800080613080FB3FB999999999999A8001802080F97C0080F9FC0080F97E0080C2491C000000000000000080A080A1808080A1A08080A1A18080808040804100806080616180C10080")).IsNotNull();
		await Assert.That(Accept(this.DeeplyNestedKey())).IsNotNull();
		await Assert.That(Accept(this.DeeplyNestedValue())).IsNotNull();

		object? Accept(string hex)
		{
			CborDecoder decoder = new(Convert.FromHexString(hex));
			object? value = this.ReadTokenTree(ref decoder);
			decoder.EnsureEndOfDocument();
			return value;
		}
	}

	private async Task RoundTripKey(ShapeShiftValue key)
	{
		ShapeShiftValue value = new ShapeShiftDataMap([new ShapeShiftDataMapEntry(key, "value")]);
		ShapeShiftValue? actual = this.serializer.Deserialize<ShapeShiftValue>(this.serializer.Serialize(value));
		await Assert.That(actual).IsTypeOf<ShapeShiftDataMap>();
		ShapeShiftDataMap map = (ShapeShiftDataMap)actual!;
		await Assert.That(map.Entries).Count().IsEqualTo(1);
		await Assert.That(map.Entries[0].Value).IsEqualTo((ShapeShiftValue)"value");
		await Assert.That(StructuralEqualityComparer.Create<ShapeShiftValue>().Equals(map.Entries[0].Key, key)).IsTrue();
	}

	private async Task AssertHalfKey(string keyHex, double expected)
	{
		ShapeShiftDataMap map = (ShapeShiftDataMap)this.serializer.Deserialize<ShapeShiftValue>(Convert.FromHexString("A1" + keyHex + "80"))!;
		ShapeShiftFloat key = (ShapeShiftFloat)map.Entries[0].Key;
		if (double.IsNaN(expected))
		{
			await Assert.That(double.IsNaN(key.Value)).IsTrue();
		}
		else if (expected == 0)
		{
			await Assert.That(BitConverter.DoubleToInt64Bits(key.Value)).IsEqualTo(BitConverter.DoubleToInt64Bits(expected));
		}
		else
		{
			await Assert.That(key.Value).IsEqualTo(expected);
		}
	}

	private string DeeplyNestedKey()
	{
		byte[] encoded = new byte[1017];
		encoded.AsSpan(0, 508).Fill(0xa1);
		return Convert.ToHexString(encoded);
	}

	private string DeeplyNestedValue()
	{
		byte[] encoded = new byte[1017];
		for (int i = 0; i < 508; i++)
		{
			encoded[i * 2] = 0xa1;
			encoded[(i * 2) + 1] = 0x00;
		}

		return Convert.ToHexString(encoded);
	}

	private object? ReadTokenTree(ref CborDecoder decoder)
	{
		switch (decoder.NextTokenType)
		{
			case TokenType.Null:
				decoder.ReadNull();
				return null;
			case TokenType.Boolean:
				return decoder.ReadBoolean();
			case TokenType.String:
				return decoder.ReadString();
			case TokenType.Number:
				return decoder.ReadDynamicNumber();
			case TokenType.Binary:
				return decoder.ReadByteArray();
			case TokenType.StartVector:
				decoder.ReadStartVector();
				List<object?> items = [];
				while (decoder.NextTokenType != TokenType.EndVector)
				{
					items.Add(this.ReadTokenTree(ref decoder));
				}

				decoder.ReadEndVector();
				return items;
			case TokenType.StartMap:
				decoder.ReadStartMap();
				Dictionary<string, object?> map = [];
				while (decoder.NextTokenType != TokenType.EndMap)
				{
					string key = decoder.NextTokenType == TokenType.PropertyName
						? decoder.ReadPropertyName().ToString()
						: Convert.ToString(this.ReadTokenTree(ref decoder), CultureInfo.InvariantCulture) ?? string.Empty;
					map[key] = this.ReadTokenTree(ref decoder);
				}

				decoder.ReadEndMap();
				return map;
			default:
				throw new InvalidOperationException($"Unsupported token {decoder.NextTokenType}.");
		}
	}

	[GenerateShape]
	internal partial record Named(string Name);

	[GenerateShapeFor<Dictionary<string, int>>]
	[GenerateShapeFor<Dictionary<int, string>>]
	private partial class DictionaryWitness;
}
