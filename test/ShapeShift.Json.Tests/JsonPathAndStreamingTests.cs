// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using System.Text.Json;
using ShapeShift.Tests;

namespace ShapeShift.Json.Tests;

/// <summary>
/// Verifies JSON support for the targeted and streaming deserialization primitives:
/// <c>TrySeek</c>, fragment deserialization, and incremental sequence/document readers.
/// </summary>
public partial class JsonPathAndStreamingTests : TestBase
{
	private readonly JsonSerializer serializer = new();

	[Test]
	public async Task TrySeek_FindsNestedProperty()
	{
		string json = """{"Name":"Ada","Address":{"City":"London","Zip":"E1"},"Tags":["a","b","c"]}""";
		JsonDecoder decoder = new(Encoding.UTF8.GetBytes(json));

		bool found = decoder.TrySeek(new ShapeShiftPath("Address", "City"));
		string? city = found ? this.serializer.Deserialize(ref decoder, GetTypeShape<string, Witness>()) : null;

		await Assert.That(found).IsTrue();
		await Assert.That(city).IsEqualTo("London");
	}

	[Test]
	public async Task TrySeek_FindsVectorElement()
	{
		string json = """{"Name":"Ada","Tags":["a","b","c"]}""";
		JsonDecoder decoder = new(Encoding.UTF8.GetBytes(json));

		bool found = decoder.TrySeek(new ShapeShiftPath("Tags", 1));
		string? value = found ? this.serializer.Deserialize(ref decoder, GetTypeShape<string, Witness>()) : null;

		await Assert.That(found).IsTrue();
		await Assert.That(value).IsEqualTo("b");
	}

	[Test]
	public async Task TrySeek_MissingProperty_ReturnsFalse()
	{
		string json = """{"Name":"Ada"}""";
		JsonDecoder decoder = new(Encoding.UTF8.GetBytes(json));

		bool found = decoder.TrySeek(new ShapeShiftPath("Address", "City"));

		await Assert.That(found).IsFalse();
	}

	[Test]
	public async Task TrySeek_NullMidPath_ReturnsFalse()
	{
		string json = """{"Name":"Ada","Address":null}""";
		JsonDecoder decoder = new(Encoding.UTF8.GetBytes(json));

		bool found = decoder.TrySeek(new ShapeShiftPath("Address", "City"));

		await Assert.That(found).IsFalse();
	}

	[Test]
	public async Task TrySeek_IndexOutOfRange_ReturnsFalse()
	{
		string json = """{"Tags":["a","b"]}""";
		JsonDecoder decoder = new(Encoding.UTF8.GetBytes(json));

		bool found = decoder.TrySeek(new ShapeShiftPath("Tags", 5));

		await Assert.That(found).IsFalse();
	}

	[Test]
	public async Task TrySeek_EmptyVector_ReturnsFalse()
	{
		string json = """{"Tags":[]}""";
		JsonDecoder decoder = new(Encoding.UTF8.GetBytes(json));

		bool found = decoder.TrySeek(new ShapeShiftPath("Tags", 0));

		await Assert.That(found).IsFalse();
	}

	[Test]
	public async Task TrySeek_TypeMismatch_PropagatesDecoderException()
	{
		string json = """{"Name":"Ada"}""";
		JsonDecoder decoder = new(Encoding.UTF8.GetBytes(json));

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
		string json = """{"Name":"Ada"}""";
		JsonDecoder decoder = new(Encoding.UTF8.GetBytes(json));

		bool found = decoder.TrySeek(ShapeShiftPath.Root);
		Person? person = found ? this.serializer.Deserialize(ref decoder, GetTypeShape<Person, Person>()) : null;

		await Assert.That(found).IsTrue();
		await Assert.That(person).IsEqualTo(new Person("Ada"));
	}

	[Test]
	public async Task TryDeserializeFragment_FindsNestedValue()
	{
		string json = """{"Name":"Ada","Address":{"City":"London","Zip":"E1"}}""";

		bool found = this.serializer.TryDeserializeFragment<string, Witness>(json, new ShapeShiftPath("Address", "City"), out string? city);

		await Assert.That(found).IsTrue();
		await Assert.That(city).IsEqualTo("London");
	}

	[Test]
	public async Task TryDeserializeFragment_MissingPath_ReturnsFalse()
	{
		string json = """{"Name":"Ada"}""";

		bool found = this.serializer.TryDeserializeFragment<string, Witness>(json, new ShapeShiftPath("Address", "City"), out string? city);

		await Assert.That(found).IsFalse();
		await Assert.That(city).IsNull();
	}

