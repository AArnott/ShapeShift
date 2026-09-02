// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using ShapeShift.Tests;

namespace ShapeShift.Toml.Tests;

public partial class TomlSerializerTests : TestBase
{
	private string? lastSerializedToml;

	[Test]
	public async Task SimpleString()
	{
		string original = "Hello, World!";
		await this.AssertRoundtripAsync<string, Witness>(original);

		// TOML represents strings with quotes
		await Assert.That(this.lastSerializedToml).Contains("Hello, World!");
	}

	[Test, MatrixDataSource]
	public async Task SimpleBoolean(bool original)
	{
		await this.AssertRoundtripAsync<bool, Witness>(original);

		await Assert.That(this.lastSerializedToml?.Trim()).IsEqualTo(original ? "true" : "false");
	}

	[Test]
	public async Task SimpleInt32()
	{
		int original = 42;
		await this.AssertRoundtripAsync<int, Witness>(original);

		await Assert.That(this.lastSerializedToml?.Trim()).IsEqualTo(original.ToString(CultureInfo.InvariantCulture));
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
	public async Task EncoderWritesRootTableAndInlineContainers()
	{
		StringWriter writer = new(CultureInfo.InvariantCulture);
		TomlEncoder encoder = new(writer);
		encoder.WriteStartMap(2);
		encoder.WritePropertyName("title");
		encoder.WriteValue("TOML Example");
		encoder.WritePropertyName("owner");
		encoder.WriteStartMap(1);
		encoder.WritePropertyName("names");
		encoder.WriteStartVector(2);
		encoder.WriteValue("Ada");
		encoder.WriteValue("Grace");
		encoder.WriteEndVector();
		encoder.WriteEndMap();
		encoder.WriteEndMap();

		await Assert.That(writer.ToString()).IsEqualTo($"title = \"TOML Example\"{Environment.NewLine}owner = {{ names = [\"Ada\", \"Grace\"] }}");
	}

	[Test]
	public async Task DecoderReadsCommentsLiteralStringsAndTrailingCommas()
	{
		const string Toml = """
			# A regular TOML document
			FirstName = 'Ada'
			LastName = "Lovelace"
			""";

		Person? person = new TomlSerializer().Deserialize<Person>(Toml);

		await Assert.That(person).IsEqualTo(new Person { FirstName = "Ada", LastName = "Lovelace" });
	}

	protected ValueTask<T?> AssertRoundtripAsync<T>(T? value)
		where T : IShapeable<T> => this.AssertRoundtripAsync<T, T>(value);

	protected async ValueTask<T?> AssertRoundtripAsync<T, TProvider>(T? value)
		where TProvider : IShapeable<T>
	{
		TomlSerializer serializer = new();

		this.lastSerializedToml = serializer.Serialize<T, TProvider>(value);

		Console.WriteLine("Serialized form:");
		Console.WriteLine(this.lastSerializedToml);

		T? deserialized = serializer.Deserialize<T, TProvider>(this.lastSerializedToml);

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
