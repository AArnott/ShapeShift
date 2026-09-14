// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Numerics;
using System.Text;
using ShapeShift.Equality;

namespace ShapeShift.Tests;

/// <summary>
/// Verifies the structural equality comparers produced by <see cref="StructuralEqualityComparer"/>.
/// </summary>
public partial class StructuralEqualityTests
{
	private static readonly IEqualityComparer<CyclicNode> NodeComparer = StructuralEqualityComparer.Create<CyclicNode>();

	/// <summary>
	/// An enum used by the tests in this class.
	/// </summary>
	internal enum Color
	{
		/// <summary>The color red.</summary>
		Red,

		/// <summary>The color green.</summary>
		Green,
	}

	[Test]
	public async Task Primitives_EqualByValue()
	{
		IEqualityComparer<Primitives> comparer = StructuralEqualityComparer.Create<Primitives>();
		Primitives a = CreatePrimitives();
		Primitives b = CreatePrimitives();

		await Assert.That(ReferenceEquals(a, b)).IsFalse();
		await Assert.That(comparer.Equals(a, b)).IsTrue();
		await Assert.That(comparer.GetHashCode(a)).IsEqualTo(comparer.GetHashCode(b));
	}

	[Test]
	public async Task Primitives_DifferInOneMember()
	{
		IEqualityComparer<Primitives> comparer = StructuralEqualityComparer.Create<Primitives>();
		Primitives a = CreatePrimitives();
		Primitives b = CreatePrimitives() with { Int = 42 };

		await Assert.That(comparer.Equals(a, b)).IsFalse();
	}

	[Test]
	public async Task Nulls()
	{
		IEqualityComparer<Person> comparer = StructuralEqualityComparer.Create<Person>();

		await Assert.That(comparer.Equals(null, null)).IsTrue();
		await Assert.That(comparer.Equals(null, new Person("a", 1, []))).IsFalse();
		await Assert.That(comparer.Equals(new Person("a", 1, []), null)).IsFalse();
		await Assert.That(comparer.GetHashCode(null!)).IsEqualTo(0);
	}

	[Test]
	public async Task NullMembersAreDistinctFromEmpty()
	{
		IEqualityComparer<Person> comparer = StructuralEqualityComparer.Create<Person>();
		Person withNull = new("a", 1, null);
		Person withEmpty = new("a", 1, []);

		await Assert.That(comparer.Equals(withNull, withEmpty)).IsFalse();
		await Assert.That(comparer.Equals(withNull, new Person("a", 1, null))).IsTrue();
	}

	[Test]
	public async Task Collections_CompareElementwise()
	{
		IEqualityComparer<Person> comparer = StructuralEqualityComparer.Create<Person>();
		Person a = new("a", 1, ["x", "y"]);
		Person b = new("a", 1, ["x", "y"]);
		Person c = new("a", 1, ["y", "x"]);

		await Assert.That(comparer.Equals(a, b)).IsTrue();
		await Assert.That(comparer.GetHashCode(a)).IsEqualTo(comparer.GetHashCode(b));
		await Assert.That(comparer.Equals(a, c)).IsFalse();
	}

	[Test]
	public async Task Sets_IgnoreOrder()
	{
		IEqualityComparer<HashSet<string>> comparer = StructuralEqualityComparer.Create<HashSet<string>, Witness>();
		HashSet<string> a = ["x", "y", "z"];
		HashSet<string> b = ["z", "y", "x"];
		HashSet<string> c = ["z", "y"];

		await Assert.That(comparer.Equals(a, b)).IsTrue();
		await Assert.That(comparer.GetHashCode(a)).IsEqualTo(comparer.GetHashCode(b));
		await Assert.That(comparer.Equals(a, c)).IsFalse();
	}

	[Test]
	public async Task Dictionaries_IgnoreOrder()
	{
		IEqualityComparer<Dictionary<string, int>> comparer = StructuralEqualityComparer.Create<Dictionary<string, int>, Witness>();
		Dictionary<string, int> a = new() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
		Dictionary<string, int> b = new() { ["c"] = 3, ["a"] = 1, ["b"] = 2 };

		await Assert.That(comparer.Equals(a, b)).IsTrue();
		await Assert.That(comparer.GetHashCode(a)).IsEqualTo(comparer.GetHashCode(b));
	}

