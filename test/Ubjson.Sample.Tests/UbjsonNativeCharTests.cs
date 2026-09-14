// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using ShapeShift;
using ShapeShift.Schema;

namespace Ubjson.Tests;

/// <summary>
/// Verifies the worked example of supporting a primitive whose native representation the shared
/// <see cref="IEncoder"/>/<see cref="IDecoder"/> vocabulary does not expose.
/// </summary>
/// <remarks>
/// UBJSON's <c>C</c> type is one ASCII byte behind a one-byte marker. The shared vocabulary's nearest
/// equivalent is a one-character string, which costs twice as much and says less. The pattern under
/// test is therefore: a format-specific encoder method, a format-specific decoder method, and a
/// converter over the concrete encoder and decoder that the format's serializer registers.
/// </remarks>
public partial class UbjsonNativeCharTests
{
	/// <summary>
	/// Verifies that the format-specific encoder method emits UBJSON's two-byte native form.
	/// </summary>
	/// <returns>A task tracking the assertion.</returns>
	[Test]
	public async Task WriteChar_EmitsTheNativeMarker()
	{
		ArrayBufferWriter<byte> buffer = new();
		UbjsonEncoder encoder = new(buffer);
		encoder.WriteChar('q');

		await Assert.That(Encoding.ASCII.GetString(buffer.WrittenSpan)).IsEqualTo("Cq");
	}

	/// <summary>
	/// Verifies that the registered converter chooses the native form over the shared string form,
	/// which is the whole point of the exercise.
	/// </summary>
	/// <returns>A task tracking the assertions.</returns>
	[Test]
	public async Task Serializer_UsesTheNativeFormAndItIsShorter()
	{
		UbjsonSerializer serializer = new();

		byte[] native = serializer.Serialize<char, Witness>('q');

		// 'C' + 'q'. The shared representation would be 'S' + a length marker + a length + 'q'.
		await Assert.That(Encoding.ASCII.GetString(native)).IsEqualTo("Cq");
		await Assert.That(native.Length).IsLessThan(4);
		await Assert.That(serializer.Deserialize<char, Witness>(native)).IsEqualTo('q');
	}

	/// <summary>
	/// Verifies that a value written by some other UBJSON implementation as an ordinary string still
	/// deserializes, because the format-specific decoder method reports <see langword="false" />
	/// instead of throwing.
	/// </summary>
	/// <returns>A task tracking the assertion.</returns>
	[Test]
	public async Task Deserialize_FallsBackToTheSharedStringForm()
	{
		ArrayBufferWriter<byte> buffer = new();
		UbjsonEncoder encoder = new(buffer);
		encoder.WriteValue("q");

		UbjsonSerializer serializer = new();

		await Assert.That(serializer.Deserialize<char, Witness>(buffer.WrittenSpan)).IsEqualTo('q');
	}

	/// <summary>
	/// Verifies that a character the native form cannot carry falls back to the shared representation
	/// rather than being mangled.
	/// </summary>
	/// <returns>A task tracking the assertions.</returns>
	[Test]
	public async Task NonAsciiChar_FallsBackToTheSharedStringForm()
	{
		UbjsonSerializer serializer = new();

		byte[] payload = serializer.Serialize<char, Witness>('\u00e9');

		await Assert.That(payload[0]).IsEqualTo((byte)'S');
		await Assert.That(serializer.Deserialize<char, Witness>(payload)).IsEqualTo('\u00e9');
	}

	/// <summary>
	/// Verifies that <see cref="UbjsonDecoder.TryReadChar"/> honors the same consume-on-true,
	/// consume-nothing-on-false contract that <see cref="IDecoder.TryReadNull"/> does.
	/// </summary>
	/// <returns>A task tracking the assertions.</returns>
	[Test]
	public async Task TryReadChar_ConsumesOnlyWhenItSucceeds()
	{
		ArrayBufferWriter<byte> buffer = new();
		UbjsonEncoder encoder = new(buffer);
		encoder.WriteStartVector(null);
		encoder.WriteValue("text");
		encoder.WriteChar('x');
		encoder.WriteValue(7L);
		encoder.WriteEndVector();

		UbjsonDecoder decoder = new(buffer.WrittenSpan);
		decoder.ReadStartVector();

		bool falseAnswer = decoder.TryReadChar(out char none);
		string stillThere = decoder.ReadString();
		bool trueAnswer = decoder.TryReadChar(out char taken);
		long following = decoder.ReadInt64();
		decoder.ReadEndVector();

		await Assert.That(falseAnswer).IsFalse();
		await Assert.That(none).IsEqualTo('\0');
		await Assert.That(stillThere).IsEqualTo("text");
		await Assert.That(trueAnswer).IsTrue();
		await Assert.That(taken).IsEqualTo('x');
		await Assert.That(following).IsEqualTo(7L);
	}

