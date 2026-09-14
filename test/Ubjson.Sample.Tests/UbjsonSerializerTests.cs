// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using ShapeShift;

namespace Ubjson.Tests;

/// <summary>
/// Verifies the public surface of the UBJSON sample format package.
/// </summary>
public partial class UbjsonSerializerTests
{
	/// <summary>
	/// Verifies that an object round-trips through the format's own serializer entry points.
	/// </summary>
	/// <returns>A task tracking the assertions.</returns>
	[Test]
	public async Task ObjectRoundtrips()
	{
		UbjsonSerializer serializer = new();
		Reading original = new("pressure", 101.325m, [1, 2, 3], new byte[] { 0xDE, 0xAD });

		Reading? roundtripped = serializer.Deserialize<Reading>(serializer.Serialize(original));

		await Assert.That(roundtripped).IsNotNull();
		await Assert.That(roundtripped!.Name).IsEqualTo(original.Name);
		await Assert.That(roundtripped.Value).IsEqualTo(original.Value);
		await Assert.That(roundtripped.Samples.SequenceEqual(original.Samples)).IsTrue();
		await Assert.That(Convert.ToHexString(roundtripped.Signature!)).IsEqualTo(Convert.ToHexString(original.Signature!));
	}

	/// <summary>
	/// Verifies that a byte array uses the optimized UBJSON <c>uint8</c> array form.
	/// </summary>
	/// <returns>A task tracking the assertion.</returns>
	[Test]
	public async Task BinaryUsesOptimizedArrayForm()
	{
		ArrayBufferWriter<byte> buffer = new();
		UbjsonEncoder encoder = new(buffer);
		encoder.WriteValue(new byte[] { 1, 2, 3 }.AsSpan());

		await Assert.That(Convert.ToHexString(buffer.WrittenSpan))
			.IsEqualTo(Convert.ToHexString(new byte[] { (byte)'[', (byte)'$', (byte)'U', (byte)'#', (byte)'U', 3, 1, 2, 3 }));
	}

	/// <summary>
	/// Verifies that the boundary scanner lets a sequence of concatenated values be read asynchronously.
	/// </summary>
	/// <returns>A task tracking the assertions.</returns>
	[Test]
	public async Task StreamOfValuesIsReadOneAtATime()
	{
		UbjsonSerializer serializer = new();
		byte[] stream =
		[
			.. serializer.Serialize(new Reading("first", 1m, [], null)),
			.. serializer.Serialize(new Reading("second", 2m, [], null)),
		];

		List<string> names = [];
		PipeReader reader = PipeReader.Create(new ReadOnlySequence<byte>(stream));
		await foreach (Reading? reading in serializer.DeserializeAllAsync<Reading>(reader))
		{
			names.Add(reading!.Name);
		}

		await Assert.That(string.Join(", ", names)).IsEqualTo("first, second");
	}

	/// <summary>
	/// Verifies that a truncated payload is rejected as bad input rather than crashing.
	/// </summary>
	/// <returns>A task tracking the assertion.</returns>
	[Test]
	public async Task TruncatedPayloadIsRejectedCleanly()
	{
		UbjsonSerializer serializer = new();
		byte[] payload = serializer.Serialize(new Reading("pressure", 1m, [1, 2, 3], null));
		byte[] truncated = payload.AsSpan(0, payload.Length - 2).ToArray();

		Exception? caught = null;
		try
		{
			serializer.Deserialize<Reading>(truncated);
		}
		catch (Exception ex)
		{
			caught = ex;
		}

		await Assert.That(caught is DecoderException or ShapeShiftSerializationException).IsTrue();
	}

	/// <summary>
	/// Verifies that a property name is written with a length prefix but no type marker, as UBJSON requires.
	/// </summary>
	/// <returns>A task tracking the assertion.</returns>
	[Test]
	public async Task PropertyNamesAreWrittenWithoutATypeMarker()
	{
		ArrayBufferWriter<byte> buffer = new();
		UbjsonEncoder encoder = new(buffer);
		encoder.WriteStartMap(1);
		encoder.WritePropertyName("id");
		encoder.WriteValue(1L);
		encoder.WriteEndMap();

		await Assert.That(Encoding.UTF8.GetString(buffer.WrittenSpan)).IsEqualTo("{U\u0002idU\u0001}");
	}

	/// <summary>
	/// A model that exercises strings, exact decimals, collections, and binary data.
	/// </summary>
	/// <param name="Name">The name of the reading.</param>
	/// <param name="Value">The measured value.</param>
	/// <param name="Samples">The samples the value came from.</param>
	/// <param name="Signature">An optional signature.</param>
	[GenerateShape]
	public partial record Reading(string Name, decimal Value, int[] Samples, byte[]? Signature);
}
