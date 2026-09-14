// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Tests;

/// <summary>
/// Verifies the format-neutral path model: <see cref="ShapeShiftPath"/> and <see cref="ShapeShiftPathElement"/>.
/// </summary>
public class ShapeShiftPathTests
{
	[Test]
	public async Task Root_IsEmpty()
	{
		ShapeShiftPath path = ShapeShiftPath.Root;

		await Assert.That(path.Count).IsEqualTo(0);
		await Assert.That(path.ToString()).IsEqualTo("$");
		await Assert.That(path).IsEqualTo(default(ShapeShiftPath));
	}

	[Test]
	public async Task Constructor_AcceptsElements()
	{
		ShapeShiftPath path = new("items", 2, "name");

		await Assert.That(path.Count).IsEqualTo(3);
		await Assert.That(path[0].PropertyName).IsEqualTo("items");
		await Assert.That(path[1].Index).IsEqualTo(2);
		await Assert.That(path[2].PropertyName).IsEqualTo("name");
	}

	[Test]
	public async Task ToString_ProducesJsonPathLikeNotation()
	{
		ShapeShiftPath path = new("items", 2, "name");

		await Assert.That(path.ToString()).IsEqualTo("$.items[2].name");
	}

	[Test]
	public async Task ImplicitConversions_FromStringAndInt()
	{
		ShapeShiftPathElement fromString = "hello";
		ShapeShiftPathElement fromInt = 5;

		await Assert.That(fromString.IsPropertyName).IsTrue();
		await Assert.That(fromString.PropertyName).IsEqualTo("hello");
		await Assert.That(fromInt.IsPropertyName).IsFalse();
		await Assert.That(fromInt.Index).IsEqualTo(5);
	}

	[Test]
	public async Task PropertyName_ThrowsWhenElementIsVectorIndex()
	{
		ShapeShiftPathElement element = ShapeShiftPathElement.Vector(0);

		void Access() => _ = element.PropertyName;

		await Assert.That(Access).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task Index_ThrowsWhenElementIsPropertyName()
	{
		ShapeShiftPathElement element = ShapeShiftPathElement.Property("name");

		void Access() => _ = element.Index;

		await Assert.That(Access).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task Vector_RejectsNegativeIndex()
	{
		void Create() => ShapeShiftPathElement.Vector(-1);

		await Assert.That(Create).Throws<ArgumentException>();
	}

	[Test]
	public async Task Property_RejectsNull()
	{
		void Create() => ShapeShiftPathElement.Property(null!);

		await Assert.That(Create).Throws<ArgumentNullException>();
	}

	[Test]
	public async Task Equality_ComparesElementsAndOrder()
	{
		ShapeShiftPath a = new("items", 2);
		ShapeShiftPath b = new("items", 2);
		ShapeShiftPath c = new(2, "items");
		ShapeShiftPath d = new("items", 3);

		await Assert.That(a).IsEqualTo(b);
		await Assert.That(a.GetHashCode()).IsEqualTo(b.GetHashCode());
		await Assert.That(a == b).IsTrue();
		await Assert.That(a != c).IsTrue();
		await Assert.That(a != d).IsTrue();
	}

	[Test]
	public async Task PathElement_Equality()
	{
		ShapeShiftPathElement a = ShapeShiftPathElement.Property("name");
		ShapeShiftPathElement b = ShapeShiftPathElement.Property("name");
		ShapeShiftPathElement c = ShapeShiftPathElement.Property("other");
		ShapeShiftPathElement d = ShapeShiftPathElement.Vector(0);

		await Assert.That(a).IsEqualTo(b);
		await Assert.That(a == b).IsTrue();
		await Assert.That(a != c).IsTrue();
		await Assert.That(a.Equals((object)b)).IsTrue();
		await Assert.That(a.Equals((object)d)).IsFalse();
		await Assert.That(a.Equals("not a path element")).IsFalse();
	}

	[Test]
	public async Task PathElement_ToString()
	{
		await Assert.That(ShapeShiftPathElement.Property("name").ToString()).IsEqualTo("name");
		await Assert.That(ShapeShiftPathElement.Vector(3).ToString()).IsEqualTo("3");
	}

	[Test]
	public async Task Enumeration_YieldsElementsInOrder()
	{
		ShapeShiftPath path = new("a", 1, "b");
		List<ShapeShiftPathElement> elements = [.. path];

		await Assert.That(elements.Count).IsEqualTo(3);
		await Assert.That(elements[0].PropertyName).IsEqualTo("a");
		await Assert.That(elements[1].Index).IsEqualTo(1);
		await Assert.That(elements[2].PropertyName).IsEqualTo("b");
	}
}
