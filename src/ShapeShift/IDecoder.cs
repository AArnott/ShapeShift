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
}
