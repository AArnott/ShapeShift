// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ShapeShift.Tests;

namespace ShapeShift.Json.Tests;

/// <summary>
/// Verifies that (de)serialization failures carry an actionable <see cref="ShapeShiftPath"/> breadcrumb trail.
/// </summary>
public partial class JsonErrorPathTests : TestBase
{
	private readonly JsonSerializer serializer = new();

	[Test]
	public async Task NestedObjectProperty_ReportsPath()
	{
		ShapeShiftSerializationException ex = await this.AssertFails<Root>("""{"Child":{"Age":"nope"}}""");

		await Assert.That(ex.Path).IsEqualTo(new ShapeShiftPath("Child", "Age"));
		await Assert.That(ex.Message).Contains("$.Child.Age");
		await Assert.That(ex.InnerException).IsTypeOf<DecoderException>();
	}

	[Test]
	public async Task CollectionElement_ReportsIndex()
	{
		ShapeShiftSerializationException ex = await this.AssertFails<Root>("""{"Numbers":[1,2,"nope"]}""");

		await Assert.That(ex.Path).IsEqualTo(new ShapeShiftPath("Numbers", 2));
		await Assert.That(ex.Message).Contains("$.Numbers[2]");
	}

	[Test]
	public async Task NestedCollectionOfObjects_ReportsFullPath()
	{
		ShapeShiftSerializationException ex = await this.AssertFails<Root>("""{"Children":[{"Age":1},{"Age":"nope"}]}""");

		await Assert.That(ex.Path).IsEqualTo(new ShapeShiftPath("Children", 1, "Age"));
		await Assert.That(ex.Message).Contains("$.Children[1].Age");
	}

	[Test]
	public async Task StringKeyedMapValue_ReportsPropertyName()
	{
		ShapeShiftSerializationException ex = await this.AssertFails<Root>("""{"Map":{"a":1,"b":"nope"}}""");

		await Assert.That(ex.Path).IsEqualTo(new ShapeShiftPath("Map", "b"));
	}

	[Test]
	public async Task NonStringKeyedMapEntry_ReportsEntryAndSlot()
	{
		ShapeShiftSerializationException ex = await this.AssertFails<Root>("""{"IntMap":[[1,"one"],[2,3]]}""");

		await Assert.That(ex.Path).IsEqualTo(new ShapeShiftPath("IntMap", 1, 1));
		await Assert.That(ex.Message).Contains("$.IntMap[1][1]");
	}

	[Test]
	public async Task NonStringKeyedMapKey_ReportsKeySlot()
	{
		ShapeShiftSerializationException ex = await this.AssertFails<Root>("""{"IntMap":[["nope","one"]]}""");

		await Assert.That(ex.Path).IsEqualTo(new ShapeShiftPath("IntMap", 0, 0));
	}

	[Test]
	public async Task UnionPayload_ReportsPath()
	{
		ShapeShiftSerializationException ex = await this.AssertFails<Root>("""{"Choice":["named",{"Name":5}]}""");

		await Assert.That(ex.Path).IsEqualTo(new ShapeShiftPath("Choice", 1, "Name"));
	}

	[Test]
	public async Task UnknownUnionCase_ReportsEnclosingPath()
	{
		ShapeShiftSerializationException ex = await this.AssertFails<Root>("""{"Choice":["missing",{}]}""");

		await Assert.That(ex.Path).IsEqualTo(new ShapeShiftPath("Choice"));
	}

	[Test]
	public async Task MultidimensionalArrayElement_ReportsPath()
	{
		ShapeShiftSerializationException ex = await this.AssertFails<Root>("""{"Grid":[[2,2],[1,2,"nope",4]]}""");

		await Assert.That(ex.Path).IsEqualTo(new ShapeShiftPath("Grid", 1, 2));
	}

	[Test]
	public async Task ExtensionData_ReportsPath()
	{
		ShapeShiftSerializationException ex = await this.AssertFails<Extensible>("""{"unknown":[1,{]}""");

		await Assert.That(ex.Path[0]).IsEqualTo(ShapeShiftPathElement.Property("unknown"));
	}

	[Test]
	public async Task DuplicateProperty_ReportsPropertyPath()
	{
		ShapeShiftSerializationException ex = await this.AssertFails<Root>("""{"Age":1,"Age":2}""");

		await Assert.That(ex.Path).IsEqualTo(new ShapeShiftPath("Age"));
	}

	[Test]
	public async Task NonNullablePropertyAssignedNull_ReportsPath()
	{
		ShapeShiftSerializationException ex = await this.AssertFails<Root>("""{"Child":{"Name":null}}""");

		await Assert.That(ex.Path).IsEqualTo(new ShapeShiftPath("Child", "Name"));
	}

	[Test]
	public async Task ConverterThrowingDuringWrite_ReportsPath()
	{
		JsonSerializer custom = this.serializer with { Converters = [new ThrowingConverter()] };
		Root value = new() { Child = new Child { Name = "n", Explosive = new Boom() } };

		Func<string> act = () => custom.Serialize(value);

		ShapeShiftSerializationException ex = await Assert.That(act).Throws<ShapeShiftSerializationException>() ?? throw new InvalidOperationException();
		await Assert.That(ex.Path).IsEqualTo(new ShapeShiftPath("Child", "Explosive"));
		await Assert.That(ex.InnerException).IsTypeOf<NotSupportedException>();
	}

