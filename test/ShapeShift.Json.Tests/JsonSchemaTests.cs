// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ShapeShift.Schema;
using ShapeShift.Tests;

namespace ShapeShift.Json.Tests;

public partial class JsonSchemaTests : TestBase
{
	private readonly JsonSerializer serializer = new();

	internal enum Color
	{
		Red,
		Green = 5,
	}

	[Flags]
	internal enum Access
	{
		None = 0,
		Read = 1,
		Write = 2,
	}

	[Test]
	public async Task Root_DeclaresTheDialect()
	{
		JsonObject schema = this.serializer.GetJsonSchema<Person>();

		await Assert.That((string?)schema["$schema"]).IsEqualTo(JsonSchema.Dialect);
		await Assert.That(schema.First().Key).IsEqualTo("$schema");
	}

	[Test]
	public async Task Root_DialectCanBeOmitted()
	{
		JsonObject schema = this.serializer.GetJsonSchema<Person>(new JsonSchemaOptions { IncludeSchemaKeyword = false });

		await Assert.That(schema.ContainsKey("$schema")).IsFalse();
	}

	[Test]
	public async Task Object_DescribesPropertiesAndRequirements()
	{
		JsonObject schema = this.serializer.GetJsonSchema<Person>();

		await Assert.That((string?)schema["type"]).IsEqualTo("object");
		await Assert.That((bool?)schema["additionalProperties"]).IsFalse();

		JsonObject properties = (JsonObject)schema["properties"]!;
		await Assert.That(string.Join(",", properties.Select(p => p.Key))).IsEqualTo("Name,Age,Favorite,Access,Rank");
		await Assert.That((string?)properties["Name"]!["type"]).IsEqualTo("string");
		await Assert.That((string?)properties["Age"]!["type"]).IsEqualTo("integer");
		await Assert.That((long?)properties["Age"]!["minimum"]).IsEqualTo(int.MinValue);
		await Assert.That((long?)properties["Age"]!["maximum"]).IsEqualTo(int.MaxValue);

		JsonArray required = (JsonArray)schema["required"]!;
		await Assert.That(string.Join(",", required.Select(r => (string?)r))).IsEqualTo("Name,Age,Favorite,Access");
	}

	[Test]
	public async Task Object_NullablePropertyWidensType()
	{
		JsonObject schema = this.serializer.GetJsonSchema<Nullables>();
		JsonObject properties = (JsonObject)schema["properties"]!;

		await Assert.That(string.Join(",", ((JsonArray)properties["Text"]!["type"]!).Select(t => (string?)t))).IsEqualTo("string,null");
		await Assert.That((string?)properties["Required"]!["type"]).IsEqualTo("string");
	}

	[Test]
	public async Task Object_HonorsNamingPolicy()
	{
		JsonSerializer camel = this.serializer with { PropertyNamingPolicy = ShapeShiftNamingPolicy.CamelCase };
		JsonObject schema = camel.GetJsonSchema<Person>();

		await Assert.That(((JsonObject)schema["properties"]!).ContainsKey("favorite")).IsTrue();
	}

	[Test]
	public async Task Object_ExtensionDataAllowsAdditionalProperties()
	{
		JsonObject schema = this.serializer.GetJsonSchema<Extensible>();

		await Assert.That(schema["additionalProperties"]).IsNull();
		await Assert.That((string?)schema["$comment"]).Contains("extension data");
	}

	[Test]
	public async Task Object_ReadOnlyIsAnnotated()
	{
		JsonObject schema = this.serializer.GetJsonSchema<Computed>();
		JsonObject properties = (JsonObject)schema["properties"]!;

		await Assert.That((bool?)properties["Doubled"]!["readOnly"]).IsTrue();
		await Assert.That(properties["Value"]!["readOnly"]).IsNull();
	}

