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
