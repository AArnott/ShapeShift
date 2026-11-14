// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using ShapeShift.Tests;

namespace ShapeShift.Yaml.Tests;

public partial class YamlSerializerTests : TestBase
{
	private string? lastSerializedYaml;

	[Test]
	public async Task SimpleString()
	{
		string original = "Hello, World!";
		await this.AssertRoundtripAsync<string, Witness>(original);

		// In the case of a simple string, the yaml representation is equal to the string itself.
		await Assert.That(this.lastSerializedYaml).IsEqualTo(original);
	}

	[Test]
	public async Task InternEscapedStrings()
	{
		YamlSerializer serializer = new() { InternStrings = true };
		Person original = new() { FirstName = "First line\nSecond line", LastName = "First line\nSecond line" };
		string serialized = serializer.Serialize<Person, Person>(original);

		Person? deserialized = serializer.Deserialize<Person, Person>(serialized);

		await Assert.That(deserialized).IsNotNull();
		await Assert.That(ReferenceEquals(deserialized!.FirstName, deserialized.LastName)).IsTrue();
	}

	[Test, MatrixDataSource]
	public async Task SimpleBoolean(bool original)
	{
		await this.AssertRoundtripAsync<bool, Witness>(original);

		await Assert.That(this.lastSerializedYaml?.Trim()).IsEqualTo(original ? "true" : "false");
	}

	[Test]
	public async Task SimpleInt32()
	{
		int original = 42;
		await this.AssertRoundtripAsync<int, Witness>(original);

		// In the case of an integer, the yaml representation is equal the a simple ToString on the integer.
		await Assert.That(this.lastSerializedYaml?.Trim()).IsEqualTo(original.ToString(CultureInfo.InvariantCulture));
	}

	[Test]
	public async Task SimpleRecordWithDefaultCtor()
	{
		Person person = new() { FirstName = "John", LastName = "Doe" };
		await this.AssertRoundtripAsync(person);
	}

	[Test]
	public async Task SimpleRecordWithNonDefaultCtor()
	{
		PersonWithInit person = new("John", "Doe");
		await this.AssertRoundtripAsync(person);
	}

	[Test]
	public async Task ListOfRecords()
	{
		Family family = new()
		{
			Members =
			[
				new Person { FirstName = "John", LastName = "Doe" },
				new Person { FirstName = "Jane", LastName = "Doe" },
			],
		};

		await this.AssertRoundtripAsync(family);
	}

	[Test]
	public async Task BlockSequence_AtMappingKeyColumn_IsTheValue()
	{
		YamlSerializer serializer = new();
		ShapeShiftValue? value = serializer.Deserialize<ShapeShiftValue>("one:\n- 2\n- 3\nfour: 5\n");
		ShapeShiftMap map = (ShapeShiftMap)value!;
		ShapeShiftArray one = (ShapeShiftArray)map.Properties["one"];

		await Assert.That(one.Items.Count).IsEqualTo(2);
		await Assert.That(one.Items[0]).IsEqualTo((ShapeShiftValue)2L);
		await Assert.That(one.Items[1]).IsEqualTo((ShapeShiftValue)3L);
		await Assert.That(map.Properties["four"]).IsEqualTo((ShapeShiftValue)5L);

		ShapeShiftValue? indented = serializer.Deserialize<ShapeShiftValue>("foo:\n- 42\nbar:\n  - 44\n");
		ShapeShiftMap indentedMap = (ShapeShiftMap)indented!;
		await Assert.That(((ShapeShiftArray)indentedMap.Properties["foo"]).Items[0]).IsEqualTo((ShapeShiftValue)42L);
		await Assert.That(((ShapeShiftArray)indentedMap.Properties["bar"]).Items[0]).IsEqualTo((ShapeShiftValue)44L);
	}

	[Test]
	public async Task BlockSequence_EmptyItem_IsNullBeforeTheNextSibling()
	{
		YamlSerializer serializer = new();
		ShapeShiftValue? value = serializer.Deserialize<ShapeShiftValue>("items:\n  -\n  - 2\n");
		ShapeShiftArray items = (ShapeShiftArray)((ShapeShiftMap)value!).Properties["items"];

		await Assert.That(items.Items.Count).IsEqualTo(2);
		await Assert.That(items.Items[0]).IsEqualTo(ShapeShiftValue.Null);
		await Assert.That(items.Items[1]).IsEqualTo((ShapeShiftValue)2L);

		ShapeShiftValue? nested = serializer.Deserialize<ShapeShiftValue>("items:\n  -\n    - 2\n");
		ShapeShiftArray nestedItems = (ShapeShiftArray)((ShapeShiftMap)nested!).Properties["items"];
		ShapeShiftArray child = (ShapeShiftArray)nestedItems.Items[0];
		await Assert.That(child.Items.Count).IsEqualTo(1);
		await Assert.That(child.Items[0]).IsEqualTo((ShapeShiftValue)2L);
	}

	[Test]
	public async Task BlockSequence_RejectsKeyAtIndentedItemColumn()
	{
		YamlSerializer serializer = new();
		Func<ShapeShiftValue?> read = () => serializer.Deserialize<ShapeShiftValue>("one:\n  - 2\n  two: 3\n");

		await Assert.That(read).Throws<DecoderException>();
	}

	protected ValueTask<T?> AssertRoundtripAsync<T>(T? value)
		where T : IShapeable<T> => this.AssertRoundtripAsync<T, T>(value);

	protected async ValueTask<T?> AssertRoundtripAsync<T, TProvider>(T? value)
		where TProvider : IShapeable<T>
	{
		YamlSerializer serializer = new();

		this.lastSerializedYaml = serializer.Serialize<T, TProvider>(value);

		Console.WriteLine("Serialized form:");
		Console.WriteLine(this.lastSerializedYaml);

		T? deserialized = serializer.Deserialize<T, TProvider>(this.lastSerializedYaml);

		await Assert.That(deserialized).IsEqualTo(value);
		return deserialized;
	}

	[GenerateShape]
	internal partial record Person
	{
		public string? FirstName { get; set; }

		public string? LastName { get; set; }
	}

	[GenerateShape]
	internal partial record PersonWithInit(string FirstName, string LastName);

	[GenerateShape]
	internal partial record Family
	{
		public List<Person> Members { get; set; } = [];

		public virtual bool Equals(Family? other)
		{
			return other is not null && this.Members.SequenceEqual(other.Members);
		}

		public override int GetHashCode()
		{
			HashCode hash = default;
			foreach (Person member in this.Members)
			{
				hash.Add(member);
			}

			return hash.ToHashCode();
		}
	}

	[GenerateShapeFor<string>]
	[GenerateShapeFor<int>]
	[GenerateShapeFor<bool>]
	private partial class Witness;
}
