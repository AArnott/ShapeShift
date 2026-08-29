// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ShapeShift.Tests;

namespace ShapeShift.MsgPack.Tests;

/// <summary>
/// Verifies MessagePack support for the targeted and streaming deserialization primitives:
/// <c>TrySeek</c>, fragment deserialization, and incremental sequence/document readers.
/// </summary>
public partial class MsgPackPathAndStreamingTests : TestBase
{
	private readonly MsgPackSerializer serializer = new();

	[Test]
	public async Task TrySeek_FindsNestedProperty()
	{
		byte[] msgpack = this.serializer.Serialize(new PersonWithAddress("Ada", new Address("London", "E1"), ["a", "b", "c"]));
		MsgPackDecoder decoder = new(msgpack);

		bool found = decoder.TrySeek(new ShapeShiftPath("Address", "City"));
		string? city = found ? this.serializer.Deserialize(ref decoder, GetTypeShape<string, Witness>()) : null;

		await Assert.That(found).IsTrue();
		await Assert.That(city).IsEqualTo("London");
	}

	[Test]
	public async Task TrySeek_FindsVectorElement()
	{
		byte[] msgpack = this.serializer.Serialize(new PersonWithAddress("Ada", null, ["a", "b", "c"]));
		MsgPackDecoder decoder = new(msgpack);

		bool found = decoder.TrySeek(new ShapeShiftPath("Tags", 1));
		string? value = found ? this.serializer.Deserialize(ref decoder, GetTypeShape<string, Witness>()) : null;

		await Assert.That(found).IsTrue();
		await Assert.That(value).IsEqualTo("b");
	}

	[Test]
	public async Task TrySeek_MissingProperty_ReturnsFalse()
	{
		byte[] msgpack = this.serializer.Serialize(new Person("Ada"));
		MsgPackDecoder decoder = new(msgpack);

		bool found = decoder.TrySeek(new ShapeShiftPath("Address", "City"));

		await Assert.That(found).IsFalse();
	}

	[Test]
	public async Task TrySeek_NullMidPath_ReturnsFalse()
	{
		byte[] msgpack = this.serializer.Serialize(new PersonWithAddress("Ada", null, []));
		MsgPackDecoder decoder = new(msgpack);

		bool found = decoder.TrySeek(new ShapeShiftPath("Address", "City"));

		await Assert.That(found).IsFalse();
	}

	[Test]
	public async Task TrySeek_IndexOutOfRange_ReturnsFalse()
	{
		byte[] msgpack = this.serializer.Serialize(new PersonWithAddress("Ada", null, ["a", "b"]));
		MsgPackDecoder decoder = new(msgpack);

		bool found = decoder.TrySeek(new ShapeShiftPath("Tags", 5));

		await Assert.That(found).IsFalse();
	}

	[Test]
	public async Task TrySeek_EmptyVector_ReturnsFalse()
	{
		byte[] msgpack = this.serializer.Serialize(new PersonWithAddress("Ada", null, []));
		MsgPackDecoder decoder = new(msgpack);

		bool found = decoder.TrySeek(new ShapeShiftPath("Tags", 0));

		await Assert.That(found).IsFalse();
	}

	[Test]
	public async Task TrySeek_TypeMismatch_PropagatesDecoderException()
	{
		byte[] msgpack = this.serializer.Serialize(new Person("Ada"));
		MsgPackDecoder decoder = new(msgpack);

		DecoderException? caught = null;
		try
		{
			decoder.TrySeek(new ShapeShiftPath("Name", "Inner"));
		}
		catch (DecoderException ex)
		{
			caught = ex;
		}

		await Assert.That(caught).IsNotNull();
	}

	[Test]
	public async Task TrySeek_RootPath_LeavesDecoderAtStart()
	{
		byte[] msgpack = this.serializer.Serialize(new Person("Ada"));
		MsgPackDecoder decoder = new(msgpack);

		bool found = decoder.TrySeek(ShapeShiftPath.Root);
		Person? person = found ? this.serializer.Deserialize(ref decoder, GetTypeShape<Person, Person>()) : null;

		await Assert.That(found).IsTrue();
		await Assert.That(person).IsEqualTo(new Person("Ada"));
	}

	[Test]
	public async Task TryDeserializeFragment_FindsNestedValue()
	{
		byte[] msgpack = this.serializer.Serialize(new PersonWithAddress("Ada", new Address("London", "E1"), []));

		bool found = this.serializer.TryDeserializeFragment<string, Witness>(msgpack, new ShapeShiftPath("Address", "City"), out string? city);

		await Assert.That(found).IsTrue();
		await Assert.That(city).IsEqualTo("London");
	}

	[Test]
	public async Task TryDeserializeFragment_MissingPath_ReturnsFalse()
	{
		byte[] msgpack = this.serializer.Serialize(new Person("Ada"));

		bool found = this.serializer.TryDeserializeFragment<string, Witness>(msgpack, new ShapeShiftPath("Address", "City"), out string? city);

		await Assert.That(found).IsFalse();
		await Assert.That(city).IsNull();
	}

	[Test]
	public async Task DeserializeFragment_ThrowsWhenPathNotFound()
	{
		byte[] msgpack = this.serializer.Serialize(new Person("Ada"));

		Func<string?> act = () => this.serializer.DeserializeFragment<string, Witness>(msgpack, new ShapeShiftPath("Address", "City"));

		await Assert.That(act).Throws<ShapeShiftSerializationException>();
	}

	[Test]
	public async Task DeserializeFragment_DeserializesWholeObjectAtPath()
	{
		byte[] msgpack = this.serializer.Serialize(new PersonWithAddress("Ada", new Address("London", "E1"), []));

		Address? address = this.serializer.DeserializeFragment<Address>(msgpack, new ShapeShiftPath("Address"));

		await Assert.That(address).IsEqualTo(new Address("London", "E1"));
	}

