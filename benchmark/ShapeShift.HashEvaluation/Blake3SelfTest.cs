// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace ShapeShift.Benchmarks.HashEvaluation;

/// <summary>
/// Checks the prototype against the official BLAKE3 test vectors, including keyed mode
/// and extended output, plus the worked keyed example in the C2SP specification.
/// </summary>
internal static class Blake3SelfTest
{
	private const string ResourceName = "ShapeShift.Benchmarks.Blake3TestVectors.json";

	/// <summary>
	/// Source of <c>test_vectors.json</c>. Input byte <c>i</c> is <c>i % 251</c>.
	/// The keyed-mode key is the 32-byte ASCII string in the file.
	/// Extended outputs are 131 bytes, and the first 32 must match a default-length hash.
	/// </summary>
	private const string VectorSource = "https://github.com/BLAKE3-team/BLAKE3/blob/ae3e8e6b3a5ae3190ca5d62820789b17886a0038/test_vectors/test_vectors.json";

	/// <summary>
	/// Runs every official case in <c>hash</c>, <c>keyed_hash</c>, and <c>derive_key</c> mode
	/// and the C2SP two-chunk keyed example.
	/// </summary>
	/// <exception cref="InvalidOperationException">The prototype disagreed with a vector.</exception>
	internal static void Run()
	{
		RunSpecificationExample();
		RunOfficialVectors();
	}

	private static void RunSpecificationExample()
	{
		// c2sp.org/BLAKE3, "keyed_hash of Multiple Chunks":
		// key = 32 bytes of 0xcc, message = 1024 bytes of 0xaa || 1024 bytes of 0xbb.
		Span<byte> key = stackalloc byte[Blake3.KeyLength];
		key.Fill(0xcc);
		byte[] message = new byte[2048];
		message.AsSpan(0, 1024).Fill(0xaa);
		message.AsSpan(1024, 1024).Fill(0xbb);

		ReadOnlySpan<byte> expected = Convert.FromHexString("34afab3d37b3971642df4b84862c3dfa5c50d5351be79ce33bd924de559f8d05");
		Span<byte> actual = stackalloc byte[Blake3.DefaultOutputLength];
		Blake3.KeyedHash(key, message, actual);
		if (!actual.SequenceEqual(expected))
		{
			throw new InvalidOperationException("BLAKE3 disagreed with the C2SP keyed two-chunk example.");
		}
	}

	private static void RunOfficialVectors()
	{
		using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
			?? throw new InvalidOperationException($"Missing embedded BLAKE3 vectors ({VectorSource}).");
		using JsonDocument document = JsonDocument.Parse(stream);
		JsonElement root = document.RootElement;
		byte[] key = Encoding.ASCII.GetBytes(root.GetProperty("key").GetString() ?? string.Empty);
		byte[] context = Encoding.ASCII.GetBytes(root.GetProperty("context_string").GetString() ?? string.Empty);
		if (key.Length != Blake3.KeyLength)
		{
			throw new InvalidOperationException($"Official keyed-mode key was {key.Length} bytes, not {Blake3.KeyLength}.");
		}

		Span<uint> keyWords = stackalloc uint[8];
		for (int i = 0; i < 8; i++)
		{
			keyWords[i] = BinaryPrimitives.ReadUInt32LittleEndian(key.AsSpan(i * 4, 4));
		}

		int cases = 0;
		foreach (JsonElement testCase in root.GetProperty("cases").EnumerateArray())
		{
			int length = testCase.GetProperty("input_len").GetInt32();
			byte[] message = new byte[length];
			for (int i = 0; i < message.Length; i++)
			{
				message[i] = unchecked((byte)(i % 251));
			}

			Check("hash", length, message, testCase.GetProperty("hash").GetString(), static (message, output) => Blake3.Hash(message, output), keyWords: default);
			Check("keyed_hash", length, message, testCase.GetProperty("keyed_hash").GetString(), (message, output) => Blake3.KeyedHash(key, message, output), keyWords);
			Check("derive_key", length, message, testCase.GetProperty("derive_key").GetString(), (message, output) => Blake3.DeriveKey(context, message, output), keyWords: default);
			cases++;
		}

		if (cases != 35)
		{
			throw new InvalidOperationException($"Expected 35 official BLAKE3 cases from {VectorSource}, found {cases}.");
		}
	}

	private static void Check(
		string mode,
		int length,
		byte[] message,
		string? expectedHex,
		Action<byte[], byte[]> hash,
		ReadOnlySpan<uint> keyWords)
	{
		if (string.IsNullOrEmpty(expectedHex))
		{
			throw new InvalidOperationException($"Missing {mode} vector for input length {length}.");
		}

		byte[] expected = Convert.FromHexString(expectedHex);
		byte[] actual = new byte[expected.Length];
		hash(message, actual);
		if (!actual.AsSpan().SequenceEqual(expected))
		{
			int index = 0;
			while (index < actual.Length && actual[index] == expected[index])
			{
				index++;
			}

			throw new InvalidOperationException($"BLAKE3 {mode} disagreed with the official vector at input length {length}, output byte {index}.");
		}

		byte[] prefix = new byte[Blake3.DefaultOutputLength];
		hash(message, prefix);
		if (!prefix.AsSpan().SequenceEqual(expected.AsSpan(0, Blake3.DefaultOutputLength)))
		{
			throw new InvalidOperationException($"BLAKE3 {mode} 32-byte output disagreed with the extended-output prefix at input length {length}.");
		}

		if (!keyWords.IsEmpty)
		{
			uint folded = 0;
			for (int i = 0; i < 8; i++)
			{
				folded ^= BinaryPrimitives.ReadUInt32LittleEndian(expected.AsSpan(i * 4, 4));
			}

			int actualFold = Blake3.KeyedHash32(keyWords, message);
			if (actualFold != unchecked((int)folded))
			{
				throw new InvalidOperationException(
					$"BLAKE3 keyed 32-bit fold disagreed at input length {length}: actual 0x{actualFold:x8}, expected 0x{folded:x8}.");
			}
		}
	}
}
