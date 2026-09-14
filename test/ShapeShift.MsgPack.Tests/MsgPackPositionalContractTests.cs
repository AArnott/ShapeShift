// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;
using ShapeShift.Json;
using ShapeShift.Schema;
using ShapeShift.Tests;

namespace ShapeShift.MsgPack.Tests;

/// <summary>
/// Verifies the explicit positional (array) contract mode: its wire shape, its versioning behavior, the way it
/// handles omitted values, the errors it raises for contracts it cannot honor, and how it is described as schema.
/// </summary>
public partial class MsgPackPositionalContractTests : TestBase
{
	private readonly MsgPackSerializer serializer = new();

	[Test]
	public async Task MapIsStillTheDefault()
	{
		byte[] encoded = this.serializer.Serialize(new MapPerson("Ada", 36));

		await Assert.That(encoded[0]).IsEqualTo((byte)0x82);
	}

	[Test]
	public async Task Positional_WritesAnArray()
	{
		byte[] encoded = this.serializer.Serialize(new Person("Ada", 36));

		await Assert.That(encoded[0]).IsEqualTo((byte)0x92);
		await Assert.That(encoded.Length).IsLessThan(this.serializer.Serialize(new MapPerson("Ada", 36)).Length);
	}

	[Test]
	public async Task Positional_RoundTrips()
	{
		Person value = new("Ada", 36);

		Person? actual = this.serializer.Deserialize<Person>(this.serializer.Serialize(value));

		await Assert.That(actual).IsEqualTo(value);
	}

	[Test]
	public async Task Positional_RoundTripsAMutableType()
	{
		MutablePerson value = new() { Name = "Ada", Age = 36 };

		MutablePerson? actual = this.serializer.Deserialize<MutablePerson>(this.serializer.Serialize(value));

		await Assert.That(actual!.Name).IsEqualTo("Ada");
		await Assert.That(actual.Age).IsEqualTo(36);
	}

	[Test]
	public async Task Positional_HonorsDeclaredOrderNotDeclarationOrder()
	{
		// Name is declared second but takes position 0, so it must be written first.
		byte[] encoded = this.serializer.Serialize(new Reordered(36, "Ada"));

		await Assert.That(encoded[1]).IsEqualTo((byte)0xa3);
	}

	[Test]
	public async Task RetiredPosition_IsWrittenAsANullPlaceholder()
	{
		byte[] encoded = this.serializer.Serialize(new WithHole("Ada", 36));

		// Three elements: position 0, the retired position 1 (nil), then position 2.
		await Assert.That(encoded[0]).IsEqualTo((byte)0x93);
		await Assert.That(encoded[5]).IsEqualTo((byte)0xc0);
		await Assert.That(this.serializer.Deserialize<WithHole>(encoded)).IsEqualTo(new WithHole("Ada", 36));
	}

	[Test]
	public async Task OlderPayload_LeavesAppendedMembersAtTheirDefaults()
	{
		// A writer that only knew positions 0 and 1.
		byte[] older = this.serializer.Serialize(new Person("Ada", 36));

		PersonV2? actual = this.serializer.Deserialize<PersonV2>(older);

		await Assert.That(actual!.Name).IsEqualTo("Ada");
		await Assert.That(actual.Age).IsEqualTo(36);
		await Assert.That(actual.City).IsNull();
	}

	[Test]
	public async Task NewerPayload_SkipsPositionsTheReaderDoesNotKnow()
	{
		byte[] newer = this.serializer.Serialize(new PersonV2 { Name = "Ada", Age = 36, City = "London" });

		Person? actual = this.serializer.Deserialize<Person>(newer);

		await Assert.That(actual).IsEqualTo(new Person("Ada", 36));
	}

	[Test]
	public async Task NewerPayload_WithComplexSurplusMembers_IsStillReadable()
	{
		byte[] newer = this.serializer.Serialize(new PersonV3 { Name = "Ada", Age = 36, Tags = ["x", "y"] });

		Person? actual = this.serializer.Deserialize<Person>(newer);

		await Assert.That(actual).IsEqualTo(new Person("Ada", 36));
	}