	[Test]
	public async Task Enum_OffersNameAndNumberForms()
	{
		JsonObject schema = this.serializer.GetJsonSchema<Person>();
		JsonObject favorite = Resolve(schema, (JsonObject)schema["properties"]!["Favorite"]!);

		await Assert.That((string?)favorite["x-shapeshift-enum-serialized-as"]).IsEqualTo("name");

		JsonArray anyOf = (JsonArray)favorite["anyOf"]!;
		await Assert.That(string.Join(",", ((JsonArray)anyOf[0]!["enum"]!).Select(v => (string?)v))).IsEqualTo("Red,Green");
		await Assert.That((string?)anyOf[1]!["type"]).IsEqualTo("integer");
	}

	[Test]
	public async Task Enum_NumericFormComesFirstWhenSerializedByValue()
	{
		JsonSerializer numeric = this.serializer with { SerializeEnumValuesByName = false };
		JsonObject schema = numeric.GetJsonSchema<Person>();
		JsonObject favorite = Resolve(schema, (JsonObject)schema["properties"]!["Favorite"]!);

		await Assert.That((string?)favorite["x-shapeshift-enum-serialized-as"]).IsEqualTo("number");
		await Assert.That((string?)((JsonArray)favorite["anyOf"]!)[0]!["type"]).IsEqualTo("integer");
	}

	[Test]
	public async Task Enum_FlagsAreAnnotatedAndUnconstrainedNumerically()
	{
		JsonObject schema = this.serializer.GetJsonSchema<Person>();
		JsonObject access = Resolve(schema, (JsonObject)schema["properties"]!["Access"]!);

		await Assert.That((bool?)access["x-shapeshift-enum-flags"]).IsTrue();
	}

	[Test]
	public async Task Sequence_UsesItems()
	{
		JsonObject schema = this.serializer.GetJsonSchema<Bag>();
		JsonObject properties = (JsonObject)schema["properties"]!;

		JsonObject items = (JsonObject)properties["Items"]!;
		await Assert.That((string?)items["type"]).IsEqualTo("array");
		await Assert.That((string?)items["items"]!["type"]).IsEqualTo("integer");
		await Assert.That(items["uniqueItems"]).IsNull();

		await Assert.That((bool?)properties["Tags"]!["uniqueItems"]).IsTrue();
	}

	[Test]
	public async Task Map_WithStringKeysUsesObject()
	{
		JsonObject words = (JsonObject)this.serializer.GetJsonSchema<Bag>()["properties"]!["Words"]!;

		await Assert.That((string?)words["type"]).IsEqualTo("object");
		await Assert.That((string?)words["additionalProperties"]!["type"]).IsEqualTo("integer");
	}

	[Test]
	public async Task Map_WithNonStringKeysUsesPairArray()
	{
		JsonObject numbers = (JsonObject)this.serializer.GetJsonSchema<Bag>()["properties"]!["Numbers"]!;

		await Assert.That((string?)numbers["type"]).IsEqualTo("array");

		JsonObject pair = (JsonObject)numbers["items"]!;
		await Assert.That((string?)pair["type"]).IsEqualTo("array");
		await Assert.That((int?)pair["minItems"]).IsEqualTo(2);
		await Assert.That((int?)pair["maxItems"]).IsEqualTo(2);

		JsonArray prefix = (JsonArray)pair["prefixItems"]!;
		await Assert.That((string?)prefix[0]!["type"]).IsEqualTo("integer");
		await Assert.That((string?)prefix[1]!["type"]).IsEqualTo("string");
	}

	[Test]
	public async Task RectangularArray_UsesDimensionEnvelope()
	{
		JsonObject grid = (JsonObject)this.serializer.GetJsonSchema<Bag>()["properties"]!["Grid"]!;

		await Assert.That((string?)grid["type"]).IsEqualTo("array");
		await Assert.That((int?)grid["minItems"]).IsEqualTo(2);
		await Assert.That((int?)grid["maxItems"]).IsEqualTo(2);

		JsonArray prefix = (JsonArray)grid["prefixItems"]!;
		await Assert.That((int?)prefix[0]!["minItems"]).IsEqualTo(2);
		await Assert.That((int?)prefix[0]!["maxItems"]).IsEqualTo(2);
		await Assert.That((string?)prefix[1]!["items"]!["type"]).IsEqualTo("integer");
	}

