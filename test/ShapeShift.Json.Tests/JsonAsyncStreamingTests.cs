// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using ShapeShift.Tests;

namespace ShapeShift.Json.Tests;

/// <summary>
/// Verifies the incremental, non-blocking <see cref="PipeWriter"/>/<see cref="PipeReader"/>/<see cref="Stream"/>
/// based async APIs on <see cref="JsonSerializer"/>: they must fill/drain bounded buffers a chunk at a time
/// around the existing synchronous conversion, never buffering an entire document up front, never blocking a
/// thread on I/O, and always honoring cancellation.
/// </summary>
public partial class JsonAsyncStreamingTests : TestBase
{
	private readonly JsonSerializer serializer = new();

	[Test]
	public async Task SerializeAsync_PipeWriter_DeserializeAsync_PipeReader_RoundTrip()
	{
		Person original = new("Ada");
		Pipe pipe = new();

		await this.serializer.SerializeAsync(pipe.Writer, original);
		await pipe.Writer.CompleteAsync();

		Person? actual = await this.serializer.DeserializeAsync<Person>(pipe.Reader);

		await Assert.That(actual).IsEqualTo(original);
	}

	[Test]
	public async Task SerializeAsync_Stream_DeserializeAsync_Stream_RoundTrip()
	{
		Person original = new("Ada");
		using MemoryStream stream = new();

		await this.serializer.SerializeAsync(stream, original);
		stream.Position = 0;

		Person? actual = await this.serializer.DeserializeAsync<Person>(stream);

		await Assert.That(actual).IsEqualTo(original);
	}

	[Test]
	public async Task SerializeAsync_DeserializeAsync_WithExplicitProvider_RoundTrip()
	{
		using MemoryStream stream = new();

		await this.serializer.SerializeAsync<string, Witness>(stream, "hello");
		stream.Position = 0;

		string? actual = await this.serializer.DeserializeAsync<string, Witness>(stream);

		await Assert.That(actual).IsEqualTo("hello");
	}

	[Test]
	public async Task DeserializeAsync_Stream_ToleratesByteAtATimeReads()
	{
		PersonWithAddress original = new("Ada", new Address("London", "E1"), ["a", "b", "c"]);
		byte[] data = this.serializer.SerializeToUtf8Bytes(original);
		using ChunkedReadStream stream = new(data, maxBytesPerRead: 1);

		PersonWithAddress? actual = await this.serializer.DeserializeAsync<PersonWithAddress>(stream);

		// PersonWithAddress.Tags is a List<string>, which does not override Equals: two distinct lists with
		// identical contents are unequal via the record's auto-generated (reference-based) equality, so the
		// scalar and list members are asserted separately here rather than via whole-record equality.
		await Assert.That(actual).IsNotNull();
		await Assert.That(actual!.Name).IsEqualTo(original.Name);
		await Assert.That(actual.Address).IsEqualTo(original.Address);
		await Assert.That(actual.Tags.SequenceEqual(original.Tags)).IsTrue();
	}

	[Test]
	public async Task DeserializeAsync_PipeReader_ToleratesSegmentedWrites()
	{
		Person original = new("Ada", "Extra padding to force the value across several small writes.");
		byte[] data = this.serializer.SerializeToUtf8Bytes(original);
		Pipe pipe = new();

		Task writeTask = Task.Run(async () =>
		{
			foreach (byte[] chunk in Chunk(data, 3))
			{
				await pipe.Writer.WriteAsync(chunk);
				await Task.Delay(1);
			}

			await pipe.Writer.CompleteAsync();
		});

		Person? actual = await this.serializer.DeserializeAsync<Person>(pipe.Reader);
		await writeTask;

		await Assert.That(actual).IsEqualTo(original);
	}

	[Test]
	public async Task DeserializeAsync_Stream_PreCanceledToken_ThrowsOperationCanceledException()
	{
		byte[] data = this.serializer.SerializeToUtf8Bytes(new Person("Ada"));
		using MemoryStream stream = new(data);
		using CancellationTokenSource cts = new();
		await cts.CancelAsync();

		Func<Task> act = () => this.serializer.DeserializeAsync<Person>(stream, cancellationToken: cts.Token).AsTask();

		await Assert.That(act).Throws<OperationCanceledException>();
	}

	[Test]
	public async Task SerializeAsync_Stream_PreCanceledToken_ThrowsOperationCanceledException()
	{
		using MemoryStream stream = new();
		using CancellationTokenSource cts = new();
		await cts.CancelAsync();

		Func<Task> act = () => this.serializer.SerializeAsync(stream, new Person("Ada"), cts.Token).AsTask();

		await Assert.That(act).Throws<OperationCanceledException>();
	}

	[Test]
	public async Task DeserializeAsync_PipeReader_CancellationWhileWaitingForData_ThrowsOperationCanceledException()
	{
		Pipe pipe = new();
		using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(200));

		// Nothing is ever written to pipe.Writer, so this can only complete via cancellation.
		Func<Task> act = () => this.serializer.DeserializeAsync<Person>(pipe.Reader, cancellationToken: cts.Token).AsTask();

