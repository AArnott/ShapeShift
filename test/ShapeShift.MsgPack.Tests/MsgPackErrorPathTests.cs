// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ShapeShift.Tests;

namespace ShapeShift.MsgPack.Tests;

/// <summary>
/// Verifies that MessagePack (de)serialization failures carry an actionable
/// <see cref="ShapeShiftPath"/> breadcrumb trail.
/// </summary>
public partial class MsgPackErrorPathTests : TestBase
{
	private readonly MsgPackSerializer serializer = new();

	[Test]
	public async Task NestedObjectProperty_ReportsPath()
	{
		byte[] payload = this.serializer.Serialize(new WireRoot { Child = new WireChild { Age = "nope" } });

		ShapeShiftSerializationException ex = await this.AssertFails<Root>(payload);

		await Assert.That(ex.Path).IsEqualTo(new ShapeShiftPath("Child", "Age"));
		await Assert.That(ex.Message).Contains("$.Child.Age");
	}

	[Test]
	public async Task CollectionElement_ReportsIndex()
	{
		byte[] payload = this.serializer.Serialize(new WireRoot { Numbers = [1L, 2L, "nope"] });

		ShapeShiftSerializationException ex = await this.AssertFails<Root>(payload);

		await Assert.That(ex.Path).IsEqualTo(new ShapeShiftPath("Numbers", 2));
	}

	[Test]
	public async Task NestedCollectionOfObjects_ReportsFullPath()
	{
		byte[] payload = this.serializer.Serialize(new WireRoot
		{
			Children = [new WireChild { Age = 1L }, new WireChild { Age = "nope" }],
		});

		ShapeShiftSerializationException ex = await this.AssertFails<Root>(payload);

		await Assert.That(ex.Path).IsEqualTo(new ShapeShiftPath("Children", 1, "Age"));
	}

	[Test]
	public async Task StringKeyedMapValue_ReportsPropertyName()
	{
		byte[] payload = this.serializer.Serialize(new WireRoot
		{
			Map = new Dictionary<string, string> { ["b"] = "nope" },
		});

		ShapeShiftSerializationException ex = await this.AssertFails<Root>(payload);

		await Assert.That(ex.Path).IsEqualTo(new ShapeShiftPath("Map", "b"));
	}

	[Test]
	public async Task NonStringKeyedMapEntry_ReportsEntryAndSlot()
	{
		byte[] payload = this.serializer.Serialize(new WireRoot
		{
			IntMap = new Dictionary<int, int> { [1] = 5 },
		});

		ShapeShiftSerializationException ex = await this.AssertFails<Root>(payload);

		await Assert.That(ex.Path).IsEqualTo(new ShapeShiftPath("IntMap", 0, 1));
	}

	[Test]
	public async Task ConverterThrowingDuringWrite_ReportsPath()
	{
		MsgPackSerializer custom = this.serializer with { Converters = [new ThrowingConverter()] };

		Func<byte[]> act = () => custom.Serialize(new ExplosiveList { Explosives = [new Boom(), new Boom()] });

		ShapeShiftSerializationException ex = await Assert.That(act).Throws<ShapeShiftSerializationException>() ?? throw new InvalidOperationException();
		await Assert.That(ex.Path).IsEqualTo(new ShapeShiftPath("Explosives", 0));
		await Assert.That(ex.InnerException).IsTypeOf<NotSupportedException>();
	}

	[Test]
	public async Task DuplicateProperty_ReportsPropertyPath()
	{
		byte[] payload = [0x82, 0xa3, (byte)'A', (byte)'g', (byte)'e', 0x01, 0xa3, (byte)'A', (byte)'g', (byte)'e', 0x02];

		ShapeShiftSerializationException ex = await this.AssertFails<Root>(payload);

		await Assert.That(ex.Path).IsEqualTo(new ShapeShiftPath("Age"));
	}

	[Test]
	public async Task SuccessfulRoundTrip_IsUnaffected()
	{
		Root value = new()
		{
			Age = 3,
			Child = new Child { Age = 4 },
			Numbers = [1, 2],
			IntMap = new Dictionary<int, string> { [1] = "one" },
		};

		Root? actual = this.serializer.Deserialize<Root>(this.serializer.Serialize(value));

		await Assert.That(actual?.Child?.Age).IsEqualTo(4);
		await Assert.That(actual?.IntMap?[1]).IsEqualTo("one");
	}

	private async Task<ShapeShiftSerializationException> AssertFails<T>(byte[] payload)
		where T : IShapeable<T>
	{
		Func<T?> act = () => this.serializer.Deserialize<T>(payload);
		ShapeShiftSerializationException? ex = await Assert.That(act).Throws<ShapeShiftSerializationException>();
		return ex ?? throw new InvalidOperationException("Expected an exception.");
	}

	internal sealed class Boom;

	internal sealed class ThrowingConverter : ShapeShiftConverter<Boom, MsgPackEncoder, MsgPackDecoder>
	{
		public override Boom? Read(ref MsgPackDecoder decoder, SerializationContext<MsgPackEncoder, MsgPackDecoder> context)
			=> throw new NotSupportedException("read boom");

		public override void Write(ref MsgPackEncoder encoder, in Boom? value, SerializationContext<MsgPackEncoder, MsgPackDecoder> context)
			=> throw new NotSupportedException("write boom");
	}

	[GenerateShape]
	internal partial class Root
	{
		public int Age { get; set; }

		public Child? Child { get; set; }

		public List<int>? Numbers { get; set; }

		public List<Child>? Children { get; set; }

		public Dictionary<string, int>? Map { get; set; }

		public Dictionary<int, string>? IntMap { get; set; }
	}

	[GenerateShape]
	internal partial class Child
	{
		public int Age { get; set; }
	}

	[GenerateShape]
	internal partial class WireRoot
	{
		public WireChild? Child { get; set; }

		public List<ShapeShiftValue>? Numbers { get; set; }

		public List<WireChild>? Children { get; set; }

		public Dictionary<string, string>? Map { get; set; }

		public Dictionary<int, int>? IntMap { get; set; }
	}

	[GenerateShape]
	internal partial class WireChild
	{
		public ShapeShiftValue Age { get; set; } = 0L;
	}

	[GenerateShape]
	internal partial class ExplosiveList
	{
		public List<Boom>? Explosives { get; set; }
	}
}
