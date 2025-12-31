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

	void Write(bool value);

	void Write(long value);

	void Write(ulong value);

	void Write(Half value);

	void Write(float value);

	void Write(double value);

	void Write(string? value);

	void Write(scoped ReadOnlySpan<char> value);
}
