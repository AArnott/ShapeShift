// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Conformance.Suites;

/// <summary>
/// Helpers that let a single case body work for formats that do and do not allow a scalar
/// to stand alone as a whole document.
/// </summary>
internal static class ScalarHarness
{
	/// <summary>
	/// Encodes one scalar as a whole document, wrapping it in a single-entry map when the format
	/// cannot represent a bare scalar document.
	/// </summary>
	/// <typeparam name="TEncoder">The format's encoder type.</typeparam>
	/// <typeparam name="TDecoder">The format's decoder type.</typeparam>
	/// <param name="adapter">The adapter under test.</param>
	/// <param name="writeValue">Writes the scalar.</param>
	/// <returns>The payload.</returns>
	internal static byte[] Encode<TEncoder, TDecoder>(FormatConformanceAdapter<TEncoder, TDecoder> adapter, EncodeAction<TEncoder> writeValue)
		where TEncoder : IEncoder, allows ref struct
		where TDecoder : IDecoder, allows ref struct
	{
		if (adapter.Options.SupportsRootScalars)
		{
			return adapter.Encode(writeValue);
		}

		return adapter.Encode((ref TEncoder encoder) =>
		{
			encoder.WriteStartMap(1);
			encoder.WritePropertyName("value");
			writeValue(ref encoder);
			encoder.WriteEndMap();
		});
	}

	/// <summary>
	/// Decodes a payload produced by <see cref="Encode"/>, positioning the decoder at the scalar first.
	/// </summary>
	/// <typeparam name="TEncoder">The format's encoder type.</typeparam>
	/// <typeparam name="TDecoder">The format's decoder type.</typeparam>
	/// <typeparam name="TResult">The type of value the callback produces.</typeparam>
	/// <param name="adapter">The adapter under test.</param>
	/// <param name="payload">The payload to decode.</param>
	/// <param name="readValue">Reads the scalar. It need not consume the scalar, and never needs to close the wrapper.</param>
	/// <returns>Whatever <paramref name="readValue"/> returned.</returns>
	internal static TResult Decode<TEncoder, TDecoder, TResult>(FormatConformanceAdapter<TEncoder, TDecoder> adapter, ReadOnlyMemory<byte> payload, DecodeFunc<TDecoder, TResult> readValue)
		where TEncoder : IEncoder, allows ref struct
		where TDecoder : IDecoder, allows ref struct
	{
		if (adapter.Options.SupportsRootScalars)
		{
			return adapter.Decode(payload, readValue);
		}

		return adapter.Decode(payload, (ref TDecoder decoder) =>
		{
			decoder.ReadStartMap();
			_ = decoder.ReadPropertyName();
			return readValue(ref decoder);
		});
	}

	/// <summary>
	/// Round-trips one scalar through the format.
	/// </summary>
	/// <typeparam name="TEncoder">The format's encoder type.</typeparam>
	/// <typeparam name="TDecoder">The format's decoder type.</typeparam>
	/// <typeparam name="TValue">The type of scalar.</typeparam>
	/// <param name="adapter">The adapter under test.</param>
	/// <param name="writeValue">Writes the scalar.</param>
	/// <param name="readValue">Reads the scalar.</param>
	/// <returns>The value that survived the round trip.</returns>
	internal static TValue Roundtrip<TEncoder, TDecoder, TValue>(
		FormatConformanceAdapter<TEncoder, TDecoder> adapter,
		EncodeAction<TEncoder> writeValue,
		DecodeFunc<TDecoder, TValue> readValue)
		where TEncoder : IEncoder, allows ref struct
		where TDecoder : IDecoder, allows ref struct
		=> Decode<TEncoder, TDecoder, TValue>(adapter, Encode(adapter, writeValue), readValue);
}