	[Test]
	public async Task Dictionaries_UseStructuralKeyEqualityRatherThanTheDictionaryComparer()
	{
		IEqualityComparer<Dictionary<string, int>> comparer = StructuralEqualityComparer.Create<Dictionary<string, int>, Witness>();
		Dictionary<string, int> insensitive = new(StringComparer.OrdinalIgnoreCase) { ["A"] = 1 };
		Dictionary<string, int> ordinal = new() { ["a"] = 1 };

		await Assert.That(comparer.Equals(insensitive, ordinal)).IsFalse();
		await Assert.That(comparer.Equals(insensitive, new Dictionary<string, int> { ["A"] = 1 })).IsTrue();
	}

	[Test]
	public async Task Dictionaries_WithNonStringKeys()
	{
		IEqualityComparer<Dictionary<Point, string>> comparer = StructuralEqualityComparer.Create<Dictionary<Point, string>, Witness>();
		Dictionary<Point, string> a = new() { [new(1, 2)] = "a", [new(3, 4)] = "b" };
		Dictionary<Point, string> b = new() { [new(3, 4)] = "b", [new(1, 2)] = "a" };
		Dictionary<Point, string> c = new() { [new(3, 4)] = "b", [new(1, 9)] = "a" };

		await Assert.That(comparer.Equals(a, b)).IsTrue();
		await Assert.That(comparer.GetHashCode(a)).IsEqualTo(comparer.GetHashCode(b));
		await Assert.That(comparer.Equals(a, c)).IsFalse();
	}

	[Test]
	public async Task Dictionaries_WithStructurallyEqualKeysThatAreDistinctToTheDictionary()
	{
		// The dictionary's own comparer treats these keys as distinct, but ours does not.
		IEqualityComparer<Dictionary<string[], int>> comparer = StructuralEqualityComparer.Create<Dictionary<string[], int>, Witness>();
		Dictionary<string[], int> a = new() { [["k"]] = 1, [["k"]] = 2 };
		Dictionary<string[], int> b = new() { [["k"]] = 2, [["k"]] = 1 };
		Dictionary<string[], int> c = new() { [["k"]] = 1, [["k"]] = 1 };

		await Assert.That(comparer.Equals(a, b)).IsTrue();
		await Assert.That(comparer.Equals(a, c)).IsFalse();
	}

	[Test]
	public async Task MultidimensionalArrays()
	{
		IEqualityComparer<int[,]> comparer = StructuralEqualityComparer.Create<int[,], Witness>();
		int[,] a = new int[2, 3];
		int[,] b = new int[2, 3];
		int[,] transposedShape = new int[3, 2];
		for (int i = 0; i < 6; i++)
		{
			a[i / 3, i % 3] = i;
			b[i / 3, i % 3] = i;
			transposedShape[i / 2, i % 2] = i;
		}

		await Assert.That(comparer.Equals(a, b)).IsTrue();
		await Assert.That(comparer.GetHashCode(a)).IsEqualTo(comparer.GetHashCode(b));

		// Same elements in the same (row major) order, but different dimensions.
		await Assert.That(comparer.Equals(a, transposedShape)).IsFalse();
	}

	[Test]
	public async Task Enums()
	{
		IEqualityComparer<Color> comparer = StructuralEqualityComparer.Create<Color, Witness>();

		await Assert.That(comparer.Equals(Color.Red, Color.Red)).IsTrue();
		await Assert.That(comparer.Equals(Color.Red, Color.Green)).IsFalse();
		await Assert.That(comparer.GetHashCode(Color.Red)).IsEqualTo(comparer.GetHashCode(Color.Red));
	}

	[Test]
	public async Task Optionals_CompareByPresenceAndValue()
	{
		IEqualityComparer<Optionals> comparer = StructuralEqualityComparer.Create<Optionals>();

		await Assert.That(comparer.Equals(new(null, null), new(null, null))).IsTrue();
		await Assert.That(comparer.Equals(new(1, null), new(1, null))).IsTrue();
		await Assert.That(comparer.Equals(new(1, null), new(null, null))).IsFalse();
		await Assert.That(comparer.Equals(new(null, new(1, 2)), new(null, new(1, 2)))).IsTrue();
		await Assert.That(comparer.GetHashCode(new(null, null))).IsEqualTo(comparer.GetHashCode(new(null, null)));
	}

