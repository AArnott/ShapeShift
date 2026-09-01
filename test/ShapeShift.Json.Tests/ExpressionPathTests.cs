// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Linq.Expressions;
using ShapeShift.Tests;

namespace ShapeShift.Json.Tests;

/// <summary>
/// Tests for the expression-based path API,
/// <see cref="ShapeShiftSerializer{TEncoder, TDecoder}.GetPath{TRoot, TValue}(Expression{Func{TRoot, TValue}}, ITypeShape{TRoot})"/>.
/// </summary>
/// <remarks>
/// The translation itself lives in the format-neutral core library but needs a concrete serializer to run.
/// <see cref="JsonSerializer"/> hosts it here, and also proves that the resulting paths locate the intended
/// values in a real payload.
/// </remarks>
public partial class ExpressionPathTests : TestBase
{
	private readonly JsonSerializer serializer = new();

	[Test]
	public async Task RootSelector_IsRootPath()
	{
		await Assert.That(this.serializer.GetPath((Person p) => p)).IsEqualTo(ShapeShiftPath.Root);
	}

	[Test]
	public async Task MemberAccess_UsesSerializedName()
	{
		await Assert.That(this.serializer.GetPath((Person p) => p.Name)).IsEqualTo(new ShapeShiftPath("Name"));
	}

	[Test]
	public async Task NestedMemberChain_ProducesOneElementPerStep()
	{
		await Assert.That(this.serializer.GetPath((Person p) => p.HomeAddress!.City)).IsEqualTo(new ShapeShiftPath("HomeAddress", "City"));
	}

	[Test]
	public async Task NullForgivingOperator_IsInvisible()
	{
		// The null-forgiving operator produces no expression node at all, so both forms must agree.
#pragma warning disable CS8602 // Dereference of a possibly null reference: the expression is never executed.
		await Assert.That(this.serializer.GetPath((Person p) => p.HomeAddress!.City))
			.IsEqualTo(this.serializer.GetPath((Person p) => p.HomeAddress.City));
#pragma warning restore CS8602
	}

	[Test]
	public async Task NamingPolicy_IsApplied()
	{
		JsonSerializer camel = this.serializer with { PropertyNamingPolicy = ShapeShiftNamingPolicy.CamelCase };

		await Assert.That(camel.GetPath((Person p) => p.HomeAddress!.City)).IsEqualTo(new ShapeShiftPath("homeAddress", "city"));
	}

	[Test]
	public async Task PropertyShapeAlias_WinsOverNamingPolicy()
	{
		JsonSerializer camel = this.serializer with { PropertyNamingPolicy = ShapeShiftNamingPolicy.CamelCase };

		await Assert.That(this.serializer.GetPath((Person p) => p.Nickname)).IsEqualTo(new ShapeShiftPath("nick"));
		await Assert.That(camel.GetPath((Person p) => p.Nickname)).IsEqualTo(new ShapeShiftPath("nick"));
	}

	[Test]
	public async Task ArrayIndex_ProducesVectorElement()
	{
		await Assert.That(this.serializer.GetPath((Person p) => p.Tags![1])).IsEqualTo(new ShapeShiftPath("Tags", 1));
	}

	[Test]
	public async Task ListIndex_ProducesVectorElement()
	{
		await Assert.That(this.serializer.GetPath((Person p) => p.PreviousAddresses![2].Zip)).IsEqualTo(new ShapeShiftPath("PreviousAddresses", 2, "Zip"));
	}

	[Test]
	public async Task ImmutableArrayIndex_ProducesVectorElement()
	{
		await Assert.That(this.serializer.GetPath((Person p) => p.Scores[0])).IsEqualTo(new ShapeShiftPath("Scores", 0));
	}

	[Test]
	public async Task StringKeyedDictionary_ProducesPropertyElement()
	{
		await Assert.That(this.serializer.GetPath((Person p) => p.Attributes!["hue"])).IsEqualTo(new ShapeShiftPath("Attributes", "hue"));
	}

	[Test]
	public async Task NullableValueAccess_IsSteppedOver()
	{
		await Assert.That(this.serializer.GetPath((Person p) => p.Location!.Value.Latitude)).IsEqualTo(new ShapeShiftPath("Location", "Latitude"));
	}

	[Test]
	public async Task NullableLeaf_NeedsNoSpecialHandling()
	{
		await Assert.That(this.serializer.GetPath((Person p) => p.Age)).IsEqualTo(new ShapeShiftPath("Age"));
	}

	[Test]
	public async Task BoxingConversion_IsSteppedOver()
	{
		await Assert.That(this.serializer.GetPath((Person p) => (object?)p.Age)).IsEqualTo(new ShapeShiftPath("Age"));
	}

	[Test]
	public async Task WideningReferenceConversion_IsSteppedOver()
	{
		await Assert.That(this.serializer.GetPath((Person p) => (object?)p.HomeAddress)).IsEqualTo(new ShapeShiftPath("HomeAddress"));
	}

