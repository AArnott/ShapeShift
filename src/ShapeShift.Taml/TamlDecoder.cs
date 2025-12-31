// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;

namespace ShapeShift.Taml;

/// <summary>
/// A ShapeShift-compatible TAML decoder.
/// </summary>
/// <param name="reader">The underlying text reader from which to get the TAML.</param>
public ref struct TamlDecoder(TextReader reader) : IDecoder
{
	public TokenType NextTokenType => throw new NotImplementedException();

	public void ReadNull()
	{
		throw new NotImplementedException();
	}

	public BigInteger ReadBigInteger()
	{
		throw new NotImplementedException();
	}

	public bool ReadBoolean()
	{
		throw new NotImplementedException();
	}

	public ReadOnlySpan<char> ReadCharSpan()
	{
		throw new NotImplementedException();
	}

	public DateTime ReadDateTime()
	{
		throw new NotImplementedException();
	}

	public decimal ReadDecimal()
	{
		throw new NotImplementedException();
	}

	public double ReadDouble()
	{
		throw new NotImplementedException();
	}

	public void ReadEndMap()
	{
		throw new NotImplementedException();
	}

	public void ReadEndVector()
	{
		throw new NotImplementedException();
	}

	public Half ReadHalf()
	{
		throw new NotImplementedException();
	}

	public Int128 ReadInt128()
	{
		throw new NotImplementedException();
	}

	public long ReadInt64()
	{
		throw new NotImplementedException();
	}

	public ReadOnlySpan<char> ReadPropertyName()
	{
		throw new NotImplementedException();
	}

	public float ReadSingle()
	{
		throw new NotImplementedException();
	}

	public int? ReadStartMap()
	{
		throw new NotImplementedException();
	}

	public int? ReadStartVector()
	{
		throw new NotImplementedException();
	}

	public string ReadString()
	{
		throw new NotImplementedException();
	}

	public TimeSpan ReadTimeSpan()
	{
		throw new NotImplementedException();
	}

	public UInt128 ReadUInt128()
	{
		throw new NotImplementedException();
	}

	public ulong ReadUInt64()
	{
		throw new NotImplementedException();
	}

	public void Skip()
	{
		throw new NotImplementedException();
	}

	public bool TryReadNull()
	{
		throw new NotImplementedException();
	}
}