	[Test]
	public async Task TrailingDefaults_AreElidedWhenThePolicyAllowsIt()
	{
		MsgPackSerializer terse = this.serializer with { SerializeDefaultValues = SerializeDefaultValuesPolicy.Never };

		byte[] encoded = terse.Serialize(new MutablePerson { Name = "Ada", Age = 0 });

		// Only position 0 survives: the array header says one element.
		await Assert.That(encoded[0]).IsEqualTo((byte)0x91);
		MutablePerson? actual = terse.Deserialize<MutablePerson>(encoded);
		await Assert.That(actual!.Name).IsEqualTo("Ada");
		await Assert.That(actual.Age).IsEqualTo(0);
	}

	[Test]
	public async Task InteriorDefaults_AreAlwaysWritten()
	{
		// An array cannot say "this interior element is absent" as distinct from "this element is null", so a
		// positional contract declines omission for anything but the tail.
		MsgPackSerializer terse = this.serializer with { SerializeDefaultValues = SerializeDefaultValuesPolicy.Never };

		byte[] encoded = terse.Serialize(new MutablePerson { Name = null, Age = 36 });

		await Assert.That(encoded[0]).IsEqualTo((byte)0x92);
		await Assert.That(encoded[1]).IsEqualTo((byte)0xc0);
		MutablePerson? actual = terse.Deserialize<MutablePerson>(encoded);
		await Assert.That(actual!.Name).IsNull();
		await Assert.That(actual.Age).IsEqualTo(36);
	}

	[Test]
	public async Task RequiredMembers_AreNeverElided()
	{
		MsgPackSerializer terse = this.serializer with { SerializeDefaultValues = SerializeDefaultValuesPolicy.Never };

		byte[] encoded = terse.Serialize(new Person(string.Empty, 0));

		await Assert.That(encoded[0]).IsEqualTo((byte)0x92);
		await Assert.That(terse.Deserialize<Person>(encoded)).IsEqualTo(new Person(string.Empty, 0));
	}

	[Test]
	public async Task MissingRequiredMember_IsRejected()
	{
		// An empty array supplies neither constructor argument.
		byte[] truncated = [0x90];

		Func<Person?> deserialize = () => this.serializer.Deserialize<Person>(truncated);

		await Assert.That(deserialize).Throws<ShapeShiftSerializationException>();
	}

	[Test]
	public async Task MissingRequiredMember_IsAcceptedWhenThePolicyAllowsIt()
	{
		MsgPackSerializer lenient = this.serializer with { DeserializeDefaultValues = DeserializeDefaultValuesPolicy.AllowMissingValuesForRequiredProperties };

		Person? actual = lenient.Deserialize<Person>([0x90]);

		await Assert.That(actual!.Age).IsEqualTo(0);
	}

	[Test]
	public async Task NullForANonNullableMember_IsRejected()
	{
		byte[] payload = [0x92, 0xc0, 36];

		Func<Person?> deserialize = () => this.serializer.Deserialize<Person>(payload);

		await Assert.That(deserialize).Throws<ShapeShiftSerializationException>();
	}

	[Test]
	public async Task NonArrayPayload_IsRejected()
	{
		byte[] asMap = this.serializer.Serialize(new MapPerson("Ada", 36));

		Func<Person?> deserialize = () => this.serializer.Deserialize<Person>(asMap);

		await Assert.That(deserialize).ThrowsException();
	}

	[Test]
	public async Task NullPayload_ReadsAsNull()
	{
		MutablePerson? actual = this.serializer.Deserialize<MutablePerson>([0xc0]);

		await Assert.That(actual).IsNull();
	}

	[Test]
	public async Task NestedPositionalTypes_RoundTrip()
	{
		Envelope value = new(new Person("Ada", 36), [new Person("Bob", 40)]);

		Envelope? actual = this.serializer.Deserialize<Envelope>(this.serializer.Serialize(value));

		await Assert.That(actual!.Owner).IsEqualTo(value.Owner);
		await Assert.That(actual.Others.SequenceEqual(value.Others)).IsTrue();
	}

