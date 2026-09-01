// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ShapeShift.Tests;

namespace ShapeShift.MsgPack.Tests;

/// <summary>
/// Verifies that reference preservation round-trips object identity and cycles through the reserved MessagePack
/// reference extension, and that payloads which misuse it are rejected.
/// </summary>
public partial class MsgPackReferencePreservationTests : TestBase
{
	private readonly MsgPackSerializer plain = new();

	[Test]
	public async Task Off_WritesSharedObjectsTwice()
	{
		Node shared = new() { Name = "shared" };
		Pair value = new() { First = shared, Second = shared };

		Pair? actual = this.plain.Deserialize<Pair>(this.plain.Serialize(value));

		await Assert.That(actual!.First!.Name).IsEqualTo("shared");
		await Assert.That(ReferenceEquals(actual.First, actual.Second)).IsFalse();
	}

	[Test]
	public async Task RejectCycles_PreservesIdentity()
	{
		MsgPackSerializer serializer = this.plain with { PreserveReferences = ReferencePreservationMode.RejectCycles };
		Node shared = new() { Name = "shared" };
		Pair value = new() { First = shared, Second = shared };

		byte[] encoded = serializer.Serialize(value);
		Pair? actual = serializer.Deserialize<Pair>(encoded);

		await Assert.That(ReferenceEquals(actual!.First, actual.Second)).IsTrue();
		await Assert.That(actual.First!.Name).IsEqualTo("shared");
		await Assert.That(encoded.Length).IsLessThan(this.plain.Serialize(value).Length);
	}

	[Test]
	public async Task RejectCycles_RejectsACycle()
	{
		MsgPackSerializer serializer = this.plain with { PreserveReferences = ReferencePreservationMode.RejectCycles };
		Node node = new() { Name = "a" };
		node.Next = node;

		Func<byte[]> serialize = () => serializer.Serialize(node);

		await Assert.That(serialize).Throws<ShapeShiftSerializationException>();
	}

	[Test]
	public async Task AllowCycles_RoundTripsASelfReference()
	{
		MsgPackSerializer serializer = this.plain with { PreserveReferences = ReferencePreservationMode.AllowCycles };
		Node node = new() { Name = "a" };
		node.Next = node;

		Node? actual = serializer.Deserialize<Node>(serializer.Serialize(node));

		await Assert.That(actual!.Name).IsEqualTo("a");
		await Assert.That(ReferenceEquals(actual, actual.Next)).IsTrue();
	}

	[Test]
	public async Task AllowCycles_RoundTripsALongerCycle()
	{
		MsgPackSerializer serializer = this.plain with { PreserveReferences = ReferencePreservationMode.AllowCycles };
		Node a = new() { Name = "a" };
		Node b = new() { Name = "b" };
		a.Next = b;
		b.Next = a;

		Node? actual = serializer.Deserialize<Node>(serializer.Serialize(a));

		await Assert.That(actual!.Name).IsEqualTo("a");
		await Assert.That(actual.Next!.Name).IsEqualTo("b");
		await Assert.That(ReferenceEquals(actual, actual.Next.Next)).IsTrue();
	}

	[Test]
	public async Task References_UseTheReservedExtension()
	{
		MsgPackSerializer serializer = this.plain with { PreserveReferences = ReferencePreservationMode.RejectCycles };
		Node shared = new() { Name = "shared" };
		byte[] encoded = serializer.Serialize(new Pair { First = shared, Second = shared });

		// Somewhere after the first copy of the shared node there must be a reference extension.
		bool foundReference = false;
		for (int i = 0; i < encoded.Length - 1; i++)
		{
			if (encoded[i] == 0xd4 && encoded[i + 1] == unchecked((byte)MsgPackExtensionCodes.Reference))
			{
				foundReference = true;
				break;
			}
		}

		await Assert.That(foundReference).IsTrue();
	}

	[Test]
	public async Task ManyObjects_UseWiderReferenceIdentifiers()
	{
		MsgPackSerializer serializer = this.plain with { PreserveReferences = ReferencePreservationMode.RejectCycles };
		Node[] nodes = new Node[600];
		for (int i = 0; i < nodes.Length; i++)
		{
			nodes[i] = new Node { Name = i.ToString(System.Globalization.CultureInfo.InvariantCulture) };
		}

		// Repeat the array so that every node is written once and then referenced, forcing identifiers past 255.
		Node[] doubled = [.. nodes, .. nodes];

		Node[]? actual = serializer.Deserialize<Node[], Witness>(serializer.Serialize<Node[], Witness>(doubled));

		await Assert.That(actual!.Length).IsEqualTo(1200);
		await Assert.That(ReferenceEquals(actual[599], actual[1199])).IsTrue();
		await Assert.That(actual[1199].Name).IsEqualTo("599");
	}

	[Test]
	public async Task PayloadWithoutReferences_StillReadsWhenPreservationIsOn()
	{
		MsgPackSerializer serializer = this.plain with { PreserveReferences = ReferencePreservationMode.RejectCycles };
		Node shared = new() { Name = "shared" };
		byte[] plainBytes = this.plain.Serialize(new Pair { First = shared, Second = shared });

		Pair? actual = serializer.Deserialize<Pair>(plainBytes);

		await Assert.That(actual!.First!.Name).IsEqualTo("shared");
		await Assert.That(ReferenceEquals(actual.First, actual.Second)).IsFalse();
	}

	[Test]
	public async Task DanglingReference_IsRejected()
	{
		MsgPackSerializer serializer = this.plain with { PreserveReferences = ReferencePreservationMode.RejectCycles };

		// An array whose only element is a reference to object 9, which was never written.
		byte[] malformed = [0x91, 0xd4, unchecked((byte)MsgPackExtensionCodes.Reference), 9];

		Func<Node[]?> deserialize = () => serializer.Deserialize<Node[], Witness>(malformed);

		await Assert.That(deserialize).ThrowsException();
	}

	[Test]
	public async Task NullValues_AreStillNull()
	{
		MsgPackSerializer serializer = this.plain with { PreserveReferences = ReferencePreservationMode.AllowCycles };
		Pair value = new() { First = null, Second = new Node { Name = "b" } };

		Pair? actual = serializer.Deserialize<Pair>(serializer.Serialize(value));

		await Assert.That(actual!.First).IsNull();
		await Assert.That(actual.Second!.Name).IsEqualTo("b");
	}

	[Test]
	public async Task GetContract_RefusesWhileReferencesArePreserved()
	{
		MsgPackSerializer serializer = this.plain with { PreserveReferences = ReferencePreservationMode.RejectCycles };

		Action describe = () => serializer.GetContract<Node>();

		await Assert.That(describe).Throws<NotSupportedException>();
	}

	[GenerateShape]
	internal partial class Node
	{
		public string? Name { get; set; }

		public Node? Next { get; set; }
	}

	[GenerateShape]
	internal partial class Pair
	{
		public Node? First { get; set; }

		public Node? Second { get; set; }
	}

	[GenerateShapeFor<Node[]>]
	private partial class Witness;
}
