// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using PolyType;
using ShapeShift.Tests;

namespace ShapeShift.Protobuf.Tests;

public partial class ProtobufSerializerTests : TestBase
{
	private readonly ProtobufSerializer serializer = new();

	[Test]
	public async Task PrimitiveRoots_RoundTrip()
	{
		await Assert.That(this.serializer.Deserialize<int, Witness>(this.serializer.Serialize<int, Witness>(42))).IsEqualTo(42);
		await Assert.That(this.serializer.Deserialize<string, Witness>(this.serializer.Serialize<string, Witness>("hello"))).IsEqualTo("hello");
		await Assert.That(this.serializer.Deserialize<bool, Witness>(this.serializer.Serialize<bool, Witness>(true))).IsTrue();
	}

	[Test]
	public async Task ObjectAndCollection_RoundTrip()
	{
		Person value = new("Ada", [1, 2, 3]);
		byte[] encoded = this.serializer.Serialize(value);
		Person? actual = this.serializer.Deserialize<Person>(encoded);

		await Assert.That(actual).IsNotNull();
		await Assert.That(actual!.Name).IsEqualTo(value.Name);
		await Assert.That(actual.Values.SequenceEqual(value.Values)).IsTrue();
		await Assert.That(encoded.Length).IsGreaterThan(0);
	}

	[Test]
	public async Task BinaryPayload_RoundTrips()
	{
		byte[] value = [1, 2, 3, 4];
		byte[] encoded = this.serializer.Serialize<byte[], Witness>(value);
		byte[]? actual = this.serializer.Deserialize<byte[], Witness>(encoded);

		await Assert.That(actual).IsNotNull();
		await Assert.That(actual!.SequenceEqual(value)).IsTrue();
	}

	[Test]
	public async Task NullablePrimitive_UsesNullToken()
	{
		string? value = null;
		byte[] encoded = this.serializer.Serialize<string?, Witness>(value);
		await Assert.That(this.serializer.Deserialize<string?, Witness>(encoded)).IsNull();
	}

	[GenerateShape]
	internal partial record Person(string Name, List<int> Values);

	[GenerateShapeFor<int>]
	[GenerateShapeFor<string>]
	[GenerateShapeFor<bool>]
	[GenerateShapeFor<byte[]>]
	private partial class Witness;
}
