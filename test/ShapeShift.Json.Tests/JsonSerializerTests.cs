// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using ShapeShift.Tests;

namespace ShapeShift.Json.Tests;

public partial class JsonSerializerTests : TestBase
{
	private readonly JsonSerializer serializer = new();

	internal enum Status
	{
		Inactive,
		Active,
	}

	[Test]
	public async Task PrimitiveRoots_RoundTrip()
	{
		await Assert.That(this.RoundTrip<string, Witness>("hello")).IsEqualTo("hello");
		await Assert.That(this.RoundTrip<int, Witness>(42)).IsEqualTo(42);
		await Assert.That(this.RoundTrip<bool, Witness>(true)).IsTrue();
	}

	[Test]
	public async Task Object_RoundTrips()
	{
		Person value = new("Ada", 37, Status.Active);

		string json = this.serializer.Serialize(value);
		Person? actual = this.serializer.Deserialize<Person>(json);

		await Assert.That(json).IsEqualTo("""{"Name":"Ada","Age":37,"Status":"Active"}""");
		await Assert.That(actual).IsEqualTo(value);
	}

	[Test]
	public async Task CollectionsAndDictionaries_RoundTrip()
	{
		Collections value = new(
			[1, 2, 3],
			ImmutableArray.Create("a", "b"),
			new Dictionary<string, int> { ["one"] = 1 },
			new Dictionary<int, string> { [2] = "two" });

		string json = this.serializer.Serialize(value);
		Collections? actual = this.serializer.Deserialize<Collections>(json);

		await Assert.That(json).Contains("\"Words\":{\"one\":1}");
		await Assert.That(json).Contains("\"Numbers\":[[2,\"two\"]]");
		await Assert.That(actual).IsNotNull();
		await Assert.That(actual.Items.SequenceEqual(value.Items)).IsTrue();
		await Assert.That(actual.ImmutableItems.SequenceEqual(value.ImmutableItems)).IsTrue();
		await Assert.That(actual.Words.Count == 1 && actual.Words["one"] == 1).IsTrue();
		await Assert.That(actual.Numbers.Count == 1 && actual.Numbers[2] == "two").IsTrue();
	}

	[Test]
	public async Task NullCollectionElement_IsConsumed()
	{
		NullableItems? actual = this.serializer.Deserialize<NullableItems>("""{"Items":[null,"value"]}""");

		await Assert.That(actual?.Items.SequenceEqual([null, "value"])).IsTrue();
	}

	[Test]
	public async Task DuplicateProperty_IsRejected()
	{
		Func<Person?> deserialize = () => this.serializer.Deserialize<Person>("""{"Name":"a","Name":"b","Age":1,"Status":"Active"}""");

		await Assert.That(deserialize).Throws<ShapeShiftSerializationException>();
	}

	[Test]
	public async Task MissingRequiredConstructorProperty_IsRejected()
	{
		Func<Person?> deserialize = () => this.serializer.Deserialize<Person>("""{"Age":1,"Status":"Active"}""");

		await Assert.That(deserialize).Throws<ShapeShiftSerializationException>();
	}

	[Test]
	public async Task DefaultValues_CanBeOmitted()
	{
		JsonSerializer serializer = this.serializer with { SerializeDefaultValues = SerializeDefaultValuesPolicy.Never };

		string json = serializer.Serialize(new Defaults());

		await Assert.That(json).IsEqualTo("{}");
	}

	[Test]
	public async Task ReaderOptions_AreHonored()
	{
		JsonSerializer serializer = this.serializer with
		{
			AllowTrailingCommas = true,
			CommentHandling = JsonCommentHandling.Skip,
		};

		Person? actual = serializer.Deserialize<Person>("""{"Name":"Ada",/* comment */"Age":37,"Status":"Active",}""");

		await Assert.That(actual).IsEqualTo(new Person("Ada", 37, Status.Active));
	}

	[Test]
	public async Task BufferWriterAndStreams_RoundTrip()
	{
		Person value = new("Ada", 37, Status.Active);
		ArrayBufferWriter<byte> writer = new();
		this.serializer.Serialize<Person, Person>(writer, value);

		await using MemoryStream stream = new();
		await this.serializer.SerializeAsync(stream, value);
		stream.Position = 0;
		Person? actual = await this.serializer.DeserializeAsync<Person>(stream);

		await Assert.That(writer.WrittenSpan.SequenceEqual(stream.ToArray())).IsTrue();
		await Assert.That(actual).IsEqualTo(value);
	}

	[Test]
	public async Task Surrogate_RoundTrips()
	{
		SurrogateValue value = new(3, 5);

		string json = this.serializer.Serialize(value);
		SurrogateValue? actual = this.serializer.Deserialize<SurrogateValue>(json);

		await Assert.That(json).IsEqualTo("""{"A":3,"B":5}""");
		await Assert.That(actual?.Sum).IsEqualTo(8);
	}

	[Test]
	public async Task AttributedUnion_RoundTripsStringAndIntegerDiscriminators()
	{
		UnionContainer value = new(new NamedCase("Ada"), new TaggedCase(42));

		string json = this.serializer.Serialize(value);
		UnionContainer? actual = this.serializer.Deserialize<UnionContainer>(json);

		await Assert.That(json).IsEqualTo("""{"Named":["named",{"Name":"Ada"}],"Tagged":[7,{"Value":42}]}""");
		await Assert.That(actual?.Named).IsEqualTo(value.Named);
		await Assert.That(actual?.Tagged).IsEqualTo(value.Tagged);
	}

