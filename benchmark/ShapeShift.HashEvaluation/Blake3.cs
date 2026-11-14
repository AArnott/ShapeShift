// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace ShapeShift.Benchmarks.HashEvaluation;

/// <summary>
/// A scalar, allocation-free prototype of BLAKE3 for the keyed-hash evaluation.
/// </summary>
/// <remarks>
/// <para>
/// This is a direct port of the reference algorithm: one 64-byte block compression of 7 rounds,
/// 1024-byte chunks, and the binary CV stack. Keyed mode uses the 32-byte key as the chaining
/// value and sets <c>KEYED_HASH</c> on every compression, including parent nodes. It is not a
/// SIMD implementation. Upstream BLAKE3 gets its published throughput by compressing several
/// independent chunks at once with SSE, AVX2, AVX-512, or NEON; a single chunk is a serial chain
/// of blocks, so that parallelism does not apply to the 4–64 byte inputs this library hashes.
/// </para>
/// <para>
/// Specification: <see href="https://c2sp.org/BLAKE3">c2sp.org/BLAKE3</see>.
/// Reference used while porting:
/// <see href="https://github.com/BLAKE3-team/BLAKE3/blob/master/reference_impl/reference_impl.rs">reference_impl.rs</see>.
/// </para>
/// </remarks>
internal static class Blake3
{
	/// <summary>The keyed-mode key length, in bytes.</summary>
	internal const int KeyLength = 32;

	/// <summary>The compression-function block length, in bytes.</summary>
	internal const int BlockLength = 64;

	/// <summary>The chunk length, in bytes. Inputs no longer than this are a single chunk.</summary>
	internal const int ChunkLength = 1024;

	/// <summary>The default digest length, in bytes.</summary>
	internal const int DefaultOutputLength = 32;

	private const int MaxDepth = 54;

	private const uint ChunkStart = 1 << 0;

	private const uint ChunkEnd = 1 << 1;

	private const uint Parent = 1 << 2;

	private const uint Root = 1 << 3;

	private const uint KeyedHashFlag = 1 << 4;

	private const uint DeriveKeyContext = 1 << 5;

	private const uint DeriveKeyMaterial = 1 << 6;

	private const uint Iv0 = 0x6A09E667;

	private const uint Iv1 = 0xBB67AE85;

	private const uint Iv2 = 0x3C6EF372;

	private const uint Iv3 = 0xA54FF53A;

	private const uint Iv4 = 0x510E527F;

	private const uint Iv5 = 0x9B05688C;

	private const uint Iv6 = 0x1F83D9AB;

	private const uint Iv7 = 0x5BE0CD19;

	/// <summary>
	/// Gets the message-word indices for each of the seven rounds. Row <c>r</c> is the identity
	/// permutation applied <c>r</c> times.
	/// </summary>
	private static ReadOnlySpan<byte> MessageSchedule =>
	[
		0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
		2, 6, 3, 10, 7, 0, 4, 13, 1, 11, 12, 5, 9, 14, 15, 8,
		3, 4, 10, 12, 13, 2, 7, 14, 6, 5, 9, 0, 11, 15, 8, 1,
		10, 7, 12, 9, 14, 3, 13, 15, 4, 0, 11, 2, 5, 8, 1, 6,
		12, 13, 9, 11, 15, 10, 14, 8, 7, 2, 5, 3, 0, 1, 6, 4,
		9, 14, 11, 5, 8, 12, 15, 1, 13, 3, 0, 10, 2, 6, 4, 7,
		11, 15, 5, 0, 1, 9, 8, 6, 14, 10, 2, 12, 3, 4, 7, 13,
	];

	/// <summary>
	/// Computes an unkeyed BLAKE3 hash into <paramref name="output"/> (any positive length).
	/// </summary>
	/// <param name="message">The input bytes.</param>
	/// <param name="output">The buffer that receives the extendable output.</param>
	internal static void Hash(ReadOnlySpan<byte> message, Span<byte> output)
	{
		Span<uint> iv = stackalloc uint[8];
		WriteIv(iv);
		WriteOutput(PrepareRoot(message, iv, modeFlags: 0), output);
	}

