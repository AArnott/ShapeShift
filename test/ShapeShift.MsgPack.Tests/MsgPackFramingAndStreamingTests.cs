// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using ShapeShift.Tests;

namespace ShapeShift.MsgPack.Tests;

/// <summary>
/// Verifies endless top-level streaming in both directions, the length-prefixed framing helpers (including their
/// rejection of truncated and oversize frames), and targeted path deserialization over an asynchronous source.
/// </summary>
public partial class MsgPackFramingAndStreamingTests : TestBase
{
	private readonly MsgPackSerializer serializer = new();

	[Test]
	public async Task SerializeAllAsync_DeserializeAllAsync_PipeRoundTrip()
	{
		Person[] values = [new("Ada"), new("Bob"), new("Cid")];
		Pipe pipe = new();

		await this.serializer.SerializeAllAsync(pipe.Writer, values);
		await pipe.Writer.CompleteAsync();

		List<Person?> actual = [];
		await foreach (Person? value in this.serializer.DeserializeAllAsync<Person>(pipe.Reader))
		{
			actual.Add(value);
		}

		await Assert.That(actual.SequenceEqual(values)).IsTrue();
	}

	[Test]
	public async Task SerializeAllAsync_DeserializeAllAsync_StreamRoundTrip()
	{
		using MemoryStream stream = new();

		await this.serializer.SerializeAllAsync(stream, Produce());
		stream.Position = 0;

		List<Person?> actual = [];
		await foreach (Person? value in this.serializer.DeserializeAllAsync<Person>(stream))
		{
			actual.Add(value);
		}

		await Assert.That(actual.Select(p => p!.Name).SequenceEqual(["0", "1", "2", "3", "4"])).IsTrue();

		static async IAsyncEnumerable<Person?> Produce()
		{
			for (int i = 0; i < 5; i++)
			{
				await Task.Yield();
				yield return new Person(i.ToString(System.Globalization.CultureInfo.InvariantCulture));
			}
		}
	}

	[Test]
	public async Task DeserializeAllAsync_Stream_EndsGracefullyOnEmptyInput()
	{
		using MemoryStream stream = new();

		int count = 0;
		await foreach (Person? ignored in this.serializer.DeserializeAllAsync<Person>(stream))
		{
			count++;
		}

		await Assert.That(count).IsEqualTo(0);
	}

	[Test]
	public async Task DeserializeAllAsync_Stream_RejectsATruncatedValue()
	{
		byte[] encoded = this.serializer.Serialize(new Person("Ada"));
		using MemoryStream stream = new(encoded[..^2]);

		async Task Enumerate()
		{
			await foreach (Person? ignored in this.serializer.DeserializeAllAsync<Person>(stream))
			{
			}
		}

		await Assert.That(Enumerate).Throws<DecoderException>();
	}

	[Test]
	public async Task SerializeAllAsync_HonorsCancellation()
	{
		using CancellationTokenSource cts = new();
		Pipe pipe = new();

		async Task Write()
		{
			await this.serializer.SerializeAllAsync(pipe.Writer, Produce(cts), cts.Token);
		}

		await Assert.That(Write).Throws<OperationCanceledException>();

		static async IAsyncEnumerable<Person?> Produce(CancellationTokenSource cts)
		{
			await Task.Yield();
			yield return new Person("Ada");
			cts.Cancel();
			yield return new Person("Bob");
		}
	}

	[Test]
	public async Task Frame_RoundTripsOverAPipe()
	{
		Pipe pipe = new();

		await this.serializer.SerializeFrameAsync(pipe.Writer, new Person("Ada"));
		await pipe.Writer.CompleteAsync();

		Person? actual = await this.serializer.DeserializeFrameAsync<Person>(pipe.Reader);

		await Assert.That(actual).IsEqualTo(new Person("Ada"));
	}

	[Test]
	public async Task Frame_RoundTripsOverAStream()
	{
		using MemoryStream stream = new();

		await this.serializer.SerializeFrameAsync(stream, new Person("Ada"));
		stream.Position = 0;

		Person? actual = await this.serializer.DeserializeFrameAsync<Person>(stream);

		await Assert.That(actual).IsEqualTo(new Person("Ada"));
	}

	[Test]
	public async Task Frame_StartsWithABigEndianLengthPrefix()
	{
		using MemoryStream stream = new();
		await this.serializer.SerializeFrameAsync(stream, new Person("Ada"));

		byte[] framed = stream.ToArray();
		byte[] unframed = this.serializer.Serialize(new Person("Ada"));

		await Assert.That(framed.Length).IsEqualTo(unframed.Length + MsgPackFraming.LengthPrefixByteCount);
		await Assert.That((int)BinaryPrimitives.ReadUInt32BigEndian(framed)).IsEqualTo(unframed.Length);
		await Assert.That(framed.AsSpan(MsgPackFraming.LengthPrefixByteCount).SequenceEqual(unframed)).IsTrue();
	}