	[Test]
	public async Task Unions()
	{
		IEqualityComparer<Shape> comparer = StructuralEqualityComparer.Create<Shape>();

		await Assert.That(comparer.Equals(new Circle(1), new Circle(1))).IsTrue();
		await Assert.That(comparer.GetHashCode(new Circle(1))).IsEqualTo(comparer.GetHashCode(new Circle(1)));
		await Assert.That(comparer.Equals(new Circle(1), new Circle(2))).IsFalse();
		await Assert.That(comparer.Equals(new Circle(1), new Square(1))).IsFalse();
	}

	[Test]
	public async Task Surrogates_CompareTheSurrogateState()
	{
		IEqualityComparer<SurrogateValue> comparer = StructuralEqualityComparer.Create<SurrogateValue>();

		await Assert.That(comparer.Equals(new(1, 2), new(1, 2))).IsTrue();
		await Assert.That(comparer.GetHashCode(new(1, 2))).IsEqualTo(comparer.GetHashCode(new(1, 2)));

		// Both have the same publicly visible Sum, but their surrogate state differs.
		await Assert.That(comparer.Equals(new(1, 2), new(3, 0))).IsFalse();
	}

	[Test]
	public async Task SelfLoop_IsEqualToItself()
	{
		CyclicNode a = new() { Name = "a" };
		a.Next = a;
		CyclicNode b = new() { Name = "a" };
		b.Next = b;

		await Assert.That(NodeComparer.Equals(a, b)).IsTrue();
		await Assert.That(NodeComparer.GetHashCode(a)).IsEqualTo(NodeComparer.GetHashCode(b));
	}

	[Test]
	public async Task SelfLoop_EqualsEquivalentTwoCycle()
	{
		// Equality is defined by the unfolding of the graph, not by its topology,
		// so a self loop and a two-node cycle with equal contents are equal.
		CyclicNode a = new() { Name = "a" };
		a.Next = a;

		CyclicNode b = new() { Name = "a" };
		CyclicNode c = new() { Name = "a" };
		b.Next = c;
		c.Next = b;

		await Assert.That(NodeComparer.Equals(a, b)).IsTrue();
		await Assert.That(NodeComparer.GetHashCode(a)).IsEqualTo(NodeComparer.GetHashCode(b));
	}

	[Test]
	public async Task Cycles_WithDifferingContentAreNotEqual()
	{
		CyclicNode a = new() { Name = "a" };
		a.Next = a;

		CyclicNode b = new() { Name = "a" };
		CyclicNode c = new() { Name = "z" };
		b.Next = c;
		c.Next = b;

		await Assert.That(NodeComparer.Equals(a, b)).IsFalse();
	}

	[Test]
	public async Task Cycle_IsNotEqualToAcyclicPrefix()
	{
		CyclicNode cyclic = new() { Name = "a" };
		cyclic.Next = cyclic;

		CyclicNode finite = new() { Name = "a", Next = new() { Name = "a" } };

		await Assert.That(NodeComparer.Equals(cyclic, finite)).IsFalse();
	}

	[Test]
	public async Task SharedReferences_EqualDuplicatedEquivalentObjects()
	{
		CyclicNode shared = new() { Name = "s" };
		Pair withSharing = new(shared, shared);
		Pair withoutSharing = new(new() { Name = "s" }, new() { Name = "s" });

		IEqualityComparer<Pair> comparer = StructuralEqualityComparer.Create<Pair>();
		await Assert.That(comparer.Equals(withSharing, withoutSharing)).IsTrue();
		await Assert.That(comparer.GetHashCode(withSharing)).IsEqualTo(comparer.GetHashCode(withoutSharing));
	}

	[Test]
	public async Task DeepGraph_DoesNotOverflow()
	{
		CyclicNode a = BuildChain(500);
		CyclicNode b = BuildChain(500);

		await Assert.That(NodeComparer.Equals(a, b)).IsTrue();
		await Assert.That(NodeComparer.GetHashCode(a)).IsEqualTo(NodeComparer.GetHashCode(b));
	}