	[Test]
	public async Task DeserializeFragment_ThrowsWhenPathNotFound()
	{
		string json = """{"Name":"Ada"}""";

		Func<string?> act = () => this.serializer.DeserializeFragment<string, Witness>(json, new ShapeShiftPath("Address", "City"));

		await Assert.That(act).Throws<ShapeShiftSerializationException>();
	}

	[Test]
	public async Task DeserializeFragment_DeserializesWholeObjectAtPath()
	{
		string json = """{"Name":"Ada","Address":{"City":"London","Zip":"E1"}}""";

		Address? address = this.serializer.DeserializeFragment<Address>(json, new ShapeShiftPath("Address"));

		await Assert.That(address).IsEqualTo(new Address("London", "E1"));
	}

	[Test]
	public async Task SequenceReader_EnumeratesTopLevelArray()
	{
		string json = """["a","b","c"]""";
		JsonDecoder decoder = new(Encoding.UTF8.GetBytes(json));
		using ShapeShiftSequenceReader<string, JsonEncoder, JsonDecoder> reader = this.serializer.CreateSequenceReader<string, Witness>();

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
		string json = """[]""";
		JsonDecoder decoder = new(Encoding.UTF8.GetBytes(json));
		using ShapeShiftSequenceReader<string, JsonEncoder, JsonDecoder> reader = this.serializer.CreateSequenceReader<string, Witness>();

		await Assert.That(reader.MoveNext(ref decoder)).IsFalse();
	}

	[Test]
	public async Task SequenceReader_EnumeratesNestedArrayReachedBySeeking()
	{
		string json = """{"Name":"Ada","Tags":["a","b","c"]}""";
		JsonDecoder decoder = new(Encoding.UTF8.GetBytes(json));
		bool found = decoder.TrySeek(new ShapeShiftPath("Tags"));
		using ShapeShiftSequenceReader<string, JsonEncoder, JsonDecoder> reader = this.serializer.CreateSequenceReader<string, Witness>();

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
		string json = """[{"Name":"Ada"},{"Name":"Bob"}]""";
		JsonDecoder decoder = new(Encoding.UTF8.GetBytes(json));
		using ShapeShiftSequenceReader<Person, JsonEncoder, JsonDecoder> reader = this.serializer.CreateSequenceReader<Person>();

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
		string ndjson = """{"Name":"Ada"}{"Name":"Bob"}{"Name":"Cid"}""";
		JsonDecoder decoder = new(Encoding.UTF8.GetBytes(ndjson));
		using ShapeShiftDocumentReader<Person, JsonEncoder, JsonDecoder> reader = this.serializer.CreateDocumentReader<Person>();

		List<Person?> items = [];
		while (reader.MoveNext(ref decoder))
		{
			items.Add(reader.Current);
		}

		await Assert.That(items.SequenceEqual([new Person("Ada"), new Person("Bob"), new Person("Cid")])).IsTrue();
	}

	[Test]
	public async Task DocumentReader_NewlineDelimitedJson_RoundTrips()
	{
		string ndjson = "{\"Name\":\"Ada\"}\n{\"Name\":\"Bob\"}\n";
		JsonDecoder decoder = new(Encoding.UTF8.GetBytes(ndjson));
		using ShapeShiftDocumentReader<Person, JsonEncoder, JsonDecoder> reader = this.serializer.CreateDocumentReader<Person>();

		List<Person?> items = [];
		while (reader.MoveNext(ref decoder))
		{
			items.Add(reader.Current);
		}

		await Assert.That(items.SequenceEqual([new Person("Ada"), new Person("Bob")])).IsTrue();
	}

	[Test]
	public async Task DocumentReader_EmptyInput_ThrowsBecauseJsonRequiresAtLeastOneToken()
	{
		// JSON (unlike some other formats) has no way to represent "zero top-level values": even a single
		// top-level value is required for a document to be well-formed, so the decoder constructor itself
		// rejects a completely empty buffer rather than allowing a document reader to observe zero values.
		Action act = () => _ = new JsonDecoder(Encoding.UTF8.GetBytes(string.Empty));

		await Assert.That(act).Throws<JsonException>();
	}

	[Test]
	public async Task DocumentReader_SingleValue_MatchesOrdinaryDeserialize()
	{
		string json = """{"Name":"Ada"}""";
		JsonDecoder decoder = new(Encoding.UTF8.GetBytes(json));
		using ShapeShiftDocumentReader<Person, JsonEncoder, JsonDecoder> reader = this.serializer.CreateDocumentReader<Person>();

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

	[GenerateShapeFor<string>]
	private partial class Witness;
}
