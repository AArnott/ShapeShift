// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using ShapeShift.Tests;

namespace ShapeShift.Toml.Tests;

public partial class TomlSerializerTests : TestBase
{
	private string? lastSerializedToml;

	[Test]
	public async Task RootScalarIsRejected()
	{
		TomlSerializer serializer = new();

		void Serialize() => serializer.Serialize<string, Witness>("Hello, World!");
		await Assert.That(Serialize).Throws<NotSupportedException>();
	}

	[Test]
	public async Task DecimalIsRejected()
	{
		await Assert.That(WriteDecimal).Throws<NotSupportedException>();

		static void WriteDecimal()
		{
			StringWriter writer = new(CultureInfo.InvariantCulture);
			TomlEncoder encoder = new(writer);
			encoder.WriteStartMap(1);
			encoder.WritePropertyName("value");
			encoder.WriteValue(0.1m);
		}
	}

	[Test]
	public async Task DecimalReadIsRejected()
	{
		await Assert.That(ReadDecimal).Throws<NotSupportedException>();

		static void ReadDecimal()
		{
			StringReader reader = new("value = 0.1");
			TomlDecoder decoder = new(reader);
			decoder.ReadStartMap();
			decoder.ReadPropertyName();
			decoder.ReadDecimal();
		}
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
	public async Task EncoderWritesTableSections()
	{
		StringWriter writer = new(CultureInfo.InvariantCulture);
		TomlEncoder encoder = new(writer);
		encoder.WriteStartMap(2);
		encoder.WritePropertyName("owner");
		encoder.WriteStartMap(1);
		encoder.WritePropertyName("names");
		encoder.WriteStartVector(2);
		encoder.WriteValue("Ada");
		encoder.WriteValue("Grace");
		encoder.WriteEndVector();
		encoder.WriteEndMap();
		encoder.WritePropertyName("title");
		encoder.WriteValue("TOML Example");
		encoder.WriteEndMap();

		await Assert.That(writer.ToString()).Contains("[owner]");
		await Assert.That(writer.ToString()).Contains("names = [\"Ada\", \"Grace\"]");
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

	[Test]
	public async Task CargoManifestRoundtrips()
	{
		const string CargoToml = """"
			[package]
			name = "shape-shift"
			version = "1.0.0"
			description = """
			A serializer
			for structured data"""

			[dependencies]
			serde = { version = "1", features = ["derive"] }

			[[bin]]
			name = "shape-shift"
			path = "src/main.rs"

			[[bin]]
			name = "shape-shift-admin"
			path = "src/admin.rs"
			"""";
		TomlSerializer serializer = new() { PropertyNamingPolicy = ShapeShiftNamingPolicy.CamelCase };

		CargoManifest? manifest = serializer.Deserialize<CargoManifest>(CargoToml);
		string encoded = serializer.Serialize(manifest);
		CargoManifest? copy = serializer.Deserialize<CargoManifest>(encoded);
		manifest = manifest ?? throw new InvalidOperationException("The Cargo manifest was not deserialized.");
		copy = copy ?? throw new InvalidOperationException("The encoded Cargo manifest was not deserialized.");

		await Assert.That(copy.Package).IsEqualTo(manifest.Package);
		await Assert.That(copy.Dependencies.Keys.SequenceEqual(manifest.Dependencies.Keys)).IsTrue();
		await Assert.That(copy.Dependencies["serde"].Version).IsEqualTo(manifest.Dependencies["serde"].Version);
		await Assert.That(copy.Dependencies["serde"].Features.SequenceEqual(manifest.Dependencies["serde"].Features)).IsTrue();
		await Assert.That(copy.Bin.SequenceEqual(manifest.Bin)).IsTrue();
		await Assert.That(copy.Bin).Count().IsEqualTo(2);
		await Assert.That(encoded).Contains("[package]");
		await Assert.That(encoded).Contains("[dependencies]");
		await Assert.That(encoded.Split("[[bin]]", StringSplitOptions.None)).Count().IsEqualTo(3);
	}

	[Test]
	public async Task DottedQuotedKeysCreateNestedTables()
	{
		const string Toml = "target.'cfg(windows)'.dependencies.winapi = \"0.3\"";
		string[] values;
		{
			StringReader reader = new(Toml);
			TomlDecoder decoder = new(reader);
			decoder.ReadStartMap();
			string target = decoder.ReadPropertyName().ToString();
			decoder.ReadStartMap();
			string condition = decoder.ReadPropertyName().ToString();
			decoder.ReadStartMap();
			string dependencies = decoder.ReadPropertyName().ToString();
			decoder.ReadStartMap();
			string dependency = decoder.ReadPropertyName().ToString();
			values = [target, condition, dependencies, dependency, decoder.ReadString()];
		}

		await Assert.That(values.SequenceEqual(["target", "cfg(windows)", "dependencies", "winapi", "0.3"])).IsTrue();
	}

	[Test]
	public async Task DuplicateKeysAreRejected()
	{
		await Assert.That(ParseInvalidToml).Throws<DecoderException>();

		static void ParseInvalidToml()
		{
			StringReader reader = new("key = 1\nkey = 2");
			_ = new TomlDecoder(reader);
		}
	}

	[Test]
	[Arguments("decimal = 1_000\nhex = 0xDEAD_BEEF\noctal = 0o755\nbinary = 0b1101\nfloat = 6.626e-34\npositive = +inf\nnot_a_number = nan\nmixed = [1, 'two', true]")]
	[Arguments("offset_z = 1979-05-27T07:32:00Z\noffset = 1979-05-27T00:32:00-07:00\nlocal_datetime = 1979-05-27T07:32:00\nlocal_date = 1979-05-27\nlocal_time = 07:32:00")]
	[Arguments("basic = \"\"\"first line\\nsecond line\"\"\"\nliteral = '''C:\\\\Users\\nodejs\\templates'''")]
	[Arguments("[[fruits]]\nname = 'apple'\n[fruits.physical]\ncolor = 'red'\n[[fruits.varieties]]\nname = 'red delicious'\n[[fruits.varieties]]\nname = 'granny smith'\n[[fruits]]\nname = 'banana'\n[[fruits.varieties]]\nname = 'plantain'")]
	public async Task Toml10ValidDocumentsAreAccepted(string toml)
	{
		StringReader reader = new(toml);
		TomlDecoder decoder = new(reader);
		decoder.Skip();

		await Assert.That(decoder.NextTokenType).IsEqualTo(TokenType.EndDocument);
	}

	[Test]
	[Arguments("value = 01")]
	[Arguments("value = { first = 1, }")]
	[Arguments("value = 1979-05-27T07:32:00+24:00")]
	[Arguments("[table]\nvalue = 1\n[table]\nother = 2")]
	public async Task Toml10InvalidDocumentsAreRejected(string toml)
	{
		void Parse()
		{
			StringReader reader = new(toml);
			_ = new TomlDecoder(reader);
		}

		await Assert.That(Parse).Throws<DecoderException>();
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

	[GenerateShape]
	internal partial record CargoManifest
	{
		public Package Package { get; init; } = new();

		public Dictionary<string, Dependency> Dependencies { get; init; } = [];

		public List<BinaryTarget> Bin { get; init; } = [];
	}

	[GenerateShape]
	internal partial record Package
	{
		public string Name { get; init; } = string.Empty;

		public string Version { get; init; } = string.Empty;

		public string Description { get; init; } = string.Empty;
	}

	[GenerateShape]
	internal partial record Dependency
	{
		public string Version { get; init; } = string.Empty;

		public List<string> Features { get; init; } = [];
	}

	[GenerateShape]
	internal partial record BinaryTarget
	{
		public string Name { get; init; } = string.Empty;

		public string Path { get; init; } = string.Empty;
	}

	[GenerateShapeFor<string>]
	[GenerateShapeFor<int>]
	[GenerateShapeFor<bool>]
	private partial class Witness;
}
