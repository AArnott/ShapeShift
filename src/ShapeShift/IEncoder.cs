// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift;

public interface IEncoder
{
	void WriteStartMap(int? propertyCount);

	void WriteEndMap();

	void WriteStartVector(int? itemCount);

	void WriteEndVector();

	void WritePropertyName(ReadOnlySpan<char> name);

	void WriteNull();

	void Write(bool value);

	void Write(int value);

	void Write(string? value);
}