	[Test]
	public async Task Frames_StreamEndlessly()
	{
		using MemoryStream stream = new();
		for (int i = 0; i < 20; i++)
		{
			await this.serializer.SerializeFrameAsync(stream, new Person(i.ToString(System.Globalization.CultureInfo.InvariantCulture)));
		}

		stream.Position = 0;

		List<string?> names = [];
		await foreach (Person? value in this.serializer.DeserializeAllFramesAsync<Person>(stream))
		{
			names.Add(value?.Name);
		}

		await Assert.That(names.Count).IsEqualTo(20);
		await Assert.That(names[19]).IsEqualTo("19");
	}

	[Test]
	public async Task Frames_EndGracefullyOnEmptyInput()
	{
		Pipe pipe = new();
		await pipe.Writer.CompleteAsync();

		int count = 0;
		await foreach (Person? ignored in this.serializer.DeserializeAllFramesAsync<Person>(pipe.Reader))
		{
			count++;
		}

		await Assert.That(count).IsEqualTo(0);
	}

	[Test]
	public async Task Frame_ArrivingOneByteAtATime_IsStillRead()
	{
		byte[] framed = await this.CreateFrameAsync(new Person("Ada"));
		Pipe pipe = new();

		Task writer = Task.Run(async () =>
		{
			foreach (byte b in framed)
			{
				await pipe.Writer.WriteAsync(new byte[] { b });
			}

			await pipe.Writer.CompleteAsync();
		});

		Person? actual = await this.serializer.DeserializeFrameAsync<Person>(pipe.Reader);
		await writer;

		await Assert.That(actual).IsEqualTo(new Person("Ada"));
	}

	[Test]
	public async Task TruncatedFramePayload_IsRejected()
	{
		byte[] framed = await this.CreateFrameAsync(new Person("Ada"));
		using MemoryStream stream = new(framed[..^1]);

		Func<Task> read = async () => await this.serializer.DeserializeFrameAsync<Person>(stream);

		DecoderException? caught = null;
		try
		{
			await read();
		}
		catch (DecoderException ex)
		{
			caught = ex;
		}

		await Assert.That(caught).IsNotNull();
		await Assert.That(caught!.Message).Contains("middle of a MessagePack frame");
	}

	[Test]
	public async Task TruncatedFramePrefix_IsRejected()
	{
		using MemoryStream stream = new([0, 0]);

		DecoderException? caught = null;
		try
		{
			await this.serializer.DeserializeFrameAsync<Person>(stream);
		}
		catch (DecoderException ex)
		{
			caught = ex;
		}

		await Assert.That(caught).IsNotNull();
	}

	[Test]
	public async Task MissingFrame_IsRejected()
	{
		using MemoryStream stream = new();

		DecoderException? caught = null;
		try
		{
			await this.serializer.DeserializeFrameAsync<Person>(stream);
		}
		catch (DecoderException ex)
		{
			caught = ex;
		}

		await Assert.That(caught).IsNotNull();
		await Assert.That(caught!.Message).Contains("did not contain a MessagePack frame");
	}

	[Test]
	public async Task OversizeFrame_IsRejectedBeforeItIsBuffered()
	{
		// A length prefix claiming 1 GiB, followed by nothing at all. A reader that buffered first would wait
		// (and allocate) forever; the limit is checked against the prefix alone.
		byte[] prefixOnly = [0x40, 0x00, 0x00, 0x00];
		using MemoryStream stream = new(prefixOnly);

		ShapeShiftSerializationException? caught = null;
		try
		{
			await this.serializer.DeserializeFrameAsync<Person>(stream, maxFrameLength: 1024);
		}
		catch (ShapeShiftSerializationException ex)
		{
			caught = ex;
		}

		await Assert.That(caught).IsNotNull();
		await Assert.That(caught!.Message).Contains("exceeds the maximum");
	}

	[Test]
	public async Task OversizeFrame_IsRejectedInTheMiddleOfAStream()
	{
		using MemoryStream stream = new();
		await this.serializer.SerializeFrameAsync(stream, new Person("Ada"));
		stream.Write([0x00, 0x10, 0x00, 0x00]);
		stream.Position = 0;

		async Task Enumerate()
		{
			await foreach (Person? ignored in this.serializer.DeserializeAllFramesAsync<Person>(stream, maxFrameLength: 64))
			{
			}
		}

		await Assert.That(Enumerate).Throws<ShapeShiftSerializationException>();
	}