	[Test]
	public async Task TypeShapeOverload_MatchesShapeableOverload()
	{
		await Assert.That(this.serializer.GetPath((Person p) => p.Name, Shape<Person>()))
			.IsEqualTo(this.serializer.GetPath((Person p) => p.Name));
	}

	[Test]
	public async Task WitnessOverload_MatchesShapeableOverload()
	{
		await Assert.That(this.serializer.GetPath<Person, string?, PersonWitness>(p => p.Name))
			.IsEqualTo(this.serializer.GetPath((Person p) => p.Name));
	}

	[Test]
	public async Task NarrowingConversion_IsRejected()
	{
		NotSupportedException ex = await ThrowsAsync<NotSupportedException>(() => this.serializer.GetPath((Person p) => (long)p.Salary));

		await Assert.That(ex.Message).Contains("conversion");
		await Assert.That(ex.Message).Contains("Salary");
	}

	[Test]
	public async Task DowncastConversion_IsRejected()
	{
		NotSupportedException ex = await ThrowsAsync<NotSupportedException>(() => this.serializer.GetPath((Person p) => ((DetailedAddress)p.HomeAddress!).Country));

		await Assert.That(ex.Message).Contains("conversion");
	}

	[Test]
	public async Task ComputedIndex_IsRejected()
	{
		NotSupportedException ex = await ThrowsAsync<NotSupportedException>(() => this.serializer.GetPath((Person p) => p.Tags![p.Tags.Length - 1]));

		await Assert.That(ex.Message).Contains("constant index");
	}

	[Test]
	public async Task CapturedVariableIndex_IsRejected()
	{
		int index = 3;
		NotSupportedException ex = await ThrowsAsync<NotSupportedException>(() => this.serializer.GetPath((Person p) => p.Tags![index]));

		await Assert.That(ex.Message).Contains("constant index");
		await Assert.That(ex.Message).Contains(nameof(ShapeShiftPath));
	}

	[Test]
	public async Task MethodCall_IsRejected()
	{
		NotSupportedException ex = await ThrowsAsync<NotSupportedException>(() => this.serializer.GetPath((Person p) => p.Tags!.First()));

		await Assert.That(ex.Message).Contains("First");
	}

	[Test]
	public async Task IgnoredMember_IsRejected()
	{
		ArgumentException ex = await ThrowsAsync<ArgumentException>(() => this.serializer.GetPath((Person p) => p.Secret));

		await Assert.That(ex.Message).Contains("Secret");
		await Assert.That(ex.ParamName).IsEqualTo("path");
	}

	[Test]
	public async Task ExtensionDataMember_IsRejected()
	{
		ArgumentException ex = await ThrowsAsync<ArgumentException>(() => this.serializer.GetPath((Person p) => p.Extras));

		await Assert.That(ex.Message).Contains("extension-data");
	}

	[Test]
	public async Task NonStringKeyedDictionary_IsRejected()
	{
		NotSupportedException ex = await ThrowsAsync<NotSupportedException>(() => this.serializer.GetPath((Person p) => p.ByNumber![7]));

		await Assert.That(ex.Message).Contains("key/value pairs");
	}

	[Test]
	public async Task IndexingANonCollection_IsRejected()
	{
		NotSupportedException ex = await ThrowsAsync<NotSupportedException>(() => this.serializer.GetPath((Person p) => p[0]));

		await Assert.That(ex.Message).Contains("cannot be indexed");
	}

	[Test]
	public async Task StringIndexing_IsRejected()
	{
		// A string's indexer is a method call, not an indexer over a serialized vector.
		NotSupportedException ex = await ThrowsAsync<NotSupportedException>(() => this.serializer.GetPath((Person p) => p.Name![0]));

		await Assert.That(ex.Message).Contains("get_Chars");
	}

	[Test]
	public async Task MemberOfAValueWithACustomConverter_IsRejected()
	{
		NotSupportedException ex = await ThrowsAsync<NotSupportedException>(() => this.serializer.GetPath((Person p) => p.Opaque!.City));

		await Assert.That(ex.Message).Contains(nameof(OpaqueAddressConverter));
	}

	[Test]
	public async Task MemberOfAPrimitive_IsRejected()
	{
		NotSupportedException ex = await ThrowsAsync<NotSupportedException>(() => this.serializer.GetPath((Person p) => p.Name!.Length));

		await Assert.That(ex.Message).Contains("Length");
	}

	[Test]
	public async Task NullPath_IsRejected()
	{
		Func<ShapeShiftPath> act = () => this.serializer.GetPath<Person, string?>(null!);

		await Assert.That(act).Throws<ArgumentNullException>();
	}

