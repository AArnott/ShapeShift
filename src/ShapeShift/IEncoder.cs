// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

	void WriteValue(Half value);

	void WriteValue(float value);

	void WriteValue(double value);

	void WriteValue(string value);

	void WriteValue(scoped ReadOnlySpan<char> value);
}