	[Test]
	public async Task FrameCarryingMoreThanOneValue_IsRejected()
	{
		byte[] two = [.. this.serializer.Serialize(new Person("Ada")), .. this.serializer.Serialize(new Person("Bob"))];
		byte[] framed = new byte[MsgPackFraming.LengthPrefixByteCount + two.Length];
		BinaryPrimitives.WriteUInt32BigEndian(framed, (uint)two.Length);
		two.CopyTo(framed.AsSpan(MsgPackFraming.LengthPrefixByteCount));
		using MemoryStream stream = new(framed);

		Func<Task> read = async () => await this.serializer.DeserializeFrameAsync<Person>(stream);

		await Assert.That(read).Throws<DecoderException>();
	}

	[Test]
	public async Task Frames_HonorCancellation()
	{
		using CancellationTokenSource cts = new();
		Pipe pipe = new();
		await cts.CancelAsync();

		Func<Task> read = async () => await this.serializer.DeserializeFrameAsync<Person>(pipe.Reader, MsgPackFraming.DefaultMaxFrameLength, cts.Token);

		await Assert.That(read).Throws<OperationCanceledException>();
	}

	[Test]
	public async Task TryDeserializeFragmentAsync_FindsANestedValue()
	{
		using MemoryStream stream = new();
		await this.serializer.SerializeAsync(stream, new Document(new Person("Ada"), [1, 2, 3]));
		stream.Position = 0;

		(bool found, string? name) = await this.serializer.TryDeserializeFragmentAsync<string, Witness>(stream, new ShapeShiftPath("Owner", "Name"));

		await Assert.That(found).IsTrue();
		await Assert.That(name).IsEqualTo("Ada");
	}

	[Test]
	public async Task TryDeserializeFragmentAsync_ReportsAMissingPath()
	{
		Pipe pipe = new();
		await this.serializer.SerializeAsync(pipe.Writer, new Document(new Person("Ada"), []));
		await pipe.Writer.CompleteAsync();

		(bool found, string? name) = await this.serializer.TryDeserializeFragmentAsync<string, Witness>(pipe.Reader, new ShapeShiftPath("Missing"));

		await Assert.That(found).IsFalse();
		await Assert.That(name).IsNull();
	}

	[Test]
	public async Task TryDeserializeFragmentAsync_ReadsOnlyTheEnclosingValue()
	{
		// Two values back to back: the fragment read must consume exactly the first, leaving the second intact.
		Pipe pipe = new();
		await this.serializer.SerializeAsync(pipe.Writer, new Document(new Person("Ada"), []));
		await this.serializer.SerializeAsync(pipe.Writer, new Document(new Person("Bob"), []));
		await pipe.Writer.CompleteAsync();

		(bool found, string? first) = await this.serializer.TryDeserializeFragmentAsync<string, Witness>(pipe.Reader, new ShapeShiftPath("Owner", "Name"));
		Document? second = await this.serializer.DeserializeAsync<Document>(pipe.Reader);

		await Assert.That(found).IsTrue();
		await Assert.That(first).IsEqualTo("Ada");
		await Assert.That(second!.Owner.Name).IsEqualTo("Bob");
	}

	[Test]
	public async Task TryDeserializeFragmentAsync_RejectsAnEmptyInput()
	{
		using MemoryStream stream = new();

		Func<Task> read = async () => await this.serializer.TryDeserializeFragmentAsync<string, Witness>(stream, new ShapeShiftPath("Owner"));

		await Assert.That(read).Throws<DecoderException>();
	}

	[Test]
	public async Task TryDeserializeFragmentAsync_HonorsTheBufferLimit()
	{
		using MemoryStream stream = new();
		await this.serializer.SerializeAsync(stream, new Document(new Person("Ada"), [.. Enumerable.Range(0, 5000)]));
		stream.Position = 0;

		Func<Task> read = async () => await this.serializer.TryDeserializeFragmentAsync<string, Witness>(stream, new ShapeShiftPath("Owner", "Name"), maxBufferedSize: 64);

		await Assert.That(read).Throws<ShapeShiftSerializationException>();
	}

	private async Task<byte[]> CreateFrameAsync<T>(T value)
		where T : IShapeable<T>
	{
		using MemoryStream stream = new();
		await this.serializer.SerializeFrameAsync(stream, value);
		return stream.ToArray();
	}

	[GenerateShape]
	internal partial record Person(string Name);

	[GenerateShape]
	internal partial record Document(Person Owner, List<int> Values);

	[GenerateShapeFor<string>]
	private partial class Witness;
}
