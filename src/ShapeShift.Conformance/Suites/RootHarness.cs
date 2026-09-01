// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Conformance.Suites;

/// <summary>
/// Lets one case body serve both formats that accept any value as a whole document and formats whose
/// document must be a map.
/// </summary>
/// <remarks>
/// An indentation-based format generally carries a map at the root, because a document that is a bare
/// list of lines cannot be told apart from a single scalar. Rather than skipping every case whose root
/// value happens to be a scalar or a vector, the suite writes such a value under a single well-known
/// key and steps back into it when reading. What the case is actually testing -- the value's own tokens
/// -- is unchanged.
/// </remarks>
internal static class RootHarness
{
	private const string WrapperKey = "value";

	/// <summary>
	/// Encodes one scalar as a whole document, wrapping it when the format requires a map at the root.
	/// </summary>
	/// <typeparam name="TEncoder">The format's encoder type.</typeparam>
	/// <typeparam name="TDecoder">The format's decoder type.</typeparam>
	/// <param name="adapter">The adapter under test.</param>
	/// <param name="writeValue">Writes the scalar.</param>
	/// <returns>The payload.</returns>
	internal static byte[] EncodeScalar<TEncoder, TDecoder>(FormatConformanceAdapter<TEncoder, TDecoder> adapter, EncodeAction<TEncoder> writeValue)
		where TEncoder : IEncoder, allows ref struct
		where TDecoder : IDecoder, allows ref struct
		=> Encode(adapter, adapter.Options.SupportsRootScalars, writeValue);

	/// <summary>
	/// Encodes one vector as a whole document, wrapping it when the format requires a map at the root.
	/// </summary>
	/// <typeparam name="TEncoder">The format's encoder type.</typeparam>
	/// <typeparam name="TDecoder">The format's decoder type.</typeparam>
	/// <param name="adapter">The adapter under test.</param>
	/// <param name="writeValue">Writes the vector, including its start and end tokens.</param>
	/// <returns>The payload.</returns>
	internal static byte[] EncodeVector<TEncoder, TDecoder>(FormatConformanceAdapter<TEncoder, TDecoder> adapter, EncodeAction<TEncoder> writeValue)
		where TEncoder : IEncoder, allows ref struct
		where TDecoder : IDecoder, allows ref struct
		=> Encode(adapter, adapter.Options.SupportsRootVectors, writeValue);

	/// <summary>
	/// Decodes a payload produced by <see cref="EncodeScalar"/>, positioning the decoder at the scalar.
	/// </summary>
	/// <typeparam name="TEncoder">The format's encoder type.</typeparam>
	/// <typeparam name="TDecoder">The format's decoder type.</typeparam>
	/// <typeparam name="TResult">The type of value the callback produces.</typeparam>
	/// <param name="adapter">The adapter under test.</param>
	/// <param name="payload">The payload to decode.</param>
	/// <param name="readValue">Reads the scalar. It need not consume it, and never needs to close the wrapper.</param>
	/// <returns>Whatever <paramref name="readValue"/> returned.</returns>
	internal static TResult DecodeScalar<TEncoder, TDecoder, TResult>(FormatConformanceAdapter<TEncoder, TDecoder> adapter, ReadOnlyMemory<byte> payload, DecodeFunc<TDecoder, TResult> readValue)
		where TEncoder : IEncoder, allows ref struct
		where TDecoder : IDecoder, allows ref struct
		=> Decode(adapter, adapter.Options.SupportsRootScalars, payload, readValue);

	/// <summary>
	/// Decodes a payload produced by <see cref="EncodeVector"/>, positioning the decoder at the vector's start token.
	/// </summary>
	/// <typeparam name="TEncoder">The format's encoder type.</typeparam>
	/// <typeparam name="TDecoder">The format's decoder type.</typeparam>
	/// <typeparam name="TResult">The type of value the callback produces.</typeparam>
	/// <param name="adapter">The adapter under test.</param>
	/// <param name="payload">The payload to decode.</param>
	/// <param name="readValue">Reads the vector. It never needs to close the wrapper.</param>
	/// <returns>Whatever <paramref name="readValue"/> returned.</returns>
	internal static TResult DecodeVector<TEncoder, TDecoder, TResult>(FormatConformanceAdapter<TEncoder, TDecoder> adapter, ReadOnlyMemory<byte> payload, DecodeFunc<TDecoder, TResult> readValue)
		where TEncoder : IEncoder, allows ref struct
		where TDecoder : IDecoder, allows ref struct
		=> Decode(adapter, adapter.Options.SupportsRootVectors, payload, readValue);

	/// <summary>
	/// Decodes a payload produced by <see cref="EncodeVector"/> with a callback that produces no result.
	/// </summary>
	/// <typeparam name="TEncoder">The format's encoder type.</typeparam>
	/// <typeparam name="TDecoder">The format's decoder type.</typeparam>
	/// <param name="adapter">The adapter under test.</param>
	/// <param name="payload">The payload to decode.</param>
	/// <param name="readValue">Reads the vector.</param>
	internal static void DecodeVector<TEncoder, TDecoder>(FormatConformanceAdapter<TEncoder, TDecoder> adapter, ReadOnlyMemory<byte> payload, DecodeAction<TDecoder> readValue)
		where TEncoder : IEncoder, allows ref struct
		where TDecoder : IDecoder, allows ref struct
	{
		Requires.NotNull(readValue);
		DecodeVector(adapter, payload, (ref TDecoder decoder) =>
		{
			readValue(ref decoder);
			return 0;
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
	internal static TValue RoundtripScalar<TEncoder, TDecoder, TValue>(
		FormatConformanceAdapter<TEncoder, TDecoder> adapter,
		EncodeAction<TEncoder> writeValue,
		DecodeFunc<TDecoder, TValue> readValue)
		where TEncoder : IEncoder, allows ref struct
		where TDecoder : IDecoder, allows ref struct
		=> DecodeScalar<TEncoder, TDecoder, TValue>(adapter, EncodeScalar(adapter, writeValue), readValue);

	private static byte[] Encode<TEncoder, TDecoder>(FormatConformanceAdapter<TEncoder, TDecoder> adapter, bool allowedAtRoot, EncodeAction<TEncoder> writeValue)
		where TEncoder : IEncoder, allows ref struct
		where TDecoder : IDecoder, allows ref struct
	{
		if (allowedAtRoot)
		{
			return adapter.Encode(writeValue);
		}

		return adapter.Encode((ref TEncoder encoder) =>
		{
			encoder.WriteStartMap(1);
			encoder.WritePropertyName(WrapperKey);
			writeValue(ref encoder);
			encoder.WriteEndMap();
		});
	}

	private static TResult Decode<TEncoder, TDecoder, TResult>(FormatConformanceAdapter<TEncoder, TDecoder> adapter, bool allowedAtRoot, ReadOnlyMemory<byte> payload, DecodeFunc<TDecoder, TResult> readValue)
		where TEncoder : IEncoder, allows ref struct
		where TDecoder : IDecoder, allows ref struct
	{
		if (allowedAtRoot)
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
}
