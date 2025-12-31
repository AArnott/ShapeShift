// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;

namespace ShapeShift.Taml;

/// <summary>
/// A ShapeShift-compatible TAML encoder.
/// </summary>
/// <param name="writer">The underlying text writer to which to write the TAML.</param>
public ref struct TamlEncoder(TextWriter writer) : IEncoder
{
	public void WriteEndMap()
	{
		throw new NotImplementedException();
	}

	public void WriteEndVector()
	{
		throw new NotImplementedException();
	}

	public void WriteNull()
	{
		throw new NotImplementedException();
	}

	public void WritePropertyName(scoped ReadOnlySpan<char> name)
	{
		throw new NotImplementedException();
	}

	public void WriteStartMap(int? propertyCount)
	{
		throw new NotImplementedException();
	}

	public void WriteStartVector(int? itemCount)
	{
		throw new NotImplementedException();
	}

	public void WriteValue(bool value)
	{
		throw new NotImplementedException();
	}

	public void WriteValue(long value)
	{
		throw new NotImplementedException();
	}

	public void WriteValue(ulong value)
	{
		throw new NotImplementedException();
	}

	public void WriteValue(Int128 value)
	{
		throw new NotImplementedException();
	}

	public void WriteValue(UInt128 value)
	{
		throw new NotImplementedException();
	}

	public void WriteValue(Half value)
	{
		throw new NotImplementedException();
	}

	public void WriteValue(float value)
	{
		throw new NotImplementedException();
	}

	public void WriteValue(double value)
	{
		throw new NotImplementedException();
	}

	public void WriteValue(decimal value)
	{
		throw new NotImplementedException();
	}

	public void WriteValue(DateTime value)
	{
		throw new NotImplementedException();
	}

	public void WriteValue(TimeSpan value)
	{
		throw new NotImplementedException();
	}

	public void WriteValue(BigInteger value)
	{
		throw new NotImplementedException();
	}

	public void WriteValue(string value)
	{
		throw new NotImplementedException();
	}

	public void WriteValue(scoped ReadOnlySpan<char> value)
	{
		throw new NotImplementedException();
	}
}
