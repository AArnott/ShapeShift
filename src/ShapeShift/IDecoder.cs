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
	/// Reads a binary value.
	/// </summary>
	/// <returns>The decoded bytes.</returns>
	/// <exception cref="NotSupportedException">Thrown when the format has no binary representation.</exception>
	public byte[] ReadByteArray() => throw new NotSupportedException("This decoder does not support binary values.");

	/// <summary>
	/// Reads a number while preserving the representation available from the format.
	/// </summary>
	/// <returns>The dynamic number.</returns>
	public ShapeShiftNumber ReadDynamicNumber() => new ShapeShiftDecimal(this.ReadDecimal());
}
