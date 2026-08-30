// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;
using System.Text.Json.Nodes;
using ShapeShift.Json;
using ShapeShift.Schema;
using ShapeShift.Tests;

namespace ShapeShift.MsgPack.Tests;

/// <summary>
/// Verifies that contracts produced by <see cref="MsgPackSerializer"/> project to JSON Schema
/// with MessagePack-specific annotations.
/// </summary>
public partial class MsgPackSchemaTests : TestBase
{
	private static readonly JsonSchemaOptions Options = new() { Profile = JsonSchemaProfile.MessagePack };

	private readonly MsgPackSerializer serializer = new();

	[Test]
	public async Task Object_IsAnnotatedAsAMap()
	{
		JsonObject schema = this.Schema<Person>();

		await Assert.That((string?)schema["type"]).IsEqualTo("object");
		await Assert.That((string?)schema["x-msgpack-type"]).IsEqualTo("map");
		await Assert.That((string?)schema["$schema"]).IsEqualTo(JsonSchema.Dialect);
	}

	[Test]
	public async Task Binary_UsesTheBinFamily()
	{
		JsonObject blob = (JsonObject)this.Schema<Blobs>()["properties"]!["Data"]!;

		await Assert.That((string?)blob["x-msgpack-type"]).IsEqualTo("bin");
		await Assert.That((string?)blob["$comment"]).Contains("bin family");
	}

	[Test]
	public async Task Extensions_AreAnnotatedWithTheirTypeCodes()
	{
		JsonObject properties = (JsonObject)this.Schema<Exotic>()["properties"]!;

		await Assert.That((int?)properties["Timestamp"]!["x-msgpack-extension"]).IsEqualTo((int)MsgPackExtensionCodes.Timestamp);
		await Assert.That((int?)properties["Money"]!["x-msgpack-extension"]).IsEqualTo((int)MsgPackExtensionCodes.Decimal);
		await Assert.That((int?)properties["Signed"]!["x-msgpack-extension"]).IsEqualTo((int)MsgPackExtensionCodes.Int128);
		await Assert.That((int?)properties["Unsigned"]!["x-msgpack-extension"]).IsEqualTo((int)MsgPackExtensionCodes.UInt128);
		await Assert.That((int?)properties["Huge"]!["x-msgpack-extension"]).IsEqualTo((int)MsgPackExtensionCodes.BigInteger);
		await Assert.That((int?)properties["Duration"]!["x-msgpack-extension"]).IsEqualTo((int)MsgPackExtensionCodes.TimeSpan);
	}

	[Test]
	public async Task TimeSpan_IsANumberInMessagePack()
	{
		JsonObject duration = (JsonObject)this.Schema<Exotic>()["properties"]!["Duration"]!;

		await Assert.That((string?)duration["type"]).IsEqualTo("integer");
	}

	[Test]
	public async Task DateTimeOffset_AnnotatesItsTimestampComponent()
	{
		JsonObject offset = (JsonObject)this.Schema<Exotic>()["properties"]!["Offset"]!;

		await Assert.That((string?)offset["type"]).IsEqualTo("array");
		await Assert.That((int?)((JsonArray)offset["prefixItems"]!)[0]!["x-msgpack-extension"]).IsEqualTo(-1);
	}

	[Test]
	public async Task Collections_AreAnnotated()
	{
		JsonObject properties = (JsonObject)this.Schema<Collections>()["properties"]!;

		await Assert.That((string?)properties["Items"]!["x-msgpack-type"]).IsEqualTo("array");
		await Assert.That((string?)properties["Words"]!["x-msgpack-type"]).IsEqualTo("map");
		await Assert.That((string?)properties["Numbers"]!["x-msgpack-type"]).IsEqualTo("array");
		await Assert.That((string?)properties["Numbers"]!["$comment"]).Contains("[key, value] pairs");
	}

	[Test]
	public async Task NamedFloatingPointValues_AreNeverOfferedInTheMessagePackProfile()
	{
		JsonObject value = (JsonObject)JsonSchema.Create(
			this.serializer.GetContract<Floats>(),
			new JsonSchemaOptions { Profile = JsonSchemaProfile.MessagePack, AllowNamedFloatingPointValues = true })["properties"]!["Value"]!;

		await Assert.That((string?)value["type"]).IsEqualTo("number");
		await Assert.That(value["anyOf"]).IsNull();
	}

	[Test]
	public async Task Contract_MatchesTheJsonContractExceptForFormatSpecificChoices()
	{
		DataContract msgpack = this.serializer.GetContract<Person>();
		DataContract json = new JsonSerializer().GetContract<Person>();

		await Assert.That(msgpack.Kind).IsEqualTo(json.Kind);
		await Assert.That(((ObjectContract)msgpack).Properties.Length).IsEqualTo(((ObjectContract)json).Properties.Length);
	}

	[Test]
	public async Task Contract_IsRejectedWhileReferencesArePreserved()
	{
		// Reference preservation replaces repeated values with back-references, which no static
		// contract can describe, so describing a type is refused while it is enabled.
		MsgPackSerializer preserving = this.serializer with { PreserveReferences = ReferencePreservationMode.RejectCycles };
		Func<DataContract> act = () => preserving.GetContract<Person>();

		await Assert.That(act).Throws<NotSupportedException>();
	}

	private JsonObject Schema<T>()
		where T : IShapeable<T> => JsonSchema.Create(this.serializer.GetContract<T>(), Options);

	[GenerateShape]
	internal partial record Person(string Name, int Age);

	[GenerateShape]
	internal partial record Blobs(byte[] Data);

	[GenerateShape]
	internal partial record Floats(double Value);

	[GenerateShape]
	internal partial record Collections(List<int> Items, Dictionary<string, int> Words, Dictionary<int, string> Numbers);

	[GenerateShape]
	internal partial record Exotic(
		DateTime Timestamp,
		DateTimeOffset Offset,
		TimeSpan Duration,
		decimal Money,
		Int128 Signed,
		UInt128 Unsigned,
		BigInteger Huge);
}