	[Test]
	public async Task Binary_IsBase64EncodedString()
	{
		JsonObject blob = (JsonObject)this.serializer.GetJsonSchema<Bag>()["properties"]!["Blob"]!;

		await Assert.That((string?)blob["type"]).IsEqualTo("string");
		await Assert.That((string?)blob["contentEncoding"]).IsEqualTo("base64");
		await Assert.That(blob["x-shapeshift-max-binary-length"]).IsNull();
	}

	[Test]
	public async Task Recursion_ProducesDefinitionsAndReferences()
	{
		JsonObject schema = this.serializer.GetJsonSchema<Node>();

		await Assert.That((string?)schema["$ref"]).IsEqualTo("#/$defs/Node");

		JsonObject definition = (JsonObject)schema["$defs"]!["Node"]!;
		JsonObject children = (JsonObject)definition["properties"]!["Children"]!;
		await Assert.That((string?)children["items"]!["$ref"]).IsEqualTo("#/$defs/Node");
	}

	[Test]
	public async Task Union_ProjectsToOneOfTuples()
	{
		JsonObject schema = this.serializer.GetJsonSchema<Shape>();
		JsonArray oneOf = (JsonArray)schema["oneOf"]!;

		await Assert.That(oneOf.Count).IsEqualTo(3);
		await Assert.That((string?)((JsonArray)oneOf[0]!["prefixItems"]!)[0]!["type"]).IsEqualTo("null");
		await Assert.That((string?)((JsonArray)oneOf[1]!["prefixItems"]!)[0]!["const"]).IsEqualTo("circle");
		await Assert.That((int?)((JsonArray)oneOf[2]!["prefixItems"]!)[0]!["const"]).IsEqualTo(3);
	}

	[Test]
	public async Task Optional_AllowsNull()
	{
		JsonObject rank = (JsonObject)this.serializer.GetJsonSchema<Person>()["properties"]!["Rank"]!;

		await Assert.That(string.Join(",", ((JsonArray)rank["type"]!).Select(t => (string?)t))).IsEqualTo("integer,null");
	}

	[Test]
	public async Task Surrogate_ProjectsTheSurrogateShape()
	{
		JsonObject schema = this.serializer.GetJsonSchema<SurrogateValue>();

		await Assert.That((string?)schema["x-shapeshift-surrogate-for"]).Contains("SurrogateValue");
		await Assert.That(string.Join(",", ((JsonArray)schema["type"]!).Select(t => (string?)t))).IsEqualTo("object,null");
	}

	[Test]
	public async Task Dynamic_IsUnconstrained()
	{
		JsonObject schema = this.serializer.GetJsonSchema<Exotic>();
		JsonObject properties = (JsonObject)schema["properties"]!;

		await Assert.That(properties["Raw"]!["type"]).IsNull();
		await Assert.That(properties["Dynamic"]!["type"]).IsNull();
		await Assert.That((string?)properties["Dynamic"]!["$comment"]).IsNotNull();
	}