	[Test]
	public async Task RecursivePositionalType_RoundTrips()
	{
		Tree value = new() { Name = "root", Child = new Tree { Name = "leaf" } };

		Tree? actual = this.serializer.Deserialize<Tree>(this.serializer.Serialize(value));

		await Assert.That(actual!.Name).IsEqualTo("root");
		await Assert.That(actual.Child!.Name).IsEqualTo("leaf");
	}

	[Test]
	public async Task MemberWithoutAKey_IsRejected()
	{
		Func<byte[]> serialize = () => this.serializer.Serialize(new MissingKey { Name = "Ada", Age = 1 });

		ShapeShiftSerializationException? caught = Capture(serialize);

		await Assert.That(caught).IsNotNull();
		await Assert.That(caught!.Message).Contains("MsgPackKeyAttribute");
	}

	[Test]
	public async Task DuplicateKeys_AreRejected()
	{
		Func<byte[]> serialize = () => this.serializer.Serialize(new DuplicateKeys { First = "a", Second = "b" });

		ShapeShiftSerializationException? caught = Capture(serialize);

		await Assert.That(caught).IsNotNull();
		await Assert.That(caught!.Message).Contains("both declare position");
	}

	[Test]
	public async Task OutOfRangeKey_IsRejected()
	{
		Func<byte[]> serialize = () => this.serializer.Serialize(new OutOfRangeKey { Name = "a" });

		ShapeShiftSerializationException? caught = Capture(serialize);

		await Assert.That(caught).IsNotNull();
		await Assert.That(caught!.Message).Contains("outside the supported range");
	}

	[Test]
	public async Task NegativeKey_IsRejected()
	{
		Func<byte[]> serialize = () => this.serializer.Serialize(new NegativeKey { Name = "a" });

		ShapeShiftSerializationException? caught = Capture(serialize);

		await Assert.That(caught).IsNotNull();
		await Assert.That(caught!.Message).Contains("outside the supported range");
	}

	[Test]
	public async Task ExtensionData_IsRejected()
	{
		Func<byte[]> serialize = () => this.serializer.Serialize(new PositionalWithExtensionData { Name = "a" });

		ShapeShiftSerializationException? caught = Capture(serialize);

		await Assert.That(caught).IsNotNull();
		await Assert.That(caught!.Message).Contains("extension-data member");
	}

	[Test]
	public async Task ErrorsCarryThePositionBreadcrumb()
	{
		// Position 1 holds a string where an int belongs.
		byte[] payload = [0x92, 0xa3, (byte)'A', (byte)'d', (byte)'a', 0xa1, (byte)'x'];

		ShapeShiftSerializationException? caught = Capture(() => this.serializer.Deserialize<Person>(payload));

		await Assert.That(caught).IsNotNull();
		await Assert.That(caught!.Path.ToString()).IsEqualTo("$[1]");
	}

	[Test]
	public async Task Contract_DescribesPositions()
	{
		var contract = (ObjectContract)this.serializer.GetContract<Person>();

		await Assert.That(contract.Encoding).IsEqualTo(ObjectEncoding.Positional);
		await Assert.That(contract.Properties.Length).IsEqualTo(2);
		await Assert.That(contract.Properties.All(p => p.Position is not null)).IsTrue();
		await Assert.That(contract.Properties.Single(p => p.Name == "Name").Position).IsEqualTo(0);
		await Assert.That(contract.Properties.Single(p => p.Name == "Age").Position).IsEqualTo(1);
	}