	/// <summary>
	/// Computes a keyed BLAKE3 hash. The key is mixed into the chaining value on every call.
	/// </summary>
	/// <param name="key">The 32-byte key.</param>
	/// <param name="message">The input bytes.</param>
	/// <param name="output">The buffer that receives the extendable output.</param>
	internal static void KeyedHash(ReadOnlySpan<byte> key, ReadOnlySpan<byte> message, Span<byte> output)
	{
		if (key.Length != KeyLength)
		{
			throw new ArgumentException("BLAKE3 keyed mode requires a 32-byte key.", nameof(key));
		}

		Span<uint> keyWords = stackalloc uint[8];
		ReadKeyWords(key, keyWords);
		WriteOutput(PrepareRoot(message, keyWords, KeyedHashFlag), output);
	}

	/// <summary>
	/// Computes a keyed BLAKE3 hash and folds the 32-byte digest to the 32-bit
	/// <see cref="object.GetHashCode"/> width, with no heap allocation.
	/// </summary>
	/// <param name="keyWords">The key as eight little-endian words, parsed once per process.</param>
	/// <param name="message">The input bytes.</param>
	/// <returns>The folded 32-bit hash.</returns>
	internal static int KeyedHash32(ReadOnlySpan<uint> keyWords, ReadOnlySpan<byte> message)
	{
		Span<uint> state = stackalloc uint[16];
		RootNode node = PrepareRoot(message, keyWords, KeyedHashFlag);
		CompressNode(node, counter: 0, node.Flags | Root, state);
		uint folded = state[0] ^ state[1] ^ state[2] ^ state[3] ^ state[4] ^ state[5] ^ state[6] ^ state[7];
		return unchecked((int)folded);
	}

	/// <summary>
	/// Computes BLAKE3 in <c>derive_key</c> mode.
	/// </summary>
	/// <param name="context">The hardcoded context string.</param>
	/// <param name="material">The key material.</param>
	/// <param name="output">The buffer that receives the derived key bytes.</param>
	internal static void DeriveKey(ReadOnlySpan<byte> context, ReadOnlySpan<byte> material, Span<byte> output)
	{
		Span<byte> contextKey = stackalloc byte[KeyLength];
		HashContext(context, contextKey);
		Span<uint> keyWords = stackalloc uint[8];
		ReadKeyWords(contextKey, keyWords);
		WriteOutput(PrepareRoot(material, keyWords, DeriveKeyMaterial), output);
	}

	private static void HashContext(ReadOnlySpan<byte> context, Span<byte> output)
	{
		Span<uint> iv = stackalloc uint[8];
		WriteIv(iv);
		WriteOutput(PrepareRoot(context, iv, DeriveKeyContext), output);
	}

	private static void WriteIv(Span<uint> iv)
	{
		iv[0] = Iv0;
		iv[1] = Iv1;
		iv[2] = Iv2;
		iv[3] = Iv3;
		iv[4] = Iv4;
		iv[5] = Iv5;
		iv[6] = Iv6;
		iv[7] = Iv7;
	}