	/// <summary>
	/// Verifies that the native form participates in container accounting, so that a length-prefixed
	/// container can still synthesize the end token it never wrote.
	/// </summary>
	/// <returns>A task tracking the assertions.</returns>
	[Test]
	public async Task TryReadChar_KeepsContainerAccounting()
	{
		ArrayBufferWriter<byte> buffer = new();
		UbjsonEncoder encoder = new(buffer);
		encoder.WriteStartVector(null);
		encoder.WriteChar('a');
		encoder.WriteEndVector();

		UbjsonDecoder decoder = new(buffer.WrittenSpan);
		decoder.ReadStartVector();
		bool read = decoder.TryReadChar(out char value);
		TokenType next = decoder.NextTokenType;
		decoder.ReadEndVector();
		TokenType afterVector = decoder.NextTokenType;

		await Assert.That(read).IsTrue();
		await Assert.That(value).IsEqualTo('a');
		await Assert.That(next).IsEqualTo(TokenType.EndVector);
		await Assert.That(afterVector).IsEqualTo(TokenType.EndDocument);
	}

	/// <summary>
	/// Verifies that an exhausted optimized container does not let the format-specific reader run past
	/// the container's end.
	/// </summary>
	/// <returns>A task tracking the assertions.</returns>
	/// <remarks>
	/// A UBJSON optimized container declares its element type once (<c>$C</c>) and its count once
	/// (<c>#i1</c>), so once the count is spent the frame still names <c>C</c> even though no value
	/// follows. Consulting <see cref="UbjsonDecoder.NextTokenType"/> first is what keeps
	/// <see cref="UbjsonDecoder.TryReadChar"/> from consuming the next top-level value's bytes.
	/// </remarks>
	[Test]
	public async Task TryReadChar_StopsAtAnExhaustedTypedContainer()
	{
		// [$C#i1 'a'  followed by a separate top-level null.
		byte[] payload = [(byte)'[', (byte)'$', (byte)'C', (byte)'#', (byte)'i', 1, (byte)'a', (byte)'Z'];

		UbjsonDecoder decoder = new(payload);
		decoder.ReadStartVector();
		bool first = decoder.TryReadChar(out char taken);
		bool second = decoder.TryReadChar(out char beyond);
		TokenType atEnd = decoder.NextTokenType;
		decoder.ReadEndVector();
		TokenType nextValue = decoder.NextTokenType;

		await Assert.That(first).IsTrue();
		await Assert.That(taken).IsEqualTo('a');
		await Assert.That(second).IsFalse();
		await Assert.That(beyond).IsEqualTo('\0');
		await Assert.That(atEnd).IsEqualTo(TokenType.EndVector);
		await Assert.That(nextValue).IsEqualTo(TokenType.Null);
	}

	/// <summary>
	/// Verifies that a key slot in an optimized map is not mistaken for a value of the map's declared
	/// element type.
	/// </summary>
	/// <returns>A task tracking the assertions.</returns>
	[Test]
	public async Task TryReadChar_StopsAtAPropertyName()
	{
		// {$C#i1 <len>"k" 'v'
		byte[] payload = [(byte)'{', (byte)'$', (byte)'C', (byte)'#', (byte)'i', 1, (byte)'U', 1, (byte)'k', (byte)'v'];

		UbjsonDecoder decoder = new(payload);
		decoder.ReadStartMap();
		bool atKey = decoder.TryReadChar(out char keyChar);
		string name = decoder.ReadPropertyName().ToString();
		bool atValue = decoder.TryReadChar(out char valueChar);
		decoder.ReadEndMap();

		await Assert.That(atKey).IsFalse();
		await Assert.That(keyChar).IsEqualTo('\0');
		await Assert.That(name).IsEqualTo("k");
		await Assert.That(atValue).IsTrue();
		await Assert.That(valueChar).IsEqualTo('v');
	}

	/// <summary>
	/// Verifies that the format-specific reader reports <see langword="false" /> at the end of the
	/// input rather than reading past it.
	/// </summary>
	/// <returns>A task tracking the assertion.</returns>
	[Test]
	public async Task TryReadChar_StopsAtEndOfDocument()
	{
		UbjsonDecoder decoder = new(default);

		bool read = decoder.TryReadChar(out char value);

		await Assert.That(read).IsFalse();
		await Assert.That(value).IsEqualTo('\0');
	}

	/// <summary>
	/// Verifies that a <c>C</c> marker carrying a non-ASCII byte is rejected as bad input.
	/// </summary>
	/// <returns>A task tracking the assertion.</returns>
	[Test]
	public async Task NonAsciiPayload_IsRejectedAsBadInput()
	{
		byte[] payload = [(byte)'C', 0xFF];

		Exception? caught = null;
		try
		{
			Decode(payload);
		}
		catch (Exception ex)
		{
			caught = ex;
		}

		await Assert.That(caught).IsTypeOf<DecoderException>();

		static void Decode(ReadOnlySpan<byte> bytes)
		{
			UbjsonDecoder decoder = new(bytes);
			decoder.TryReadChar(out _);
		}
	}

	/// <summary>
	/// Verifies that the format-specific converter describes its own wire type to the schema layer
	/// instead of leaving it undocumented.
	/// </summary>
	/// <returns>A task tracking the assertions.</returns>
	[Test]
	public async Task Contract_DescribesTheNativeType()
	{
		UbjsonSerializer serializer = new();

		DataContract contract = serializer.GetContract<char, Witness>();

		await Assert.That(contract).IsTypeOf<PrimitiveContract>();
		await Assert.That(((PrimitiveContract)contract).PrimitiveType).IsEqualTo(PrimitiveDataType.Char);
	}

	/// <summary>
	/// Provides the shape for <see cref="char"/>, which has no shape of its own to generate.
	/// </summary>
	[GenerateShapeFor<char>]
	internal partial class Witness;
}