	[Test]
	public async Task DynamicValue_RoundTrips()
	{
		ShapeShiftValue value = new ShapeShiftMap(new Dictionary<string, ShapeShiftValue>
		{
			["boolean"] = true,
			["integer"] = 42L,
			["array"] = new ShapeShiftArray([ShapeShiftValue.Null, "text"]),
		});

		string json = this.serializer.Serialize(value);
		ShapeShiftValue? actual = this.serializer.Deserialize<ShapeShiftValue>(json);

		await Assert.That(json).IsEqualTo("""{"boolean":true,"integer":42,"array":[null,"text"]}""");
		await Assert.That(actual).IsTypeOf<ShapeShiftMap>();
		ShapeShiftMap map = actual as ShapeShiftMap ?? throw new InvalidOperationException("Expected a dynamic map.");
		await Assert.That(map.Properties["boolean"]).IsEqualTo(new ShapeShiftBoolean(true));
		await Assert.That(map.Properties["integer"]).IsEqualTo(new ShapeShiftInteger(42));
		await Assert.That(map.Properties["array"]).IsTypeOf<ShapeShiftArray>();
	}

	[Test]
	public async Task StringLengthLimit_IsEnforced()
	{
		JsonSerializer serializer = this.serializer with { StartingContext = new() { MaxStringLength = 3 } };
		Func<string> serialize = () => serializer.Serialize<string, Witness>("long");
		Func<string?> deserialize = () => serializer.Deserialize<string, Witness>("\"long\"");

		await Assert.That(serialize).Throws<ShapeShiftSerializationException>();
		await Assert.That(deserialize).Throws<ShapeShiftSerializationException>();
	}

	[Test]
	public async Task JsonDomAndBinaryConverters_RoundTrip()
	{
		using JsonDocument document = JsonDocument.Parse("""{"value":1}""");
		JsonValues value = new(
			document.RootElement.Clone(),
			JsonNode.Parse("""[true,null]"""),
			[1, 2, 3]);

		string json = this.serializer.Serialize(value);
		JsonValues? actual = this.serializer.Deserialize<JsonValues>(json);

		await Assert.That(actual?.Element.GetRawText()).IsEqualTo("""{"value":1}""");
		await Assert.That(actual?.Node?.ToJsonString()).IsEqualTo("""[true,null]""");
		await Assert.That(actual?.Binary.SequenceEqual(new byte[] { 1, 2, 3 })).IsTrue();
		await Assert.That(json).Contains("\"Binary\":\"AQID\"");
	}

	[Test]
	public async Task NamedFloatingPointValues_AreExplicitOptIn()
	{
		JsonSerializer serializer = this.serializer with { AllowNamedFloatingPointValues = true };

		string json = serializer.Serialize<double, Witness>(double.PositiveInfinity);
		double actual = serializer.Deserialize<double, Witness>(json);

		await Assert.That(json).IsEqualTo("\"Infinity\"");
		await Assert.That(double.IsPositiveInfinity(actual)).IsTrue();
	}

	private T? RoundTrip<T, TProvider>(T? value)
		where TProvider : IShapeable<T>
		=> this.serializer.Deserialize<T, TProvider>(this.serializer.Serialize<T, TProvider>(value));

	[GenerateShape]
	internal partial record Person(string Name, int Age, Status Status);

	[GenerateShape]
	internal partial record Collections(
		List<int> Items,
		ImmutableArray<string> ImmutableItems,
		Dictionary<string, int> Words,
		Dictionary<int, string> Numbers);

	[GenerateShape]
	internal partial record NullableItems(List<string?> Items);

	[GenerateShape]
	internal partial record Defaults
	{
		public int Count { get; init; }

		public string? Name { get; init; }
	}

	[GenerateShape(Marshaler = typeof(SurrogateValue.Marshaler))]
	internal partial class SurrogateValue
	{
		private readonly int a;
		private readonly int b;

		internal SurrogateValue(int a, int b)
		{
			this.a = a;
			this.b = b;
		}

		public int Sum => this.a + this.b;

		internal record struct Data(int A, int B);

		internal sealed class Marshaler : IMarshaler<SurrogateValue, Data?>
		{
			public Data? Marshal(SurrogateValue? value) => value is null ? null : new(value.a, value.b);

			public SurrogateValue? Unmarshal(Data? surrogate) => surrogate is Data value ? new(value.A, value.B) : null;
		}
	}

	[GenerateShape]
	[DerivedTypeShape(typeof(NamedCase), Name = "named")]
	[DerivedTypeShape(typeof(TaggedCase), Tag = 7)]
	internal partial record UnionBase;

	internal sealed record NamedCase(string Name) : UnionBase;

	internal sealed record TaggedCase(int Value) : UnionBase;

	[GenerateShape]
	internal partial record UnionContainer(UnionBase Named, UnionBase Tagged);

	[GenerateShape]
	internal partial record JsonValues(JsonElement Element, JsonNode? Node, byte[] Binary);

	[GenerateShapeFor<string>]
	[GenerateShapeFor<int>]
	[GenerateShapeFor<bool>]
	[GenerateShapeFor<double>]
	private partial class Witness;
}
