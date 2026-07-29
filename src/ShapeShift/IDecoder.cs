// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;

namespace ShapeShift;

public interface IDecoder
{
	public TokenType NextTokenType { get; }

	public bool TryReadNull();

	public int? ReadStartMap();

	public void ReadEndMap();

	public int? ReadStartVector();

	public void ReadEndVector();

	public ReadOnlySpan<char> ReadPropertyName();

	public void Skip();

	public void ReadNull()
	{
		if (!this.TryReadNull())
		{
			throw new DecoderException($"Expected a null token but instead got {this.NextTokenType}.");
		}
	}

	public bool ReadBoolean();

	public long ReadInt64();

	public ulong ReadUInt64();

	public Int128 ReadInt128();

	public UInt128 ReadUInt128();

	public Half ReadHalf();

	public float ReadSingle();

	public double ReadDouble();

	public decimal ReadDecimal();

	public DateTime ReadDateTime();

	public TimeSpan ReadTimeSpan();

	public BigInteger ReadBigInteger();

	public string ReadString();

	public ReadOnlySpan<char> ReadCharSpan();

	/// <summary>
	/// Reads a string as a character span, using the supplied buffer when unescaping is required.
	/// </summary>
	/// <param name="buffer">The buffer available for unescaping the string.</param>
	/// <param name="charactersWritten">Receives the number of unescaped characters written to <paramref name="buffer"/>, or <c>-1</c> when the returned span contains the result.</param>
	/// <returns>The characters that make up the string when <paramref name="charactersWritten"/> is <c>-1</c>; otherwise, an empty span.</returns>
	/// <remarks>
	/// Implementations should return a span into their input when unescaping is unnecessary.
	/// </remarks>
	public ReadOnlySpan<char> ReadCharSpan(scoped Span<char> buffer, out int charactersWritten)
	{
		charactersWritten = -1;
		return this.ReadCharSpan();
	}
}
