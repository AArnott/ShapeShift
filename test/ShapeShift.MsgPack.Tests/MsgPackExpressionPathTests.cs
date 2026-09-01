// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq.Expressions;
using ShapeShift.Tests;

namespace ShapeShift.MsgPack.Tests;

/// <summary>
/// Verifies that
/// <see cref="ShapeShiftSerializer{TEncoder, TDecoder}.GetPath{TRoot, TValue}(Expression{Func{TRoot, TValue}}, ITypeShape{TRoot})"/>
/// honors the MessagePack-specific positional (array) contract by emitting indexes where a map contract would
/// emit names.
/// </summary>
public partial class MsgPackExpressionPathTests : TestBase
{
	private readonly MsgPackSerializer serializer = new();

	[Test]
	public async Task PositionalContract_ProducesIndexes()
	{
		await Assert.That(this.serializer.GetPath((Envelope e) => e.Owner.Name)).IsEqualTo(new ShapeShiftPath(0, 1));
	}

	[Test]
	public async Task PositionalContract_HonorsDeclaredPositionsNotDeclarationOrder()
	{
		// Age is declared first but occupies position 0.
		await Assert.That(this.serializer.GetPath((Person p) => p.Age)).IsEqualTo(new ShapeShiftPath(0));
		await Assert.That(this.serializer.GetPath((Person p) => p.Name)).IsEqualTo(new ShapeShiftPath(1));
	}

	[Test]
	public async Task PositionalContract_MixesWithVectorIndexes()
	{
		await Assert.That(this.serializer.GetPath((Envelope e) => e.Others[2].Name)).IsEqualTo(new ShapeShiftPath(1, 2, 1));
	}

	[Test]
	public async Task PositionalContract_IsUnaffectedByNamingPolicy()
	{
		MsgPackSerializer camel = this.serializer with { PropertyNamingPolicy = ShapeShiftNamingPolicy.CamelCase };

		await Assert.That(camel.GetPath((Person p) => p.Name)).IsEqualTo(new ShapeShiftPath(1));
	}

	[Test]
	public async Task MapContract_StillProducesNames()
	{
		await Assert.That(this.serializer.GetPath((MapEnvelope e) => e.Owner.Name)).IsEqualTo(new ShapeShiftPath("Owner", 1));
	}

	[Test]
	public async Task GeneratedPath_DeserializesTheIntendedFragment()
	{
		Envelope envelope = new(new Person(36, "Ada"), [new Person(1, "Zero"), new Person(2, "One"), new Person(3, "Two")]);
		byte[] msgpack = this.serializer.Serialize(envelope);

		await Assert.That(this.serializer.DeserializeFragment<string, Witness>(msgpack, this.serializer.GetPath((Envelope e) => e.Owner.Name))).IsEqualTo("Ada");
		await Assert.That(this.serializer.DeserializeFragment<int, Witness>(msgpack, this.serializer.GetPath((Envelope e) => e.Owner.Age))).IsEqualTo(36);
		await Assert.That(this.serializer.DeserializeFragment<string, Witness>(msgpack, this.serializer.GetPath((Envelope e) => e.Others[2].Name))).IsEqualTo("Two");
		await Assert.That(this.serializer.DeserializeFragment<Person>(msgpack, this.serializer.GetPath((Envelope e) => e.Others[1]))).IsEqualTo(new Person(2, "One"));
	}

	[Test]
	public async Task GeneratedPath_DeserializesFromAMapContract()
	{
		MapEnvelope envelope = new(new Person(36, "Ada"));
		byte[] msgpack = this.serializer.Serialize(envelope);

		await Assert.That(this.serializer.DeserializeFragment<string, Witness>(msgpack, this.serializer.GetPath((MapEnvelope e) => e.Owner.Name))).IsEqualTo("Ada");
	}

	[GenerateShape]
	[MsgPackArrayContract]
	internal partial record Person([property: MsgPackKey(0)] int Age, [property: MsgPackKey(1)] string Name);

	[GenerateShape]
	[MsgPackArrayContract]
	internal partial record Envelope([property: MsgPackKey(0)] Person Owner, [property: MsgPackKey(1)] List<Person> Others);

	[GenerateShape]
	internal partial record MapEnvelope(Person Owner);

	[GenerateShapeFor<string>]
	[GenerateShapeFor<int>]
	private partial class Witness;
}