		await Assert.That(act).Throws<OperationCanceledException>();
	}

	[Test]
	public async Task DeserializeAllAsync_PipeReader_CancellationWhileWaitingForData_ThrowsOperationCanceledException()
	{
		Pipe pipe = new();
		using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(200));

		Func<Task> act = async () =>
		{
			await foreach (Person? unused in this.serializer.DeserializeAllAsync<Person>(pipe.Reader, cancellationToken: cts.Token))
			{
				_ = unused;
			}
		};

		await Assert.That(act).Throws<OperationCanceledException>();
	}

	[Test]
	public async Task DeserializeAsync_EmptyInput_ThrowsDecoderException()
	{
		using MemoryStream stream = new(Array.Empty<byte>());

		Func<Task> act = () => this.serializer.DeserializeAsync<Person>(stream).AsTask();

		await Assert.That(act).Throws<DecoderException>();
	}

	[Test]
	public async Task DeserializeAllAsync_EmptyInput_YieldsNoValues()
	{
		using MemoryStream stream = new(Array.Empty<byte>());
		PipeReader reader = PipeReader.Create(stream);

		List<Person?> items = [];
		await foreach (Person? item in this.serializer.DeserializeAllAsync<Person>(reader))
		{
			items.Add(item);
		}

		await Assert.That(items.Count).IsEqualTo(0);
	}

	[Test]
	public async Task DeserializeAllAsync_ConcatenatedTopLevelValues_EnumeratesAll()
	{
		// Adjacent JSON objects need no separator to be unambiguous, unlike adjacent bare numbers.
		string json = """{"Name":"Ada"}{"Name":"Bob"}{"Name":"Cid"}""";
		using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));
		PipeReader reader = PipeReader.Create(stream);

		List<Person?> items = [];
		await foreach (Person? item in this.serializer.DeserializeAllAsync<Person>(reader))
		{
			items.Add(item);
		}

		await Assert.That(items.SequenceEqual([new Person("Ada"), new Person("Bob"), new Person("Cid")])).IsTrue();
	}

	[Test]
	public async Task DeserializeAllAsync_NewlineDelimitedJson_EnumeratesAll()
	{
		string ndjson = "{\"Name\":\"Ada\"}\n{\"Name\":\"Bob\"}\n";
		using MemoryStream stream = new(Encoding.UTF8.GetBytes(ndjson));
		PipeReader reader = PipeReader.Create(stream);

		List<Person?> items = [];
		await foreach (Person? item in this.serializer.DeserializeAllAsync<Person>(reader))
		{
			items.Add(item);
		}

		await Assert.That(items.SequenceEqual([new Person("Ada"), new Person("Bob")])).IsTrue();
	}

	[Test]
	public async Task DeserializeAsync_LargePayload_RoundTripsCorrectly()
	{
		LargePayload original = new(new string('x', 500_000), [.. Enumerable.Range(0, 10_000)]);
		using MemoryStream stream = new();

		await this.serializer.SerializeAsync(stream, original);
		stream.Position = 0;

		LargePayload? actual = await this.serializer.DeserializeAsync<LargePayload>(stream);

		// LargePayload.Numbers is a List<int>, which does not override Equals: two distinct lists with identical
		// contents are unequal via the record's auto-generated (reference-based) equality, so the scalar and
		// list members are asserted separately here rather than via whole-record equality.
		await Assert.That(actual).IsNotNull();
		await Assert.That(actual!.Text).IsEqualTo(original.Text);
		await Assert.That(actual.Numbers.SequenceEqual(original.Numbers)).IsTrue();
	}

	[Test]
	public async Task DeserializeAsync_ValueExceedsMaxBufferedSize_ThrowsShapeShiftSerializationException()
	{
		LargePayload original = new(new string('x', 500_000), []);
		byte[] data = this.serializer.SerializeToUtf8Bytes(original);
		using MemoryStream stream = new(data);

		Func<Task> act = () => this.serializer.DeserializeAsync<LargePayload>(stream, maxBufferedSize: 1024).AsTask();

		await Assert.That(act).Throws<ShapeShiftSerializationException>();
	}

	[Test]
	public async Task DeserializeAsync_ValueWithinMaxBufferedSize_Succeeds()
	{
		Person original = new("Ada");
		byte[] data = this.serializer.SerializeToUtf8Bytes(original);
		using MemoryStream stream = new(data);

		Person? actual = await this.serializer.DeserializeAsync<Person>(stream, maxBufferedSize: 1024);

		await Assert.That(actual).IsEqualTo(original);
	}

	/// <summary>
	/// Splits <paramref name="data"/> into consecutive chunks of at most <paramref name="chunkSize"/> bytes each.
	/// </summary>
	private static IEnumerable<byte[]> Chunk(byte[] data, int chunkSize)
	{
		for (int i = 0; i < data.Length; i += chunkSize)
		{
			int length = Math.Min(chunkSize, data.Length - i);
			yield return data[i..(i + length)];
		}
	}

	[GenerateShape]
	internal partial record Person(string Name, string? Padding = null);

	[GenerateShape]
	internal partial record Address(string City, string Zip);

	[GenerateShape]
	internal partial record PersonWithAddress(string Name, Address? Address, List<string> Tags);

	[GenerateShape]
	internal partial record LargePayload(string Text, List<int> Numbers);

	[GenerateShapeFor<string>]
	private partial class Witness;
}