	[Test]
	public async Task GeneratedPath_DeserializesTheIntendedFragment()
	{
		JsonSerializer camel = this.serializer with { PropertyNamingPolicy = ShapeShiftNamingPolicy.CamelCase };
		Person person = new()
		{
			Name = "Ada",
			Nickname = "The Countess",
			HomeAddress = new("London", "E1"),
			Tags = ["mathematician", "programmer"],
			Scores = [10, 20],
			PreviousAddresses = [new("Ockham", "OX1"), new("Kirkby", "KM2")],
			Attributes = new(StringComparer.Ordinal) { ["hue"] = "green" },
			Location = new(51.5, -0.1),
		};
		string json = camel.Serialize(person);

		await Assert.That(camel.DeserializeFragment<string, ValueWitness>(json, camel.GetPath((Person p) => p.HomeAddress!.City))).IsEqualTo("London");
		await Assert.That(camel.DeserializeFragment<string, ValueWitness>(json, camel.GetPath((Person p) => p.Nickname))).IsEqualTo("The Countess");
		await Assert.That(camel.DeserializeFragment<string, ValueWitness>(json, camel.GetPath((Person p) => p.Tags![1]))).IsEqualTo("programmer");
		await Assert.That(camel.DeserializeFragment<string, ValueWitness>(json, camel.GetPath((Person p) => p.PreviousAddresses![1].Zip))).IsEqualTo("KM2");
		await Assert.That(camel.DeserializeFragment<string, ValueWitness>(json, camel.GetPath((Person p) => p.Attributes!["hue"]))).IsEqualTo("green");
		await Assert.That(camel.DeserializeFragment<int, ValueWitness>(json, camel.GetPath((Person p) => p.Scores[1]))).IsEqualTo(20);
		await Assert.That(camel.DeserializeFragment<double, ValueWitness>(json, camel.GetPath((Person p) => p.Location!.Value.Latitude))).IsEqualTo(51.5);
		await Assert.That(camel.DeserializeFragment<Address>(json, camel.GetPath((Person p) => p.HomeAddress))).IsEqualTo(new Address("London", "E1"));
	}

	[Test]
	public async Task GeneratedPath_ReportsAMissingFragment()
	{
		string json = this.serializer.Serialize(new Person { Name = "Ada" });

		await Assert.That(this.serializer.TryDeserializeFragment<string, ValueWitness>(json, this.serializer.GetPath((Person p) => p.HomeAddress!.City), out string? city)).IsFalse();
		await Assert.That(city).IsNull();
	}

	private static async Task<T> ThrowsAsync<T>(Func<ShapeShiftPath> act)
		where T : Exception
		=> await Assert.That(act).Throws<T>() ?? throw new InvalidOperationException($"Expected a {typeof(T).Name}.");

	private static ITypeShape<T> Shape<T>()
		where T : IShapeable<T> => T.GetTypeShape();

	[GenerateShape]
	internal partial record struct Coordinates(double Latitude, double Longitude);

	[GenerateShape]
	internal partial record Address(string City, string Zip);

	internal record DetailedAddress(string City, string Zip, string Country) : Address(City, Zip);

	[GenerateShape]
	internal partial class Person
	{
		public string? Name { get; set; }

		public Address? HomeAddress { get; set; }

		public string[]? Tags { get; set; }

		public ImmutableArray<int> Scores { get; set; } = ImmutableArray<int>.Empty;

		public List<Address>? PreviousAddresses { get; set; }

		public Dictionary<string, string>? Attributes { get; set; }

		public Dictionary<int, string>? ByNumber { get; set; }

		public int? Age { get; set; }

		public int Salary { get; set; }

		public Coordinates? Location { get; set; }

		[ShapeShiftConverter(typeof(OpaqueAddressConverter))]
		public Address? Opaque { get; set; }

		[PropertyShape(Name = "nick")]
		public string? Nickname { get; set; }

		[PropertyShape(Ignore = true)]
		public string? Secret { get; set; }

		[ShapeShiftExtensionData]
		public Dictionary<string, ShapeShiftValue> Extras { get; } = new(StringComparer.Ordinal);

		/// <summary>
		/// Gets a tag by index. An indexer is never a serialized member, so a path may not use one.
		/// </summary>
		/// <param name="index">The index of the tag.</param>
		/// <returns>The tag.</returns>
		public string this[int index] => this.Tags![index];
	}

	/// <summary>
	/// A converter that writes an address without describing the representation it produces.
	/// </summary>
	internal sealed class OpaqueAddressConverter : ShapeShiftConverter<Address, JsonEncoder, JsonDecoder>
	{
		public override Address? Read(ref JsonDecoder decoder, SerializationContext<JsonEncoder, JsonDecoder> context)
			=> decoder.ReadString() is string city ? new Address(city, string.Empty) : null;

		public override void Write(ref JsonEncoder encoder, in Address? value, SerializationContext<JsonEncoder, JsonDecoder> context)
		{
			if (value is null)
			{
				encoder.WriteNull();
			}
			else
			{
				encoder.WriteValue(value.City);
			}
		}
	}

	[GenerateShapeFor<Person>]
	private partial class PersonWitness;

	[GenerateShapeFor<string>]
	[GenerateShapeFor<int>]
	[GenerateShapeFor<double>]
	private partial class ValueWitness;
}
