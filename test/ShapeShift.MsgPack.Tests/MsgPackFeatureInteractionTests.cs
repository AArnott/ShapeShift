// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using ShapeShift.Tests;

namespace ShapeShift.MsgPack.Tests;

/// <summary>
/// Verifies how the MessagePack features interact with each other: positional contracts combined with reference
/// preservation, with segmented input, and with caller-supplied converter factories.
/// </summary>
public partial class MsgPackFeatureInteractionTests : TestBase
{
	private readonly MsgPackSerializer serializer = new();

	[Test]
	public async Task CustomConverterFactory_DoesNotDisablePositionalContracts()
	{
		MsgPackSerializer extended = this.serializer with { ConverterFactories = [new NeverMatchesFactory()] };

		byte[] encoded = extended.Serialize(new Point(1, 2));

		await Assert.That(encoded[0]).IsEqualTo((byte)0x92);
		await Assert.That(extended.Deserialize<Point>(encoded)).IsEqualTo(new Point(1, 2));
	}

	[Test]
	public async Task CustomConverterFactory_TakesPrecedence()
	{
		MsgPackSerializer extended = this.serializer with { ConverterFactories = [new PointAsStringFactory()] };

		byte[] encoded = extended.Serialize(new Point(1, 2));

		await Assert.That(encoded[0]).IsEqualTo((byte)0xa3);
		await Assert.That(extended.Deserialize<Point>(encoded)).IsEqualTo(new Point(1, 2));
	}

	[Test]
	public async Task ConverterFactories_AreNotDuplicatedByRoundTrippingThem()
	{
		MsgPackSerializer extended = this.serializer with { ConverterFactories = [.. this.serializer.ConverterFactories, new NeverMatchesFactory()] };

		await Assert.That(extended.ConverterFactories.Length).IsEqualTo(2);
		await Assert.That(extended.Serialize(new Point(1, 2))[0]).IsEqualTo((byte)0x92);
	}

	[Test]
	public async Task PositionalContract_PreservesReferences()
	{
		MsgPackSerializer preserving = this.serializer with { PreserveReferences = ReferencePreservationMode.RejectCycles };
		Point shared = new(1, 2);

		Segment? actual = preserving.Deserialize<Segment>(preserving.Serialize(new Segment(shared, shared)));

		await Assert.That(ReferenceEquals(actual!.Start, actual.End)).IsTrue();
		await Assert.That(actual.Start).IsEqualTo(shared);
	}

	[Test]
	public async Task PositionalContract_RoundTripsThroughACycle()
	{
		MsgPackSerializer cyclic = this.serializer with { PreserveReferences = ReferencePreservationMode.AllowCycles };
		Chain head = new() { Name = "head" };
		head.Next = head;

		Chain? actual = cyclic.Deserialize<Chain>(cyclic.Serialize(head));

		await Assert.That(actual!.Name).IsEqualTo("head");
		await Assert.That(ReferenceEquals(actual, actual.Next)).IsTrue();
	}

	[Test]
	[Arguments(1)]
	[Arguments(3)]
	public async Task PositionalContract_ReadsSegmentedInput(int segmentSize)
	{
		byte[] encoded = this.serializer.Serialize(new Segment(new Point(1, 2), new Point(3, 4)));

		Segment? actual = this.serializer.Deserialize<Segment>(Chop(encoded, segmentSize));

		await Assert.That(actual!.Start).IsEqualTo(new Point(1, 2));
		await Assert.That(actual.End).IsEqualTo(new Point(3, 4));
	}

	[Test]
	public async Task PositionalContract_StreamsOverAPipe()
	{
		using MemoryStream stream = new();
		await this.serializer.SerializeAllAsync(stream, new[] { new Point(1, 2), new Point(3, 4) });
		stream.Position = 0;

		List<Point?> points = [];
		await foreach (Point? point in this.serializer.DeserializeAllAsync<Point>(stream))
		{
			points.Add(point);
		}

		await Assert.That(points.SequenceEqual([new Point(1, 2), new Point(3, 4)])).IsTrue();
	}

	[Test]
	public async Task PositionalContract_SupportsTargetedReadsByIndex()
	{
		byte[] encoded = this.serializer.Serialize(new Segment(new Point(1, 2), new Point(3, 4)));

		bool found = this.serializer.TryDeserializeFragment<int, Witness>(encoded, new ShapeShiftPath(1, 0), out int x);

		await Assert.That(found).IsTrue();
		await Assert.That(x).IsEqualTo(3);
	}

	private static ReadOnlySequence<byte> Chop(byte[] bytes, int segmentSize)
	{
		BufferSegment first = new(bytes.AsMemory(0, Math.Min(segmentSize, bytes.Length)));
		BufferSegment last = first;
		for (int start = segmentSize; start < bytes.Length; start += segmentSize)
		{
			last = last.Append(bytes.AsMemory(start, Math.Min(segmentSize, bytes.Length - start)));
		}

		return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
	}

	[GenerateShape]
	[MsgPackArrayContract]
	internal partial record Point([property: MsgPackKey(0)] int X, [property: MsgPackKey(1)] int Y);

	[GenerateShape]
	[MsgPackArrayContract]
	internal partial record Segment([property: MsgPackKey(0)] Point Start, [property: MsgPackKey(1)] Point End);

	[GenerateShape]
	[MsgPackArrayContract]
	internal partial class Chain
	{
		[MsgPackKey(0)]
		public string? Name { get; set; }

		[MsgPackKey(1)]
		public Chain? Next { get; set; }
	}

	[GenerateShapeFor<int>]
	private partial class Witness;

	/// <summary>
	/// A factory that never claims a type, standing in for a caller's own factory that happens not to apply.
	/// </summary>
	private sealed class NeverMatchesFactory : IShapeShiftConverterFactory<MsgPackEncoder, MsgPackDecoder>
	{
		public ShapeShiftConverter<MsgPackEncoder, MsgPackDecoder>? CreateConverter(Type type, ITypeShape? shape, in ConverterContext<MsgPackEncoder, MsgPackDecoder> context) => null;
	}

	/// <summary>
	/// A factory that claims <see cref="Point"/>, to prove a caller's factory outranks the built-in positional one.
	/// </summary>
	private sealed class PointAsStringFactory : IShapeShiftConverterFactory<MsgPackEncoder, MsgPackDecoder>
	{
		public ShapeShiftConverter<MsgPackEncoder, MsgPackDecoder>? CreateConverter(Type type, ITypeShape? shape, in ConverterContext<MsgPackEncoder, MsgPackDecoder> context)
			=> type == typeof(Point) ? new PointAsStringConverter() : null;
	}

	private sealed class PointAsStringConverter : ShapeShiftConverter<Point, MsgPackEncoder, MsgPackDecoder>
	{
		public override Point? Read(ref MsgPackDecoder decoder, SerializationContext<MsgPackEncoder, MsgPackDecoder> context)
		{
			string[] parts = decoder.ReadString().Split(',');
			return new Point(int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
		}

		public override void Write(ref MsgPackEncoder encoder, in Point? value, SerializationContext<MsgPackEncoder, MsgPackDecoder> context)
			=> encoder.WriteValue($"{value!.X},{value.Y}");
	}

	private sealed class BufferSegment : ReadOnlySequenceSegment<byte>
	{
		internal BufferSegment(ReadOnlyMemory<byte> memory)
		{
			this.Memory = memory;
		}

		internal BufferSegment Append(ReadOnlyMemory<byte> memory)
		{
			BufferSegment segment = new(memory) { RunningIndex = this.RunningIndex + this.Memory.Length };
			this.Next = segment;
			return segment;
		}
	}
}