	[Test]
	public async Task ShapeShiftValue_MapsIgnoreOrder()
	{
		IEqualityComparer<ShapeShiftValue> comparer = StructuralEqualityComparer.Create<ShapeShiftValue>();
		ShapeShiftValue a = new ShapeShiftMap(new Dictionary<string, ShapeShiftValue>
		{
			["a"] = new ShapeShiftInteger(1),
			["b"] = new ShapeShiftString("two"),
		});
		ShapeShiftValue b = new ShapeShiftMap(new Dictionary<string, ShapeShiftValue>
		{
			["b"] = new ShapeShiftString("two"),
			["a"] = new ShapeShiftInteger(1),
		});

		await Assert.That(comparer.Equals(a, b)).IsTrue();
		await Assert.That(comparer.GetHashCode(a)).IsEqualTo(comparer.GetHashCode(b));
	}

	[Test]
	public async Task ShapeShiftValue_ArraysAndBinaryCompareByContent()
	{
		IEqualityComparer<ShapeShiftValue> comparer = StructuralEqualityComparer.Create<ShapeShiftValue>();
		ShapeShiftValue a = new ShapeShiftArray([new ShapeShiftInteger(1), new ShapeShiftBinary(new byte[] { 1, 2, 3 })]);
		ShapeShiftValue b = new ShapeShiftArray([new ShapeShiftInteger(1), new ShapeShiftBinary(new byte[] { 1, 2, 3 })]);
		ShapeShiftValue c = new ShapeShiftArray([new ShapeShiftInteger(1), new ShapeShiftBinary(new byte[] { 1, 2, 4 })]);

		await Assert.That(comparer.Equals(a, b)).IsTrue();
		await Assert.That(comparer.GetHashCode(a)).IsEqualTo(comparer.GetHashCode(b));
		await Assert.That(comparer.Equals(a, c)).IsFalse();

		// The compiler generated record equality compares the payloads by reference.
		await Assert.That(a.Equals(b)).IsFalse();
	}

	[Test]
	public async Task ShapeShiftValue_NumbersAreComparedByNodeKind()
	{
		IEqualityComparer<ShapeShiftValue> comparer = StructuralEqualityComparer.Create<ShapeShiftValue>();

		await Assert.That(comparer.Equals(new ShapeShiftInteger(1), new ShapeShiftInteger(1))).IsTrue();
		await Assert.That(comparer.Equals(new ShapeShiftInteger(1), new ShapeShiftUnsignedInteger(1))).IsFalse();
		await Assert.That(comparer.Equals(new ShapeShiftInteger(1), new ShapeShiftFloat(1))).IsFalse();
		await Assert.That(comparer.Equals(ShapeShiftValue.Null, new ShapeShiftNull())).IsTrue();
	}

	[Test]
	public async Task ShapeShiftValue_NestedInAnObject()
	{
		IEqualityComparer<Dynamics> comparer = StructuralEqualityComparer.Create<Dynamics>();
		Dynamics a = new(new ShapeShiftArray([new ShapeShiftString("x")]));
		Dynamics b = new(new ShapeShiftArray([new ShapeShiftString("x")]));

		await Assert.That(comparer.Equals(a, b)).IsTrue();
		await Assert.That(comparer.GetHashCode(a)).IsEqualTo(comparer.GetHashCode(b));
	}

	[Test]
	public async Task CustomComparerOverride()
	{
		IEqualityComparer<Person> comparer = StructuralEqualityComparerProvider.Default
			.WithComparer(StringComparer.OrdinalIgnoreCase)
			.GetComparer<Person>();

		Person a = new("bob", 1, ["tag"]);
		Person b = new("BOB", 1, ["TAG"]);

		await Assert.That(comparer.Equals(a, b)).IsTrue();
		await Assert.That(comparer.GetHashCode(a)).IsEqualTo(comparer.GetHashCode(b));
		await Assert.That(StructuralEqualityComparer.Create<Person>().Equals(a, b)).IsFalse();
	}