	[Test]
	public async Task Exotic_PrimitivesUseJsonRepresentations()
	{
		JsonObject properties = (JsonObject)this.serializer.GetJsonSchema<Exotic>()["properties"]!;

		await Assert.That((string?)properties["Timestamp"]!["format"]).IsEqualTo("date-time");
		await Assert.That((string?)properties["Duration"]!["type"]).IsEqualTo("string");
		await Assert.That(properties["Duration"]!["format"]).IsNull();
		await Assert.That((string?)properties["Duration"]!["$comment"]).Contains("\"c\"");
		await Assert.That((string?)properties["Money"]!["type"]).IsEqualTo("number");
		await Assert.That((string?)properties["Big"]!["type"]).IsEqualTo("integer");
		await Assert.That((string?)properties["Huge"]!["type"]).IsEqualTo("integer");
		await Assert.That(properties["Huge"]!["minimum"]).IsNull();
		await Assert.That((string?)properties["Initial"]!["type"]).IsEqualTo("string");
		await Assert.That((int?)properties["Initial"]!["maxLength"]).IsEqualTo(1);
		await Assert.That((string?)properties["Symbol"]!["type"]).IsEqualTo("integer");

		JsonObject offset = (JsonObject)properties["Offset"]!;
		await Assert.That((string?)offset["type"]).IsEqualTo("array");
		await Assert.That((int?)offset["minItems"]).IsEqualTo(2);
		await Assert.That((long?)((JsonArray)offset["prefixItems"]!)[1]!["minimum"]).IsEqualTo(-840);
	}

	[Test]
	public async Task FloatingPoint_NamedValuesAreOptedIn()
	{
		JsonObject strict = (JsonObject)this.serializer.GetJsonSchema<Floats>()["properties"]!["Value"]!;
		await Assert.That((string?)strict["type"]).IsEqualTo("number");

		JsonObject relaxed = (JsonObject)this.serializer.GetJsonSchema<Floats>(
			new JsonSchemaOptions { AllowNamedFloatingPointValues = true })["properties"]!["Value"]!;
		JsonArray anyOf = (JsonArray)relaxed["anyOf"]!;
		await Assert.That(string.Join(",", ((JsonArray)anyOf[1]!["enum"]!).Select(v => (string?)v))).IsEqualTo("NaN,Infinity,-Infinity");
	}

	[Test]
	public async Task Limits_ComeFromTheOptions()
	{
		JsonObject schema = JsonSchema.Create(
			this.serializer.GetContract<Bag>(),
			new JsonSchemaOptions { Limits = new JsonSchemaLimits(5, 6, 7) });

		await Assert.That((int?)schema["properties"]!["Items"]!["maxItems"]).IsEqualTo(5);
		await Assert.That((int?)schema["properties"]!["Blob"]!["x-shapeshift-max-binary-length"]).IsEqualTo(7);
		await Assert.That((int?)schema["properties"]!["Words"]!["maxProperties"]).IsEqualTo(5);
		await Assert.That((int?)schema["properties"]!["Tags"]!["items"]!["maxLength"]).IsEqualTo(6);
	}

	[Test]
	public async Task Limits_CanBeTakenFromASerializationContext()
	{
		SerializationContext<JsonEncoder, JsonDecoder> context = new();
		JsonSchemaLimits limits = JsonSchemaLimits.FromContext(context);

		await Assert.That(limits.MaxCollectionLength).IsEqualTo(context.MaxCollectionLength);
		await Assert.That(limits.MaxStringLength).IsEqualTo(context.MaxStringLength);
		await Assert.That(limits.MaxBinaryLength).IsEqualTo(context.MaxBinaryLength);

		JsonObject schema = this.serializer.GetJsonSchema<Bag>(new JsonSchemaOptions { Limits = limits });
		await Assert.That((int?)schema["properties"]!["Items"]!["maxItems"]).IsEqualTo(context.MaxCollectionLength);
	}

	[Test]
	public async Task Limits_AreOmittedByDefault()
	{
		JsonObject schema = this.serializer.GetJsonSchema<Bag>();

		await Assert.That(schema["properties"]!["Items"]!["maxItems"]).IsNull();
		await Assert.That(schema["properties"]!["Blob"]!["x-shapeshift-max-binary-length"]).IsNull();
	}

	[Test]
	public async Task Comments_CanBeSuppressed()
	{
		JsonObject schema = this.serializer.GetJsonSchema<Exotic>(new JsonSchemaOptions { IncludeComments = false });

		await Assert.That(schema["properties"]!["Dynamic"]!["$comment"]).IsNull();
	}