	[Test]
	public async Task Schema_ProjectsPositionsAsPrefixItems()
	{
		JsonObject schema = JsonSchema.Create(
			this.serializer.GetContract<WithHole>(),
			new JsonSchemaOptions { Profile = JsonSchemaProfile.MessagePack });

		await Assert.That((string?)schema["type"]).IsEqualTo("array");
		await Assert.That((string?)schema["x-msgpack-type"]).IsEqualTo("array");
		JsonArray prefixItems = (JsonArray)schema["prefixItems"]!;
		await Assert.That(prefixItems.Count).IsEqualTo(3);
		await Assert.That((string?)prefixItems[0]!["title"]).IsEqualTo("Name");
		await Assert.That((string?)prefixItems[1]!["type"]).IsEqualTo("null");
		await Assert.That((string?)prefixItems[2]!["title"]).IsEqualTo("Age");
	}

	private static ShapeShiftSerializationException? Capture(Delegate operation)
	{
		try
		{
			operation.DynamicInvoke();
			return null;
		}
		catch (Exception ex)
		{
			for (Exception? candidate = ex; candidate is not null; candidate = candidate.InnerException)
			{
				if (candidate is ShapeShiftSerializationException serializationException)
				{
					return serializationException;
				}
			}

			throw;
		}
	}

	[GenerateShape]
	internal partial record MapPerson(string Name, int Age);

	[GenerateShape]
	[MsgPackArrayContract]
	internal partial record Person([property: MsgPackKey(0)] string Name, [property: MsgPackKey(1)] int Age);

	[GenerateShape]
	[MsgPackArrayContract]
	internal partial record Reordered([property: MsgPackKey(1)] int Age, [property: MsgPackKey(0)] string Name);

	[GenerateShape]
	[MsgPackArrayContract]
	internal partial record WithHole([property: MsgPackKey(0)] string Name, [property: MsgPackKey(2)] int Age);

	[GenerateShape]
	[MsgPackArrayContract]
	internal partial record Envelope([property: MsgPackKey(0)] Person Owner, [property: MsgPackKey(1)] List<Person> Others);

	[GenerateShape]
	[MsgPackArrayContract]
	internal partial class MutablePerson
	{
		[MsgPackKey(0)]
		public string? Name { get; set; }

		[MsgPackKey(1)]
		public int Age { get; set; }
	}

	[GenerateShape]
	[MsgPackArrayContract]
	internal partial class PersonV2
	{
		[MsgPackKey(0)]
		public string? Name { get; set; }

		[MsgPackKey(1)]
		public int Age { get; set; }

		[MsgPackKey(2)]
		public string? City { get; set; }
	}

	[GenerateShape]
	[MsgPackArrayContract]
	internal partial class PersonV3
	{
		[MsgPackKey(0)]
		public string? Name { get; set; }

		[MsgPackKey(1)]
		public int Age { get; set; }

		[MsgPackKey(2)]
		public List<string> Tags { get; set; } = [];
	}

	[GenerateShape]
	[MsgPackArrayContract]
	internal partial class Tree
	{
		[MsgPackKey(0)]
		public string? Name { get; set; }

		[MsgPackKey(1)]
		public Tree? Child { get; set; }
	}

	[GenerateShape]
	[MsgPackArrayContract]
	internal partial class MissingKey
	{
		[MsgPackKey(0)]
		public string? Name { get; set; }

		public int Age { get; set; }
	}

	[GenerateShape]
	[MsgPackArrayContract]
	internal partial class DuplicateKeys
	{
		[MsgPackKey(0)]
		public string? First { get; set; }

		[MsgPackKey(0)]
		public string? Second { get; set; }
	}

	[GenerateShape]
	[MsgPackArrayContract]
	internal partial class OutOfRangeKey
	{
		[MsgPackKey(5000)]
		public string? Name { get; set; }
	}

	[GenerateShape]
	[MsgPackArrayContract]
	internal partial class NegativeKey
	{
		[MsgPackKey(-1)]
		public string? Name { get; set; }
	}

	[GenerateShape]
	[MsgPackArrayContract]
	internal partial class PositionalWithExtensionData
	{
		[MsgPackKey(0)]
		public string? Name { get; set; }

		[ShapeShiftExtensionData]
		public Dictionary<string, ShapeShiftValue> Extra { get; } = new(StringComparer.Ordinal);
	}
}