	[Test]
	public async Task CustomComparerOverride_DoesNotLeakToOtherProviders()
	{
		StructuralEqualityComparerProvider customized = StructuralEqualityComparerProvider.Default
			.WithComparer(StringComparer.OrdinalIgnoreCase);

		await Assert.That(customized.GetComparer<Person>().Equals(new("a", 1, []), new("A", 1, []))).IsTrue();
		await Assert.That(StructuralEqualityComparerProvider.Default.GetComparer<Person>().Equals(new("a", 1, []), new("A", 1, []))).IsFalse();
	}

	[Test]
	public async Task ComparersAreCachedPerProvider()
	{
		StructuralEqualityComparerProvider provider = new();
		ITypeShape<Person> shape = ShapeOf<Person>();

		await Assert.That(ReferenceEquals(provider.GetComparer(shape), provider.GetComparer(shape))).IsTrue();
		await Assert.That(ReferenceEquals(provider.GetComparer<Person>(), provider.GetComparer<Person>())).IsTrue();
	}

	[Test]
	public async Task CollisionResistant_AgreesOnEquality()
	{
		IEqualityComparer<Person> comparer = StructuralEqualityComparer.CreateCollisionResistant<Person>();
		Person a = new("a", 1, ["x"]);
		Person b = new("a", 1, ["x"]);

		await Assert.That(comparer.Equals(a, b)).IsTrue();
		await Assert.That(comparer.GetHashCode(a)).IsEqualTo(comparer.GetHashCode(b));
		await Assert.That(comparer.Equals(a, new Person("a", 2, ["x"]))).IsFalse();
	}

	[Test]
	public async Task CollisionResistant_IsStableWithinTheProcess()
	{
		IEqualityComparer<Person> first = StructuralEqualityComparer.CreateCollisionResistant<Person>();
		StructuralEqualityComparerProvider otherProvider = new() { UseCollisionResistantHashing = true };
		Person value = new("a", 1, ["x"]);

		await Assert.That(first.GetHashCode(value)).IsEqualTo(otherProvider.GetComparer<Person>().GetHashCode(value));
	}

	[Test]
	public async Task CollisionResistant_DiffersFromDeterministic()
	{
		IEqualityComparer<Person> deterministic = StructuralEqualityComparer.Create<Person>();
		IEqualityComparer<Person> resistant = StructuralEqualityComparer.CreateCollisionResistant<Person>();

		// A single value could coincidentally collide; several cannot.
		bool anyDifference = false;
		for (int i = 0; i < 8; i++)
		{
			Person value = new($"name{i}", i, [$"tag{i}"]);
			anyDifference |= deterministic.GetHashCode(value) != resistant.GetHashCode(value);
		}

		await Assert.That(anyDifference).IsTrue();
	}

	[Test]
	public async Task Deterministic_HashIsStableAcrossComparerInstances()
	{
		Person value = new("a", 1, ["x"]);
		int first = StructuralEqualityComparerProvider.Default.GetComparer<Person>().GetHashCode(value);
		int second = new StructuralEqualityComparerProvider().GetComparer<Person>().GetHashCode(value);

		await Assert.That(first).IsEqualTo(second);
	}

	[Test]
	public async Task ComparerWorksAsADictionaryKeyComparer()
	{
		Dictionary<Person, int> map = new(StructuralEqualityComparer.Create<Person>());
		map[new("a", 1, ["x"])] = 1;
		map[new("a", 1, ["x"])] = 2;

		await Assert.That(map.Count).IsEqualTo(1);
		await Assert.That(map[new("a", 1, ["x"])]).IsEqualTo(2);
	}

	[Test]
	public async Task RecursiveTypes()
	{
		IEqualityComparer<Tree> comparer = StructuralEqualityComparer.Create<Tree>();
		Tree a = new("root", [new("a", []), new("b", [new("c", [])])]);
		Tree b = new("root", [new("a", []), new("b", [new("c", [])])]);
		Tree c = new("root", [new("a", []), new("b", [new("d", [])])]);

		await Assert.That(comparer.Equals(a, b)).IsTrue();
		await Assert.That(comparer.GetHashCode(a)).IsEqualTo(comparer.GetHashCode(b));
		await Assert.That(comparer.Equals(a, c)).IsFalse();
	}