	[Test]
	public async Task UndocumentedConverter_ProducesAnExplicitlyUnknownSchema()
	{
		JsonObject opaque = (JsonObject)this.serializer.GetJsonSchema<Custom>()["properties"]!["Opaque"]!;

		await Assert.That((bool?)opaque["x-shapeshift-undocumented"]).IsTrue();
		await Assert.That((string?)opaque["x-shapeshift-converter"]).Contains("OpaqueConverter");
		await Assert.That(opaque["type"]).IsNull();
		await Assert.That(JsonSchemaValidator.Validate(opaque, "\"anything\"")).IsEmpty();
	}

	[Test]
	public async Task MessagePackProfile_AnnotatesExtensionTypes()
	{
		JsonObject properties = (JsonObject)JsonSchema.Create(
			this.serializer.GetContract<Exotic>(),
			new JsonSchemaOptions { Profile = JsonSchemaProfile.MessagePack })["properties"]!;

		await Assert.That((int?)properties["Timestamp"]!["x-msgpack-extension"]).IsEqualTo(-1);
		await Assert.That((int?)properties["Money"]!["x-msgpack-extension"]).IsEqualTo(-40);
		await Assert.That((int?)properties["Big"]!["x-msgpack-extension"]).IsEqualTo(-41);
		await Assert.That((int?)properties["Huge"]!["x-msgpack-extension"]).IsEqualTo(-43);
		await Assert.That((int?)properties["Duration"]!["x-msgpack-extension"]).IsEqualTo(-44);
	}

	[Test]
	public async Task MessagePackProfile_AnnotatesStructuralTypes()
	{
		JsonObject properties = (JsonObject)JsonSchema.Create(
			this.serializer.GetContract<Bag>(),
			new JsonSchemaOptions { Profile = JsonSchemaProfile.MessagePack })["properties"]!;

		await Assert.That((string?)properties["Items"]!["x-msgpack-type"]).IsEqualTo("array");
		await Assert.That((string?)properties["Words"]!["x-msgpack-type"]).IsEqualTo("map");
		await Assert.That((string?)properties["Blob"]!["x-msgpack-type"]).IsEqualTo("bin");
		await Assert.That((string?)properties["Blob"]!["contentEncoding"]).IsEqualTo("base64");
	}

	[Test]
	public async Task JsonProfile_DoesNotEmitMessagePackAnnotations()
	{
		JsonObject properties = (JsonObject)this.serializer.GetJsonSchema<Exotic>()["properties"]!;

		await Assert.That(properties["Timestamp"]!["x-msgpack-extension"]).IsNull();
		await Assert.That(properties["Money"]!["x-msgpack-type"]).IsNull();
	}