	[Test]
	public async Task ConverterThrowingDuringRead_ReportsPath()
	{
		JsonSerializer custom = this.serializer with { Converters = [new ThrowingConverter()] };

		Func<Root?> act = () => custom.Deserialize<Root>("""{"Child":{"Explosive":{}}}""");

		ShapeShiftSerializationException ex = await Assert.That(act).Throws<ShapeShiftSerializationException>() ?? throw new InvalidOperationException();
		await Assert.That(ex.Path).IsEqualTo(new ShapeShiftPath("Child", "Explosive"));
		await Assert.That(ex.InnerException).IsTypeOf<NotSupportedException>();
	}

	[Test]
	public async Task CollectionElementWrite_ReportsIndex()
	{
		JsonSerializer custom = this.serializer with { Converters = [new ThrowingConverter()] };
		ExplosiveList value = new() { Explosives = [new Boom(), new Boom()] };

		Func<string> act = () => custom.Serialize(value);

		ShapeShiftSerializationException ex = await Assert.That(act).Throws<ShapeShiftSerializationException>() ?? throw new InvalidOperationException();
		await Assert.That(ex.Path).IsEqualTo(new ShapeShiftPath("Explosives", 0));
	}

	[Test]
	public async Task Cancellation_IsNotWrapped()
	{
		using CancellationTokenSource cts = new();
		cts.Cancel();

		Func<Root?> act = () => this.serializer.Deserialize<Root>("""{"Child":{"Age":1}}""", cts.Token);

		await Assert.That(act).Throws<OperationCanceledException>();
	}

	[Test]
	public async Task SuccessfulRoundTrip_IsUnaffected()
	{
		Root value = new()
		{
			Age = 3,
			Child = new Child { Name = "n", Age = 4 },
			Numbers = [1, 2],
			Map = new Dictionary<string, int> { ["a"] = 1 },
		};

		Root? actual = this.serializer.Deserialize<Root>(this.serializer.Serialize(value));

		await Assert.That(actual?.Child?.Name).IsEqualTo("n");
		await Assert.That(actual?.Numbers?.Count).IsEqualTo(2);
	}

	[Test]
	public async Task PositionalRecordProperty_ReportsPath()
	{
		// Records with a parameterized constructor use a different object converter than
		// types with a default constructor, so breadcrumbs are verified for both.
		string json = """
			{
				"Id": 5,
				"Lines": [
					{ "Sku": "a-1", "Quantity": 2 },
					{ "Sku": "b-2", "Quantity": "two" }
				]
			}
			""";

		ShapeShiftSerializationException ex = await this.AssertFails<Order>(json);

		await Assert.That(ex.Path).IsEqualTo(new ShapeShiftPath("Lines", 1, "Quantity"));
		await Assert.That(ex.Message).Contains("$.Lines[1].Quantity");
	}

	private async Task<ShapeShiftSerializationException> AssertFails<T>(string json)
		where T : IShapeable<T>
	{
		Func<T?> act = () => this.serializer.Deserialize<T>(json);
		ShapeShiftSerializationException? ex = await Assert.That(act).Throws<ShapeShiftSerializationException>();
		return ex ?? throw new InvalidOperationException("Expected an exception.");
	}

	internal sealed class Boom;

	internal sealed class ThrowingConverter : ShapeShiftConverter<Boom, JsonEncoder, JsonDecoder>
	{
		public override Boom? Read(ref JsonDecoder decoder, SerializationContext<JsonEncoder, JsonDecoder> context)
			=> throw new NotSupportedException("read boom");

		public override void Write(ref JsonEncoder encoder, in Boom? value, SerializationContext<JsonEncoder, JsonDecoder> context)
			=> throw new NotSupportedException("write boom");
	}

	[GenerateShape]
	internal partial class Root
	{
		public int Age { get; set; }

		public Child? Child { get; set; }

		public List<int>? Numbers { get; set; }

		public List<Child>? Children { get; set; }

		public Dictionary<string, int>? Map { get; set; }

		public Dictionary<int, string>? IntMap { get; set; }

		public Choice? Choice { get; set; }

		public int[,]? Grid { get; set; }

		public Boom? Explosive { get; set; }

		public List<Boom>? Explosives { get; set; }
	}

	[GenerateShape]
	internal partial class ExplosiveList
	{
		public List<Boom>? Explosives { get; set; }
	}

	[GenerateShape]
	internal partial class Child
	{
		public string Name { get; set; } = string.Empty;

		public int Age { get; set; }

		public Boom? Explosive { get; set; }
	}

	[GenerateShape]
	[DerivedTypeShape(typeof(NamedChoice), Name = "named")]
	internal partial record Choice;

	internal sealed record NamedChoice : Choice
	{
		public string Name { get; set; } = string.Empty;
	}

	[GenerateShape]
	internal partial class Extensible
	{
		[ShapeShiftExtensionData]
		public Dictionary<string, ShapeShiftValue> Extra { get; } = new(StringComparer.Ordinal);
	}

	[GenerateShape]
	internal partial record Order(int Id, List<OrderLine> Lines);

	[GenerateShape]
	internal partial record OrderLine(string Sku, int Quantity);
}