	[Test]
	public async Task ByteArraysCompareByContent()
	{
		IEqualityComparer<byte[]> comparer = StructuralEqualityComparer.Create<byte[], Witness>();

		await Assert.That(comparer.Equals([1, 2, 3], [1, 2, 3])).IsTrue();
		await Assert.That(comparer.GetHashCode([1, 2, 3])).IsEqualTo(comparer.GetHashCode([1, 2, 3]));
		await Assert.That(comparer.Equals([1, 2, 3], [1, 2, 4])).IsFalse();
	}

	[Test]
	public async Task DoubleNaNAndNegativeZero()
	{
		IEqualityComparer<Doubles> comparer = StructuralEqualityComparer.Create<Doubles>();

		await Assert.That(comparer.Equals(new(double.NaN), new(double.NaN))).IsTrue();
		await Assert.That(comparer.GetHashCode(new(double.NaN))).IsEqualTo(comparer.GetHashCode(new(double.NaN)));
		await Assert.That(comparer.Equals(new(0.0), new(-0.0))).IsTrue();
		await Assert.That(comparer.GetHashCode(new(0.0))).IsEqualTo(comparer.GetHashCode(new(-0.0)));
	}

	[Test]
	public async Task DoubleNaNAndNegativeZero_CollisionResistant()
	{
		IEqualityComparer<Doubles> comparer = StructuralEqualityComparer.CreateCollisionResistant<Doubles>();

		await Assert.That(comparer.Equals(new(double.NaN), new(double.NaN))).IsTrue();
		await Assert.That(comparer.GetHashCode(new(double.NaN))).IsEqualTo(comparer.GetHashCode(new(double.NaN)));
		await Assert.That(comparer.GetHashCode(new(0.0))).IsEqualTo(comparer.GetHashCode(new(-0.0)));
	}

	private static ITypeShape<T> ShapeOf<T>()
		where T : IShapeable<T> => T.GetTypeShape();

	private static CyclicNode BuildChain(int length)
	{
		CyclicNode head = new() { Name = "0" };
		CyclicNode current = head;
		for (int i = 1; i < length; i++)
		{
			current.Next = new() { Name = i.ToString(CultureInfo.InvariantCulture) };
			current = current.Next;
		}

		return head;
	}

	private static Primitives CreatePrimitives() => new(
		true,
		'c',
		new Rune('x'),
		"text",
		-1,
		2,
		-3,
		4,
		-5,
		6,
		-7,
		8,
		new BigInteger(9),
		(Half)10,
		11.5f,
		12.5,
		13.5m,
		new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc),
		new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.Zero),
		TimeSpan.FromMinutes(3),
		Guid.Parse("11112222-3333-4444-5555-666677778888"),
		Color.Green);

	[GenerateShape]
	internal partial record struct Point(int X, int Y);

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
		BigInteger Unbounded,
		Half Tiny16,
		float Float,
		double Double,
		decimal Money,
		DateTime When,
		DateTimeOffset WhenOffset,
		TimeSpan HowLong,
		Guid Id,
		Color Hue);

	[GenerateShape]
	internal partial record Person(string Name, int Age, string[]? Tags);

	[GenerateShape]
	internal partial record Doubles(double Value);

	[GenerateShape]
	internal partial record Optionals(int? Number, Point? Location);

	[GenerateShape]
	internal partial record Tree(string Name, List<Tree> Children);

	[GenerateShape]
	internal partial record Dynamics(ShapeShiftValue Value);

	[GenerateShape]
	internal partial class CyclicNode
	{
		public string Name { get; set; } = string.Empty;

		public CyclicNode? Next { get; set; }
	}

	[GenerateShape]
	internal partial record Pair(CyclicNode First, CyclicNode Second);

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

	[GenerateShapeFor<byte[]>]
	[GenerateShapeFor<int[,]>]
	[GenerateShapeFor<Color>]
	[GenerateShapeFor<HashSet<string>>]
	[GenerateShapeFor<Dictionary<string, int>>]
	[GenerateShapeFor<Dictionary<string[], int>>]
	[GenerateShapeFor<Dictionary<Point, string>>]
	internal partial class Witness;
}
