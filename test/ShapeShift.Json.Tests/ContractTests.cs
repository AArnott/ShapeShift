// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Numerics;
using System.Text;
using ShapeShift.Schema;
using ShapeShift.Tests;

namespace ShapeShift.Json.Tests;

/// <summary>
/// Tests for the format-neutral contract description produced by
/// <see cref="ShapeShiftSerializer{TEncoder, TDecoder}.GetContract(ITypeShape)"/>.
/// </summary>
/// <remarks>
/// Contract generation lives entirely in the format-neutral core library, but it requires a
/// concrete serializer to run. <see cref="JsonSerializer"/> is used as that host.
/// </remarks>
public partial class ContractTests : TestBase
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
	public async Task Object_DescribesProperties()
	{
		ObjectContract contract = this.Contract<Person>();

		await Assert.That(contract.Kind).IsEqualTo(DataContractKind.Object);
		await Assert.That(contract.DataType).IsEqualTo(typeof(Person));
		await Assert.That(contract.HasExtensionData).IsFalse();
		await Assert.That(string.Join(",", contract.Properties.Select(p => p.Name))).IsEqualTo("Name,Age,Nickname");

		PropertyContract name = Property(contract, "Name");
		await Assert.That(name.IsRequired).IsTrue();
		await Assert.That(name.IsNullable).IsFalse();
		await Assert.That(name.IsReadable).IsTrue();
		await Assert.That(name.IsWritable).IsTrue();
		await Assert.That(((PrimitiveContract)name.Type).PrimitiveType).IsEqualTo(PrimitiveDataType.String);

		PropertyContract nickname = Property(contract, "Nickname");
		await Assert.That(nickname.IsRequired).IsFalse();
		await Assert.That(nickname.IsNullable).IsTrue();
		await Assert.That(nickname.DefaultValue).IsEqualTo(ShapeShiftValue.Null);
	}

	[Test]
	public async Task Object_ReadOnlyPropertyIsNotWritable()
	{
		ObjectContract contract = this.Contract<WithComputed>();

		await Assert.That(Property(contract, "Doubled").IsWritable).IsFalse();
		await Assert.That(Property(contract, "Doubled").IsReadable).IsTrue();
		await Assert.That(Property(contract, "Value").IsWritable).IsTrue();
	}

	[Test]
	public async Task Object_NamingPolicyIsApplied()
	{
		JsonSerializer camel = this.serializer with { PropertyNamingPolicy = ShapeShiftNamingPolicy.CamelCase };
		ObjectContract contract = (ObjectContract)camel.GetContract<Person>();

		await Assert.That(string.Join(",", contract.Properties.Select(p => p.Name))).IsEqualTo("name,age,nickname");
		await Assert.That(string.Join(",", contract.Properties.Select(p => p.DeclaredName))).IsEqualTo("Name,Age,Nickname");
	}

	[Test]
	public async Task Object_ExplicitNameWinsOverNamingPolicy()
	{
		JsonSerializer camel = this.serializer with { PropertyNamingPolicy = ShapeShiftNamingPolicy.CamelCase };
		ObjectContract contract = (ObjectContract)camel.GetContract<Renamed>();

		await Assert.That(contract.Properties.Single().Name).IsEqualTo("EXPLICIT");
		await Assert.That(contract.Properties.Single().DeclaredName).IsEqualTo("EXPLICIT");
		await Assert.That(contract.Properties.Single().MemberName).IsEqualTo("Value");
	}

	[Test]
	public async Task Object_MemberNameIsTheClrName()
	{
		JsonSerializer camel = this.serializer with { PropertyNamingPolicy = ShapeShiftNamingPolicy.CamelCase };
		ObjectContract contract = (ObjectContract)camel.GetContract<Person>();

		await Assert.That(string.Join(",", contract.Properties.Select(p => p.MemberName))).IsEqualTo("Name,Age,Nickname");
	}

	[Test]
	public async Task GetMemberName_ReadsThroughAnAlias()
	{
		var shape = (PolyType.Abstractions.IObjectTypeShape)TypeShapeOf<Renamed>();
		PolyType.Abstractions.IPropertyShape property = shape.Properties.Single();

		await Assert.That(property.Name).IsEqualTo("EXPLICIT");
		await Assert.That(PropertyContract.GetMemberName(property)).IsEqualTo("Value");
	}

	[Test]
	public async Task GetMemberName_RejectsNull()
	{
		Func<string?> act = () => PropertyContract.GetMemberName(null!);

		await Assert.That(act).Throws<ArgumentNullException>();
	}

	[Test]
	public async Task Object_ExtensionDataIsReported()
	{
		ObjectContract contract = this.Contract<Extensible>();

		await Assert.That(contract.HasExtensionData).IsTrue();
		await Assert.That(string.Join(",", contract.Properties.Select(p => p.Name))).IsEqualTo("Known");
	}

	[Test]
	public async Task Object_AllowMissingValuesRelaxesRequired()
	{
		JsonSerializer relaxed = this.serializer with { DeserializeDefaultValues = DeserializeDefaultValuesPolicy.AllowMissingValuesForRequiredProperties };
		ObjectContract contract = (ObjectContract)relaxed.GetContract<Person>();

		await Assert.That(contract.Properties.All(p => !p.IsRequired)).IsTrue();
	}

	[Test]
	public async Task Object_AllowNullValuesRelaxesNullability()
	{
		JsonSerializer relaxed = this.serializer with { DeserializeDefaultValues = DeserializeDefaultValuesPolicy.AllowNullValuesForNonNullableProperties };
		ObjectContract contract = (ObjectContract)relaxed.GetContract<Person>();

		await Assert.That(Property(contract, "Name").IsNullable).IsTrue();
	}

	[Test]
	public async Task Object_SerializeDefaultValuesPolicyDrivesAlwaysWritten()
	{
		ObjectContract defaults = this.Contract<WithComputed>();
		await Assert.That(Property(defaults, "Value").IsAlwaysWritten).IsTrue();

		JsonSerializer never = this.serializer with { SerializeDefaultValues = SerializeDefaultValuesPolicy.Never };
		ObjectContract neverContract = (ObjectContract)never.GetContract<WithComputed>();
		await Assert.That(Property(neverContract, "Value").IsAlwaysWritten).IsFalse();

		JsonSerializer valueTypes = this.serializer with { SerializeDefaultValues = SerializeDefaultValuesPolicy.ValueTypes };
		ObjectContract valueTypesContract = (ObjectContract)valueTypes.GetContract<WithComputed>();
		await Assert.That(Property(valueTypesContract, "Value").IsAlwaysWritten).IsTrue();

		JsonSerializer referenceTypes = this.serializer with { SerializeDefaultValues = SerializeDefaultValuesPolicy.ReferenceTypes };
		ObjectContract referenceTypesContract = (ObjectContract)referenceTypes.GetContract<WithComputed>();
		await Assert.That(Property(referenceTypesContract, "Value").IsAlwaysWritten).IsFalse();
	}

	[Test]
	public async Task Sequences_AreDescribed()
	{
		ObjectContract contract = this.Contract<Bag>();

		SequenceContract items = (SequenceContract)Property(contract, "Items").Type;
		await Assert.That(items.Kind).IsEqualTo(DataContractKind.Sequence);
		await Assert.That(items.IsSet).IsFalse();
		await Assert.That(((PrimitiveContract)items.ElementType).PrimitiveType).IsEqualTo(PrimitiveDataType.Int32);

		SequenceContract tags = (SequenceContract)Property(contract, "Tags").Type;
		await Assert.That(tags.IsSet).IsTrue();

		SequenceContract immutable = (SequenceContract)Property(contract, "Immutable").Type;
		await Assert.That(((PrimitiveContract)immutable.ElementType).PrimitiveType).IsEqualTo(PrimitiveDataType.String);
	}

	[Test]
	public async Task Dictionaries_DistinguishKeyEncoding()
	{
		ObjectContract contract = this.Contract<Bag>();

		MapContract words = (MapContract)Property(contract, "Words").Type;
		await Assert.That(words.Kind).IsEqualTo(DataContractKind.Map);
		await Assert.That(words.Encoding).IsEqualTo(MapEncoding.StringKeyedMap);
		await Assert.That(((PrimitiveContract)words.KeyType).PrimitiveType).IsEqualTo(PrimitiveDataType.String);

		MapContract numbers = (MapContract)Property(contract, "Numbers").Type;
		await Assert.That(numbers.Encoding).IsEqualTo(MapEncoding.KeyValuePairSequence);
		await Assert.That(((PrimitiveContract)numbers.KeyType).PrimitiveType).IsEqualTo(PrimitiveDataType.Int32);
	}

	[Test]
	public async Task RectangularArray_ReportsRank()
	{
		ObjectContract contract = this.Contract<Bag>();
		RectangularArrayContract grid = (RectangularArrayContract)Property(contract, "Grid").Type;

		await Assert.That(grid.Kind).IsEqualTo(DataContractKind.RectangularArray);
		await Assert.That(grid.Rank).IsEqualTo(2);
		await Assert.That(((PrimitiveContract)grid.ElementType).PrimitiveType).IsEqualTo(PrimitiveDataType.Int32);
	}

	[Test]
	public async Task Binary_UsesFormatSpecificConverter()
	{
		ObjectContract contract = this.Contract<Bag>();
		PrimitiveContract blob = (PrimitiveContract)Property(contract, "Blob").Type;

		await Assert.That(blob.PrimitiveType).IsEqualTo(PrimitiveDataType.Binary);
	}

	[Test]
	public async Task Enum_DescribesMembers()
	{
		ObjectContract contract = this.Contract<Enums>();

		EnumContract color = (EnumContract)Property(contract, "Color").Type;
		await Assert.That(color.Kind).IsEqualTo(DataContractKind.Enum);
		await Assert.That(color.IsSerializedByName).IsTrue();
		await Assert.That(color.IsFlags).IsFalse();
		await Assert.That(string.Join(",", color.Members.Select(m => m.Name))).IsEqualTo("Red,Green");
		await Assert.That(color.Members.Single(m => m.Name == "Green").Value).IsEqualTo((ShapeShiftValue)5L);
		await Assert.That(((PrimitiveContract)color.UnderlyingType).PrimitiveType).IsEqualTo(PrimitiveDataType.Int32);

		EnumContract access = (EnumContract)Property(contract, "Access").Type;
		await Assert.That(access.IsFlags).IsTrue();
	}

	[Test]
	public async Task Enum_HonorsSerializeEnumValuesByName()
	{
		JsonSerializer numeric = this.serializer with { SerializeEnumValuesByName = false };
		ObjectContract contract = (ObjectContract)numeric.GetContract<Enums>();

		await Assert.That(((EnumContract)Property(contract, "Color").Type).IsSerializedByName).IsFalse();
	}

	[Test]
	public async Task Optional_IsDescribed()
	{
		ObjectContract contract = this.Contract<Enums>();
		OptionalContract optional = (OptionalContract)Property(contract, "MaybeColor").Type;

		await Assert.That(optional.Kind).IsEqualTo(DataContractKind.Optional);
		await Assert.That(optional.ElementType.Kind).IsEqualTo(DataContractKind.Enum);
	}

	[Test]
	public async Task Surrogate_IsDescribed()
	{
		SurrogateContract contract = (SurrogateContract)this.serializer.GetContract<SurrogateValue>();

		await Assert.That(contract.Kind).IsEqualTo(DataContractKind.Surrogate);
		await Assert.That(contract.DataType).IsEqualTo(typeof(SurrogateValue));
		await Assert.That(contract.SurrogateType.Kind).IsEqualTo(DataContractKind.Optional);
	}

	[Test]
	public async Task Union_DescribesCases()
	{
		UnionContract contract = (UnionContract)this.serializer.GetContract<Shape>();

		await Assert.That(contract.Kind).IsEqualTo(DataContractKind.Union);
		await Assert.That(contract.BaseType.Kind).IsEqualTo(DataContractKind.Object);
		await Assert.That(contract.Cases.Length).IsEqualTo(2);

		UnionCaseContract circle = contract.Cases.Single(c => c.Name == "circle");
		await Assert.That(circle.IsTagSpecified).IsFalse();
		await Assert.That(circle.Type.DataType).IsEqualTo(typeof(Circle));

		UnionCaseContract square = contract.Cases.Single(c => c.Tag == 3);
		await Assert.That(square.IsTagSpecified).IsTrue();
		await Assert.That(square.Type.DataType).IsEqualTo(typeof(Square));
	}

	[Test]
	public async Task Dynamic_IsDescribed()
	{
		ObjectContract contract = this.Contract<Dynamics>();

		await Assert.That(Property(contract, "Value").Type.Kind).IsEqualTo(DataContractKind.Dynamic);
		await Assert.That(Property(contract, "Element").Type.Kind).IsEqualTo(DataContractKind.Dynamic);
	}

	[Test]
	public async Task Primitives_AreMapped()
	{
		ObjectContract contract = this.Contract<Primitives>();

		await Assert.That(PrimitiveOf(contract, "Flag")).IsEqualTo(PrimitiveDataType.Boolean);
		await Assert.That(PrimitiveOf(contract, "Letter")).IsEqualTo(PrimitiveDataType.Char);
		await Assert.That(PrimitiveOf(contract, "Symbol")).IsEqualTo(PrimitiveDataType.Rune);
		await Assert.That(PrimitiveOf(contract, "Text")).IsEqualTo(PrimitiveDataType.String);
		await Assert.That(PrimitiveOf(contract, "Tiny")).IsEqualTo(PrimitiveDataType.SByte);
		await Assert.That(PrimitiveOf(contract, "Small")).IsEqualTo(PrimitiveDataType.Byte);
		await Assert.That(PrimitiveOf(contract, "Short")).IsEqualTo(PrimitiveDataType.Int16);
		await Assert.That(PrimitiveOf(contract, "UShort")).IsEqualTo(PrimitiveDataType.UInt16);
		await Assert.That(PrimitiveOf(contract, "Int")).IsEqualTo(PrimitiveDataType.Int32);
		await Assert.That(PrimitiveOf(contract, "UInt")).IsEqualTo(PrimitiveDataType.UInt32);
		await Assert.That(PrimitiveOf(contract, "Long")).IsEqualTo(PrimitiveDataType.Int64);
		await Assert.That(PrimitiveOf(contract, "ULong")).IsEqualTo(PrimitiveDataType.UInt64);
		await Assert.That(PrimitiveOf(contract, "Huge")).IsEqualTo(PrimitiveDataType.Int128);
		await Assert.That(PrimitiveOf(contract, "UHuge")).IsEqualTo(PrimitiveDataType.UInt128);
		await Assert.That(PrimitiveOf(contract, "Unbounded")).IsEqualTo(PrimitiveDataType.BigInteger);
		await Assert.That(PrimitiveOf(contract, "Tiny16")).IsEqualTo(PrimitiveDataType.Half);
		await Assert.That(PrimitiveOf(contract, "Float")).IsEqualTo(PrimitiveDataType.Single);
		await Assert.That(PrimitiveOf(contract, "Double")).IsEqualTo(PrimitiveDataType.Double);
		await Assert.That(PrimitiveOf(contract, "Money")).IsEqualTo(PrimitiveDataType.Decimal);
		await Assert.That(PrimitiveOf(contract, "When")).IsEqualTo(PrimitiveDataType.DateTime);
		await Assert.That(PrimitiveOf(contract, "WhenOffset")).IsEqualTo(PrimitiveDataType.DateTimeOffset);
		await Assert.That(PrimitiveOf(contract, "HowLong")).IsEqualTo(PrimitiveDataType.TimeSpan);
	}

	[Test]
	public async Task Recursion_ReusesTheSameContractInstance()
	{
		ObjectContract contract = this.Contract<Node>();
		SequenceContract children = (SequenceContract)Property(contract, "Children").Type;

		await Assert.That(children.ElementType).IsSameReferenceAs(contract);
		await Assert.That(contract.ReferencedContracts.Contains(children)).IsTrue();
	}

	[Test]
	public async Task Contracts_AreCached()
	{
		await Assert.That(this.serializer.GetContract<Person>()).IsSameReferenceAs(this.serializer.GetContract<Person>());
	}

	[Test]
	public async Task ConverterWithoutHook_YieldsUndocumentedContract()
	{
		ObjectContract contract = this.Contract<Opaque>();
		UndocumentedContract undocumented = (UndocumentedContract)Property(contract, "Value").Type;

		await Assert.That(undocumented.Kind).IsEqualTo(DataContractKind.Undocumented);
		await Assert.That(undocumented.ConverterType).IsEqualTo(typeof(OpaqueConverter));
		await Assert.That(undocumented.Reason).Contains("does not describe");
	}

	[Test]
	public async Task ConverterWithHook_ContributesItsOwnContract()
	{
		ObjectContract contract = this.Contract<Described>();
		PrimitiveContract described = (PrimitiveContract)Property(contract, "Value").Type;

		await Assert.That(described.PrimitiveType).IsEqualTo(PrimitiveDataType.String);
	}

	[Test]
	public async Task ConverterHook_CanDescribeCompositeShapes()
	{
		ObjectContract contract = this.Contract<Composed>();
		SequenceContract sequence = (SequenceContract)Property(contract, "Value").Type;

		await Assert.That(((PrimitiveContract)sequence.ElementType).PrimitiveType).IsEqualTo(PrimitiveDataType.Int32);
	}

	[Test]
	public async Task PreserveReferences_IsRejected()
	{
		// JSON has no back-reference token, so JsonSerializer does not implement
		// IReferencePreservingSerializer and rejects the request at configuration time.
		Func<JsonSerializer> act = () => this.serializer with { PreserveReferences = ReferencePreservationMode.RejectCycles };

		await Assert.That(act).Throws<NotSupportedException>();
	}

	[Test]
	public async Task ToString_IsInformative()
	{
		await Assert.That(this.serializer.GetContract<Person>().ToString()).Contains("Object");
		await Assert.That(this.serializer.GetContract<Person>().ToString()).Contains("Person");
	}

	private static PropertyContract Property(ObjectContract contract, string name)
		=> contract.Properties.Single(p => p.Name == name);

	private static PrimitiveDataType PrimitiveOf(ObjectContract contract, string name)
		=> ((PrimitiveContract)Property(contract, name).Type).PrimitiveType;

	private static ITypeShape<T> TypeShapeOf<T>()
		where T : IShapeable<T> => T.GetTypeShape();

	private ObjectContract Contract<T>()
		where T : IShapeable<T> => (ObjectContract)this.serializer.GetContract<T>();

	[GenerateShape]
	internal partial record Person(string Name, int Age, string? Nickname = null);

	[GenerateShape]
	internal partial class WithComputed
	{
		public int Value { get; set; }

		public int Doubled => this.Value * 2;
	}

	[GenerateShape]
	internal partial class Renamed
	{
		[PropertyShape(Name = "EXPLICIT")]
		public string? Value { get; set; }
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
		ImmutableArray<string> Immutable,
		Dictionary<string, int> Words,
		Dictionary<int, string> Numbers,
		int[,] Grid,
		byte[] Blob);

	[GenerateShape]
	internal partial record Enums(Color Color, Access Access, Color? MaybeColor);

	[GenerateShape]
	internal partial record Dynamics(ShapeShiftValue Value, System.Text.Json.JsonElement Element);

	[GenerateShape]
	internal partial record Primitives(
		bool Flag,
		char Letter,
		Rune Symbol,
		string Text,
		sbyte Tiny,
		byte Small,
		short Short,
		ushort UShort,
		int Int,
		uint UInt,
		long Long,
		ulong ULong,
		Int128 Huge,
		UInt128 UHuge,
		BigInteger Unbounded,
		Half Tiny16,
		float Float,
		double Double,
		decimal Money,
		DateTime When,
		DateTimeOffset WhenOffset,
		TimeSpan HowLong);

	[GenerateShape]
	internal partial record Node(string Name, List<Node> Children);

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
	[DerivedTypeShape(typeof(Circle), Name = "circle")]
	[DerivedTypeShape(typeof(Square), Tag = 3)]
	internal partial record Shape;

	internal sealed record Circle(double Radius) : Shape;

	internal sealed record Square(double Side) : Shape;

	[GenerateShape]
	internal partial record Opaque([property: ShapeShiftConverter(typeof(OpaqueConverter))] string Value);

	[GenerateShape]
	internal partial record Described([property: ShapeShiftConverter(typeof(DescribedConverter))] string Value);

	[GenerateShape]
	internal partial record Composed([property: ShapeShiftConverter(typeof(ComposedConverter))] string Value);

	internal sealed class OpaqueConverter : ShapeShiftConverter<string, JsonEncoder, JsonDecoder>
	{
		public override string? Read(ref JsonDecoder decoder, SerializationContext<JsonEncoder, JsonDecoder> context) => decoder.ReadString();

		public override void Write(ref JsonEncoder encoder, in string? value, SerializationContext<JsonEncoder, JsonDecoder> context) => encoder.WriteValue(value!);
	}

	internal sealed class DescribedConverter : ShapeShiftConverter<string, JsonEncoder, JsonDecoder>
	{
		public override string? Read(ref JsonDecoder decoder, SerializationContext<JsonEncoder, JsonDecoder> context) => decoder.ReadString();

		public override void Write(ref JsonEncoder encoder, in string? value, SerializationContext<JsonEncoder, JsonDecoder> context) => encoder.WriteValue(value!);

		public override DataContract? GetContract(ContractContext<JsonEncoder, JsonDecoder> context)
			=> new PrimitiveContract(typeof(string), PrimitiveDataType.String);
	}

	internal sealed class ComposedConverter : ShapeShiftConverter<string, JsonEncoder, JsonDecoder>
	{
		public override string? Read(ref JsonDecoder decoder, SerializationContext<JsonEncoder, JsonDecoder> context) => decoder.ReadString();

		public override void Write(ref JsonEncoder encoder, in string? value, SerializationContext<JsonEncoder, JsonDecoder> context) => encoder.WriteValue(value!);

		public override DataContract? GetContract(ContractContext<JsonEncoder, JsonDecoder> context)
			=> new SequenceContract(typeof(string), new PrimitiveContract(typeof(int), PrimitiveDataType.Int32));
	}
}
