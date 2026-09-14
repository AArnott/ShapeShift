// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Conformance;

/// <summary>
/// Adapts a format's encoder, decoder, and serializer to the conformance suite.
/// </summary>
/// <typeparam name="TEncoder">The format's encoder type.</typeparam>
/// <typeparam name="TDecoder">The format's decoder type.</typeparam>
/// <remarks>
/// <para>
/// A format package implements this once, in its test project, and the conformance suite drives
/// every case through it. Because encoders and decoders are typically <see langword="ref" /> structs,
/// the suite never holds one: it hands the adapter a callback and the adapter creates, drives, and
/// disposes the instance within a single stack frame.
/// </para>
/// <para>
/// Payloads are exchanged as bytes so that binary and text formats can be tested identically.
/// A text format's adapter encodes to and decodes from UTF-8.
/// </para>
/// </remarks>
public abstract class FormatConformanceAdapter<TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <summary>
	/// Gets the display name of the format, used to label the generated test cases.
	/// </summary>
	public abstract string FormatName { get; }

	/// <summary>
	/// Gets the parts of the ShapeShift data model this format can represent.
	/// </summary>
	/// <remarks>The default declares a fully self-describing format.</remarks>
	public virtual FormatConformanceOptions Options => FormatConformanceOptions.Default;

	/// <summary>
	/// Creates a serializer with this format's default configuration.
	/// </summary>
	/// <returns>A new serializer. The suite may derive further configurations from it with <see langword="with" /> expressions.</returns>
	public abstract ShapeShiftSerializer<TEncoder, TDecoder> CreateSerializer();

	/// <summary>
	/// Encodes a payload by running <paramref name="action"/> against a new encoder.
	/// </summary>
	/// <param name="action">Writes the tokens that make up the payload.</param>
	/// <returns>The complete, flushed payload.</returns>
	public abstract byte[] Encode(EncodeAction<TEncoder> action);

	/// <summary>
	/// Decodes a payload by running <paramref name="func"/> against a new decoder positioned at its start.
	/// </summary>
	/// <typeparam name="TResult">The type of value the callback produces.</typeparam>
	/// <param name="payload">The payload to decode. It is typically the result of a prior <see cref="Encode"/> call.</param>
	/// <param name="func">Reads the payload and produces a result.</param>
	/// <returns>Whatever <paramref name="func"/> returned.</returns>
	public abstract TResult Decode<TResult>(ReadOnlyMemory<byte> payload, DecodeFunc<TDecoder, TResult> func);

	/// <summary>
	/// Creates the boundary scanner that backs this format's asynchronous adapters.
	/// </summary>
	/// <returns>
	/// A new scanner, or <see langword="null" /> when the format has no asynchronous adapters,
	/// in which case the scanner cases are skipped.
	/// </returns>
	/// <remarks>
	/// Each call must return a fresh instance, because a scanner is stateful across
	/// <see cref="IValueBoundaryScanner.TryScan"/> calls.
	/// </remarks>
	public virtual IValueBoundaryScanner? CreateValueBoundaryScanner() => null;

	/// <summary>
	/// Gets the token type this format reports for a family of values.
	/// </summary>
	/// <param name="kind">The kind of value that was written.</param>
	/// <returns>The <see cref="TokenType"/> the decoder is expected to report before that value is read.</returns>
	/// <remarks>
	/// The default is the self-describing mapping. Override only for the kinds a text format
	/// cannot distinguish; see <see cref="ConformanceValueKind"/>.
	/// </remarks>
	public virtual TokenType GetExpectedTokenType(ConformanceValueKind kind) => kind switch
	{
		ConformanceValueKind.Null => TokenType.Null,
		ConformanceValueKind.Boolean => TokenType.Boolean,
		ConformanceValueKind.Integer => TokenType.Number,
		ConformanceValueKind.Float => TokenType.Number,
		ConformanceValueKind.String => TokenType.String,
		ConformanceValueKind.Binary => TokenType.Binary,
		ConformanceValueKind.DateTime => TokenType.String,
		ConformanceValueKind.TimeSpan => TokenType.String,
		ConformanceValueKind.Map => TokenType.StartMap,
		ConformanceValueKind.Vector => TokenType.StartVector,
		_ => throw new ArgumentOutOfRangeException(nameof(kind)),
	};

	/// <summary>
	/// Adds format-specific cases to the suite.
	/// </summary>
	/// <param name="collector">The collector the built-in suites have already contributed to.</param>
	/// <remarks>
	/// Override this to reuse the conformance runner for cases that only make sense for one format,
	/// such as an extension type or a framing convention.
	/// </remarks>
	public virtual void AddFormatSpecificTests(ConformanceTestCollector<TEncoder, TDecoder> collector)
	{
	}

	/// <summary>
	/// Decodes a payload with a callback that produces no result.
	/// </summary>
	/// <param name="payload">The payload to decode.</param>
	/// <param name="action">Reads the payload.</param>
	public void Decode(ReadOnlyMemory<byte> payload, DecodeAction<TDecoder> action)
	{
		Requires.NotNull(action);
		this.Decode(payload, (ref TDecoder decoder) =>
		{
			action(ref decoder);
			return 0;
		});
	}

	/// <summary>
	/// Serializes a value with a serializer this adapter's format understands.
	/// </summary>
	/// <typeparam name="T">The type of value to serialize.</typeparam>
	/// <param name="serializer">The serializer to use.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="typeShape">The shape of <typeparamref name="T"/>.</param>
	/// <returns>The encoded payload.</returns>
	public byte[] Serialize<T>(ShapeShiftSerializer<TEncoder, TDecoder> serializer, T? value, ITypeShape<T> typeShape)
	{
		Requires.NotNull(serializer);
		Requires.NotNull(typeShape);
		return this.Encode((ref TEncoder encoder) => serializer.Serialize(ref encoder, value, typeShape));
	}

	/// <summary>
	/// Deserializes a value with a serializer this adapter's format understands.
	/// </summary>
	/// <typeparam name="T">The type of value to deserialize.</typeparam>
	/// <param name="serializer">The serializer to use.</param>
	/// <param name="payload">The payload to deserialize.</param>
	/// <param name="typeShape">The shape of <typeparamref name="T"/>.</param>
	/// <returns>The deserialized value.</returns>
	public T? Deserialize<T>(ShapeShiftSerializer<TEncoder, TDecoder> serializer, ReadOnlyMemory<byte> payload, ITypeShape<T> typeShape)
	{
		Requires.NotNull(serializer);
		Requires.NotNull(typeShape);
		return this.Decode(payload, (ref TDecoder decoder) => serializer.Deserialize(ref decoder, typeShape));
	}

	/// <summary>
	/// Round-trips a value through this format with a given serializer.
	/// </summary>
	/// <typeparam name="T">The type of value to round-trip.</typeparam>
	/// <param name="serializer">The serializer to use.</param>
	/// <param name="value">The value to round-trip.</param>
	/// <param name="typeShape">The shape of <typeparamref name="T"/>.</param>
	/// <returns>The value that survived the round trip.</returns>
	public T? Roundtrip<T>(ShapeShiftSerializer<TEncoder, TDecoder> serializer, T? value, ITypeShape<T> typeShape)
		=> this.Deserialize(serializer, this.Serialize(serializer, value, typeShape), typeShape);
}