	private static void ReadKeyWords(ReadOnlySpan<byte> key, Span<uint> keyWords)
	{
		for (int i = 0; i < 8; i++)
		{
			keyWords[i] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(i * 4, 4));
		}
	}

	private static RootNode PrepareRoot(ReadOnlySpan<byte> message, ReadOnlySpan<uint> keyWords, uint modeFlags)
	{
		if (message.Length <= ChunkLength)
		{
			return ChunkOutput(message, keyWords, chunkCounter: 0, modeFlags);
		}

		Hasher hasher = new(keyWords, modeFlags);
		hasher.Update(message);
		return hasher.FinalizeNode();
	}

	/// <summary>
	/// Hashes one chunk of at most 1024 bytes down to the final uncompressed node.
	/// The caller sets <c>ROOT</c> when this chunk is the whole message.
	/// </summary>
	private static RootNode ChunkOutput(ReadOnlySpan<byte> chunk, ReadOnlySpan<uint> keyWords, ulong chunkCounter, uint modeFlags)
	{
		Span<uint> cv = stackalloc uint[8];
		keyWords.CopyTo(cv);
		Span<uint> blockWords = stackalloc uint[16];
		Span<uint> state = stackalloc uint[16];

		int offset = 0;
		int blocksCompressed = 0;
		while (chunk.Length - offset > BlockLength)
		{
			ReadBlockWords(chunk.Slice(offset, BlockLength), blockWords);
			uint flags = modeFlags | (blocksCompressed == 0 ? ChunkStart : 0);
			Compress(cv, blockWords, chunkCounter, BlockLength, flags, state);
			for (int i = 0; i < 8; i++)
			{
				cv[i] = state[i];
			}

			offset += BlockLength;
			blocksCompressed++;
		}

		int tailLength = chunk.Length - offset;
		Span<byte> padded = stackalloc byte[BlockLength];
		chunk.Slice(offset, tailLength).CopyTo(padded);
		ReadBlockWords(padded, blockWords);

		RootNode node = default;
		for (int i = 0; i < 8; i++)
		{
			node.ChainingValue[i] = cv[i];
		}

		for (int i = 0; i < 16; i++)
		{
			node.BlockWords[i] = blockWords[i];
		}

		node.Counter = chunkCounter;
		node.BlockLength = (uint)tailLength;
		node.Flags = modeFlags | (blocksCompressed == 0 ? ChunkStart : 0) | ChunkEnd;
		return node;
	}

	private static void WriteOutput(in RootNode node, Span<byte> output)
	{
		Span<uint> state = stackalloc uint[16];
		ulong outputCounter = 0;
		while (output.Length > 0)
		{
			CompressNode(node, outputCounter, node.Flags | Root, state);
			int take = Math.Min(output.Length, BlockLength);
			WriteStateBytes(state, output[..take]);
			output = output[take..];
			outputCounter++;
		}
	}

	private static void CompressNode(in RootNode node, ulong counter, uint flags, Span<uint> state)
	{
		Span<uint> cv = stackalloc uint[8];
		Span<uint> blockWords = stackalloc uint[16];
		for (int i = 0; i < 8; i++)
		{
			cv[i] = node.ChainingValue[i];
		}

		for (int i = 0; i < 16; i++)
		{
			blockWords[i] = node.BlockWords[i];
		}

		Compress(cv, blockWords, counter, node.BlockLength, flags, state);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void Compress(
		ReadOnlySpan<uint> chainingValue,
		ReadOnlySpan<uint> blockWords,
		ulong counter,
		uint blockLength,
		uint flags,
		Span<uint> state)
	{
		state[0] = chainingValue[0];
		state[1] = chainingValue[1];
		state[2] = chainingValue[2];
		state[3] = chainingValue[3];
		state[4] = chainingValue[4];
		state[5] = chainingValue[5];
		state[6] = chainingValue[6];
		state[7] = chainingValue[7];
		state[8] = Iv0;
		state[9] = Iv1;
		state[10] = Iv2;
		state[11] = Iv3;
		state[12] = (uint)counter;
		state[13] = (uint)(counter >> 32);
		state[14] = blockLength;
		state[15] = flags;

		ReadOnlySpan<byte> schedule = MessageSchedule;
		for (int round = 0; round < 7; round++)
		{
			int offset = round * 16;
			G(state, 0, 4, 8, 12, blockWords[schedule[offset]], blockWords[schedule[offset + 1]]);
			G(state, 1, 5, 9, 13, blockWords[schedule[offset + 2]], blockWords[schedule[offset + 3]]);
			G(state, 2, 6, 10, 14, blockWords[schedule[offset + 4]], blockWords[schedule[offset + 5]]);
			G(state, 3, 7, 11, 15, blockWords[schedule[offset + 6]], blockWords[schedule[offset + 7]]);
			G(state, 0, 5, 10, 15, blockWords[schedule[offset + 8]], blockWords[schedule[offset + 9]]);
			G(state, 1, 6, 11, 12, blockWords[schedule[offset + 10]], blockWords[schedule[offset + 11]]);
			G(state, 2, 7, 8, 13, blockWords[schedule[offset + 12]], blockWords[schedule[offset + 13]]);
			G(state, 3, 4, 9, 14, blockWords[schedule[offset + 14]], blockWords[schedule[offset + 15]]);
		}

		for (int i = 0; i < 8; i++)
		{
			state[i] ^= state[i + 8];
			state[i + 8] ^= chainingValue[i];
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void G(Span<uint> state, int a, int b, int c, int d, uint mx, uint my)
	{
		unchecked
		{
			state[a] = state[a] + state[b] + mx;
			state[d] = BitOperations.RotateRight(state[d] ^ state[a], 16);
			state[c] += state[d];
			state[b] = BitOperations.RotateRight(state[b] ^ state[c], 12);
			state[a] = state[a] + state[b] + my;
			state[d] = BitOperations.RotateRight(state[d] ^ state[a], 8);
			state[c] += state[d];
			state[b] = BitOperations.RotateRight(state[b] ^ state[c], 7);
		}
	}

	private static void ReadBlockWords(ReadOnlySpan<byte> block, Span<uint> words)
	{
		for (int i = 0; i < 16; i++)
		{
			words[i] = BinaryPrimitives.ReadUInt32LittleEndian(block.Slice(i * 4, 4));
		}
	}

	private static void WriteStateBytes(ReadOnlySpan<uint> state, Span<byte> output)
	{
		int wordIndex = 0;
		while (output.Length >= 4)
		{
			BinaryPrimitives.WriteUInt32LittleEndian(output, state[wordIndex]);
			output = output[4..];
			wordIndex++;
		}

		if (output.Length > 0)
		{
			uint word = state[wordIndex];
			for (int i = 0; i < output.Length; i++)
			{
				output[i] = unchecked((byte)word);
				word >>= 8;
			}
		}
	}

	/// <summary>
	/// Incremental hasher used only when the input spans more than one chunk.
	/// Its CV stack is inline storage, not a heap array.
	/// </summary>
	private ref struct Hasher
	{
		private Word8 keyWords;
		private Word8 chunkCv;
		private Byte64 block;
		private Word432 cvStack;
		private ulong chunkCounter;
		private int blockLength;
		private int blocksCompressed;
		private int cvStackLength;
		private uint modeFlags;

		public Hasher(ReadOnlySpan<uint> keyWords, uint modeFlags)
		{
			this.modeFlags = modeFlags;
			for (int i = 0; i < 8; i++)
			{
				this.keyWords[i] = keyWords[i];
				this.chunkCv[i] = keyWords[i];
			}
		}

		public void Update(ReadOnlySpan<byte> input)
		{
			Span<uint> cv = stackalloc uint[8];
			while (!input.IsEmpty)
			{
				if ((this.blocksCompressed * BlockLength) + this.blockLength == ChunkLength)
				{
					this.ChainingValue(this.TakeChunkNode(), cv);
					this.AddChunk(cv, this.chunkCounter + 1);
					this.ResetChunk(this.chunkCounter + 1);
				}

				int room = ChunkLength - ((this.blocksCompressed * BlockLength) + this.blockLength);
				int take = Math.Min(room, input.Length);
				this.UpdateChunk(input[..take]);
				input = input[take..];
			}
		}

		public RootNode FinalizeNode()
		{
			RootNode node = this.TakeChunkNode();
			Span<uint> left = stackalloc uint[8];
			Span<uint> right = stackalloc uint[8];
			for (int remaining = this.cvStackLength; remaining > 0; remaining--)
			{
				int offset = (remaining - 1) * 8;
				for (int i = 0; i < 8; i++)
				{
					left[i] = this.cvStack[offset + i];
				}

				this.ChainingValue(node, right);
				node = this.ParentNode(left, right);
			}

			return node;
		}

		private void UpdateChunk(ReadOnlySpan<byte> input)
		{
			while (!input.IsEmpty)
			{
				if (this.blockLength == BlockLength)
				{
					this.CompressBufferedBlock();
					this.blockLength = 0;
					this.blocksCompressed++;
					for (int i = 0; i < BlockLength; i++)
					{
						this.block[i] = 0;
					}
				}

				int take = Math.Min(BlockLength - this.blockLength, input.Length);
				for (int i = 0; i < take; i++)
				{
					this.block[this.blockLength + i] = input[i];
				}

				this.blockLength += take;
				input = input[take..];
			}
		}

		private void CompressBufferedBlock()
		{
			Span<uint> words = stackalloc uint[16];
			Span<uint> cv = stackalloc uint[8];
			Span<uint> state = stackalloc uint[16];
			for (int i = 0; i < 16; i++)
			{
				words[i] = this.ReadBufferedWord(i);
			}

			for (int i = 0; i < 8; i++)
			{
				cv[i] = this.chunkCv[i];
			}

			uint flags = this.modeFlags | (this.blocksCompressed == 0 ? ChunkStart : 0);
			Compress(cv, words, this.chunkCounter, BlockLength, flags, state);
			for (int i = 0; i < 8; i++)
			{
				this.chunkCv[i] = state[i];
			}
		}

		private RootNode TakeChunkNode()
		{
			RootNode node = default;
			for (int i = 0; i < 8; i++)
			{
				node.ChainingValue[i] = this.chunkCv[i];
			}

			for (int i = 0; i < 16; i++)
			{
				node.BlockWords[i] = this.ReadBufferedWord(i);
			}

			node.Counter = this.chunkCounter;
			node.BlockLength = (uint)this.blockLength;
			node.Flags = this.modeFlags | (this.blocksCompressed == 0 ? ChunkStart : 0) | ChunkEnd;
			return node;
		}

		private void ChainingValue(in RootNode node, scoped Span<uint> destination)
		{
			Span<uint> state = stackalloc uint[16];
			CompressNode(node, node.Counter, node.Flags, state);
			for (int i = 0; i < 8; i++)
			{
				destination[i] = state[i];
			}
		}

		private RootNode ParentNode(scoped ReadOnlySpan<uint> left, scoped ReadOnlySpan<uint> right)
		{
			RootNode node = default;
			for (int i = 0; i < 8; i++)
			{
				node.ChainingValue[i] = this.keyWords[i];
				node.BlockWords[i] = left[i];
				node.BlockWords[i + 8] = right[i];
			}

			node.Counter = 0;
			node.BlockLength = BlockLength;
			node.Flags = this.modeFlags | Parent;
			return node;
		}

		private void AddChunk(scoped Span<uint> newCv, ulong totalChunks)
		{
			Span<uint> left = stackalloc uint[8];
			Span<uint> merged = stackalloc uint[8];
			while ((totalChunks & 1) == 0)
			{
				if (this.cvStackLength <= 0)
				{
					throw new InvalidOperationException("BLAKE3 chaining-value stack underflow.");
				}

				this.cvStackLength--;
				int offset = this.cvStackLength * 8;
				for (int i = 0; i < 8; i++)
				{
					left[i] = this.cvStack[offset + i];
				}

				RootNode parent = this.ParentNode(left, newCv);
				this.ChainingValue(parent, merged);
				merged.CopyTo(newCv);
				totalChunks >>= 1;
			}

			if (this.cvStackLength >= MaxDepth)
			{
				throw new InvalidOperationException("BLAKE3 chaining-value stack overflow.");
			}

			int pushAt = this.cvStackLength * 8;
			for (int i = 0; i < 8; i++)
			{
				this.cvStack[pushAt + i] = newCv[i];
			}

			this.cvStackLength++;
		}

		private void ResetChunk(ulong chunkCounter)
		{
			for (int i = 0; i < 8; i++)
			{
				this.chunkCv[i] = this.keyWords[i];
			}

			for (int i = 0; i < BlockLength; i++)
			{
				this.block[i] = 0;
			}

			this.chunkCounter = chunkCounter;
			this.blockLength = 0;
			this.blocksCompressed = 0;
		}

		private readonly uint ReadBufferedWord(int wordIndex)
		{
			int offset = wordIndex * 4;
			return (uint)this.block[offset]
				| ((uint)this.block[offset + 1] << 8)
				| ((uint)this.block[offset + 2] << 16)
				| ((uint)this.block[offset + 3] << 24);
		}
	}

	[InlineArray(8)]
	private struct Word8
	{
		private uint element0;
	}

	[InlineArray(64)]
	private struct Byte64
	{
		private byte element0;
	}

	[InlineArray(432)]
	private struct Word432
	{
		private uint element0;
	}

	private struct RootNode
	{
		public Word8 ChainingValue;

		public Word16 BlockWords;

		public ulong Counter;

		public uint BlockLength;

		public uint Flags;
	}

	[InlineArray(16)]
	private struct Word16
	{
		private uint element0;
	}
}
