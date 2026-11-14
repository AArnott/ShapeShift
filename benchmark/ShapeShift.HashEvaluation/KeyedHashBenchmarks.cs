// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using ShapeShift.Equality;

namespace ShapeShift.Benchmarks.HashEvaluation;

/// <summary>
/// Compares one full keyed hash plus a 32-bit fold, which is what collision-resistant
/// leaf hashing does on every call. The process-wide key is parsed once, outside the loop.
/// </summary>
[MemoryDiagnoser]
public class KeyedHashBenchmarks
{
	private static readonly Lazy<bool> VectorsVerified = new(VerifyVectors);

	private readonly uint[] keyWords = new uint[8];

	private byte[] message = [];

	private ulong sipK0;

	private ulong sipK1;

	/// <summary>
	/// Gets or sets the number of bytes hashed by each call.
	/// 4–16 covers numeric leaves, 8–64 covers short strings (UTF-16 bytes), and
	/// 1 KiB–64 KiB covers larger byte arrays.
	/// </summary>
	[Params(4, 8, 16, 32, 64, 128, 512, 1024, 4096, 16384, 65536)]
	public int Length { get; set; }

	/// <summary>
	/// Parses the keys once and refuses to time a prototype that fails the official vectors.
	/// </summary>
	[GlobalSetup]
	public void Setup()
	{
		_ = VectorsVerified.Value;

		Span<byte> blakeKey = stackalloc byte[Blake3.KeyLength];
		Span<byte> sipKey = stackalloc byte[16];
		RandomNumberGenerator.Fill(blakeKey);
		RandomNumberGenerator.Fill(sipKey);
		for (int i = 0; i < this.keyWords.Length; i++)
		{
			this.keyWords[i] = BinaryPrimitives.ReadUInt32LittleEndian(blakeKey.Slice(i * 4, 4));
		}

		this.sipK0 = BinaryPrimitives.ReadUInt64LittleEndian(sipKey);
		this.sipK1 = BinaryPrimitives.ReadUInt64LittleEndian(sipKey[8..]);
		this.message = new byte[this.Length];
		for (int i = 0; i < this.message.Length; i++)
		{
			this.message[i] = unchecked((byte)(i % 251));
		}
	}

	/// <summary>
	/// SipHash-2-4 over the message, folded to 32 bits. This is the production leaf hash.
	/// </summary>
	/// <returns>The folded hash code.</returns>
	[Benchmark(Baseline = true)]
	public int SipHash24() => SipHash.Fold(SipHash.Compute(this.message, this.sipK0, this.sipK1));

	/// <summary>
	/// Scalar keyed BLAKE3 over the message, folded from the 32-byte digest to 32 bits.
	/// </summary>
	/// <returns>The folded hash code.</returns>
	[Benchmark]
	public int Blake3Keyed() => Blake3.KeyedHash32(this.keyWords, this.message);

	private static bool VerifyVectors()
	{
		Blake3SelfTest.Run();
		return true;
	}
}