	[Test]
	public async Task Schema_AcceptsRealSerializedPayloads()
	{
		await this.AssertSerializedFormValidates<Person>(new Person("Andrew", 50, Color.Green, Access.Read | Access.Write, 3));
		await this.AssertSerializedFormValidates<Person>(new Person("Andrew", 50, Color.Red, Access.None));
		await this.AssertSerializedFormValidates<Nullables>(new Nullables(null, "here"));
		await this.AssertSerializedFormValidates<Bag>(new Bag([1, 2], ["a"], new() { ["k"] = 1 }, new() { [1] = "one" }, new int[2, 2], [1, 2, 3]));
		await this.AssertSerializedFormValidates<Node>(new Node("root", [new Node("leaf", null)]));
		await this.AssertSerializedFormValidates<Shape>(new Circle(1.5));
		await this.AssertSerializedFormValidates<Shape>(new Square(2));
		await this.AssertSerializedFormValidates<Computed>(new Computed { Value = 3 });
		await this.AssertSerializedFormValidates<Custom>(new Custom("opaque"));
		await this.AssertSerializedFormValidates<Extensible>(new Extensible { Known = "yes" });
		await this.AssertSerializedFormValidates<Exotic>(new Exotic(
			new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc),
			new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.FromHours(-8)),
			TimeSpan.FromMinutes(90),
			1.25m,
			Int128.MaxValue,
			BigInteger.Pow(10, 40),
			'x',
			new Rune('!'),
			JsonDocument.Parse("{\"a\":1}").RootElement,
			(ShapeShiftValue)42L));
	}

	[Test]
	public async Task Schema_RejectsDocumentsTheSerializerWouldReject()
	{
		JsonObject schema = this.serializer.GetJsonSchema<Person>();

		await Assert.That(JsonSchemaValidator.Validate(schema, """{"Age":1,"Favorite":"Red","Access":"None"}""")).IsNotEmpty();
		await Assert.That(JsonSchemaValidator.Validate(schema, """{"Name":null,"Age":1,"Favorite":"Red","Access":"None"}""")).IsNotEmpty();
		await Assert.That(JsonSchemaValidator.Validate(schema, """{"Name":"a","Age":1,"Favorite":"Purple","Access":"None"}""")).IsNotEmpty();
		await Assert.That(JsonSchemaValidator.Validate(schema, """{"Name":"a","Age":1,"Favorite":"Red","Access":"None","Extra":1}""")).IsNotEmpty();
	}

	private static JsonObject Resolve(JsonObject root, JsonObject node)
		=> (string?)node["$ref"] is string reference
			? (JsonObject)root["$defs"]![reference["#/$defs/".Length..]]!
			: node;

	private async Task AssertSerializedFormValidates<T>(T value)
		where T : IShapeable<T>
	{
		JsonObject schema = this.serializer.GetJsonSchema<T>();
		string json = this.serializer.Serialize(value);
		IReadOnlyList<string> errors = JsonSchemaValidator.Validate(schema, json);
		await Assert.That(errors).IsEmpty().Because($"{typeof(T).Name} serialized as {json}");
	}

	[GenerateShape]
	internal partial record Person(string Name, int Age, Color Favorite, Access Access, int? Rank = null);

	[GenerateShape]
	internal partial record Nullables(string? Text, string Required);

	[GenerateShape]
	internal partial record Floats(double Value);

	[GenerateShape]
	internal partial class Computed
	{
		public int Value { get; set; }

		public int Doubled => this.Value * 2;
	}

	[GenerateShape]
	internal partial class Extensible
	{
		public string? Known { get; set; }

		[ShapeShiftExtensionData]
		public Dictionary<string, ShapeShiftValue> Extras { get; } = new(StringComparer.Ordinal);
	}

	[GenerateShape]
	internal partial record Bag(
		List<int> Items,
		HashSet<string> Tags,
		Dictionary<string, int> Words,
		Dictionary<int, string> Numbers,
		int[,] Grid,
		byte[] Blob);

	[GenerateShape]
	internal partial record Node(string Name, List<Node>? Children);

	[GenerateShape]
	[DerivedTypeShape(typeof(Circle), Name = "circle")]
	[DerivedTypeShape(typeof(Square), Tag = 3)]
	internal partial record Shape;

	internal sealed record Circle(double Radius) : Shape;

	internal sealed record Square(double Side) : Shape;

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
	internal partial record Exotic(
		DateTime Timestamp,
		DateTimeOffset Offset,
		TimeSpan Duration,
		decimal Money,
		Int128 Big,
		BigInteger Huge,
		char Initial,
		Rune Symbol,
		JsonElement Raw,
		ShapeShiftValue Dynamic);

	[GenerateShape]
	internal partial record Custom([property: ShapeShiftConverter(typeof(OpaqueConverter))] string Opaque);

	internal sealed class OpaqueConverter : ShapeShiftConverter<string, JsonEncoder, JsonDecoder>
	{
		public override string? Read(ref JsonDecoder decoder, SerializationContext<JsonEncoder, JsonDecoder> context) => decoder.ReadString();

		public override void Write(ref JsonEncoder encoder, in string? value, SerializationContext<JsonEncoder, JsonDecoder> context) => encoder.WriteValue(value!);
	}
}
