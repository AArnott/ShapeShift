// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;

namespace ShapeShift;

public interface IEncoder
{
	void WriteStartMap(int? propertyCount);

	void WriteEndMap();

	void WriteStartVector(int? itemCount);

	void WriteEndVector();

	void WritePropertyName(scoped ReadOnlySpan<char> name);

	void WriteNull();

	void WriteValue(bool value);

	void WriteValue(long value);

	void WriteValue(ulong value);

	void WriteValue(Int128 value);

	void WriteValue(UInt128 value);

	void WriteValue(Half value);

	void WriteValue(float value);

	void WriteValue(double value);

	void WriteValue(decimal value);

	void WriteValue(DateTime value);

	void WriteValue(TimeSpan value);

	void WriteValue(BigInteger value);

	void WriteValue(string value);

	void WriteValue(scoped ReadOnlySpan<char> value);
}