	[Test]
	public async Task SequenceReader_EnumeratesTopLevelArray()
	{
		byte[] msgpack = this.serializer.Serialize<string[], Witness>(["a", "b", "c"]);
		MsgPackDecoder decoder = new(msgpack);
		using ShapeShiftSequenceReader<string, MsgPackEncoder, MsgPackDecoder> reader = this.serializer.CreateSequenceReader<string, Witness>();

		List<string?> items = [];
		while (reader.MoveNext(ref decoder))
		{
			items.Add(reader.Current);
		}

		await Assert.That(items.SequenceEqual(["a", "b", "c"])).IsTrue();
	}

	[Test]
	public async Task SequenceReader_EnumeratesEmptyArray()
	{
		byte[] msgpack = this.serializer.Serialize<string[], Witness>([]);
		MsgPackDecoder decoder = new(msgpack);
		using ShapeShiftSequenceReader<string, MsgPackEncoder, MsgPackDecoder> reader = this.serializer.CreateSequenceReader<string, Witness>();

		await Assert.That(reader.MoveNext(ref decoder)).IsFalse();
	}

	[Test]
	public async Task SequenceReader_EnumeratesNestedArrayReachedBySeeking()
	{
		byte[] msgpack = this.serializer.Serialize(new PersonWithAddress("Ada", null, ["a", "b", "c"]));
		MsgPackDecoder decoder = new(msgpack);
		bool found = decoder.TrySeek(new ShapeShiftPath("Tags"));
		using ShapeShiftSequenceReader<string, MsgPackEncoder, MsgPackDecoder> reader = this.serializer.CreateSequenceReader<string, Witness>();

		List<string?> items = [];
		while (found && reader.MoveNext(ref decoder))
		{
			items.Add(reader.Current);
		}

		await Assert.That(found).IsTrue();
		await Assert.That(items.SequenceEqual(["a", "b", "c"])).IsTrue();
	}

	[Test]
	public async Task SequenceReader_EnumeratesComplexElements()
	{
		byte[] msgpack = this.serializer.Serialize<Person[], Witness>([new Person("Ada"), new Person("Bob")]);
		MsgPackDecoder decoder = new(msgpack);
		using ShapeShiftSequenceReader<Person, MsgPackEncoder, MsgPackDecoder> reader = this.serializer.CreateSequenceReader<Person>();

		List<Person?> items = [];
		while (reader.MoveNext(ref decoder))
		{
			items.Add(reader.Current);
		}

		await Assert.That(items.SequenceEqual([new Person("Ada"), new Person("Bob")])).IsTrue();
	}

	[Test]
	public async Task DocumentReader_EnumeratesConcatenatedTopLevelValues()
	{
		// MessagePack values are self-delimiting by design, so a buffer containing several of them back-to-back
		// with no separators is itself already a valid stream: no special reconstruction is required (unlike
		// JSON, whose reader can only parse one top-level value per reader instance).
		byte[] concatenated = [
			.. this.serializer.Serialize(new Person("Ada")),
			.. this.serializer.Serialize(new Person("Bob")),
			.. this.serializer.Serialize(new Person("Cid")),
		];
		MsgPackDecoder decoder = new(concatenated);
		using ShapeShiftDocumentReader<Person, MsgPackEncoder, MsgPackDecoder> reader = this.serializer.CreateDocumentReader<Person>();

		List<Person?> items = [];
		while (reader.MoveNext(ref decoder))
		{
			items.Add(reader.Current);
		}

		await Assert.That(items.SequenceEqual([new Person("Ada"), new Person("Bob"), new Person("Cid")])).IsTrue();
	}

	[Test]
	public async Task DocumentReader_EmptyInput_YieldsNoValues()
	{
		// Unlike JSON, MessagePack has no requirement that at least one token be present: an empty buffer is
		// simply a stream containing zero top-level values.
		MsgPackDecoder decoder = new(ReadOnlySpan<byte>.Empty);
		using ShapeShiftDocumentReader<Person, MsgPackEncoder, MsgPackDecoder> reader = this.serializer.CreateDocumentReader<Person>();

		await Assert.That(reader.MoveNext(ref decoder)).IsFalse();
	}

	[Test]
	public async Task DocumentReader_SingleValue_MatchesOrdinaryDeserialize()
	{
		byte[] msgpack = this.serializer.Serialize(new Person("Ada"));
		MsgPackDecoder decoder = new(msgpack);
		using ShapeShiftDocumentReader<Person, MsgPackEncoder, MsgPackDecoder> reader = this.serializer.CreateDocumentReader<Person>();

		bool foundFirst = reader.MoveNext(ref decoder);
		Person? current = reader.Current;
		bool foundSecond = reader.MoveNext(ref decoder);

		await Assert.That(foundFirst).IsTrue();
		await Assert.That(current).IsEqualTo(new Person("Ada"));
		await Assert.That(foundSecond).IsFalse();
	}

	private static ITypeShape<T> GetTypeShape<T, TProvider>()
		where TProvider : IShapeable<T>
		=> TProvider.GetTypeShape();

	[GenerateShape]
	internal partial record Person(string Name);

	[GenerateShape]
	internal partial record Address(string City, string Zip);

	[GenerateShape]
	internal partial record PersonWithAddress(string Name, Address? Address, List<string> Tags);

	[GenerateShapeFor<string>]
	[GenerateShapeFor<string[]>]
	[GenerateShapeFor<Person[]>]
	private partial class Witness;
}
