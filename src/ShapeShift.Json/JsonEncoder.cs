// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Json;

/// <summary>
/// A ShapeShift encoder that writes UTF-8 JSON.
/// </summary>
/// <param name="writer">The underlying JSON writer.</param>
public ref struct JsonEncoder(Utf8JsonWriter writer) : IEncoder
{
	/// <summary>
	/// Gets the underlying JSON writer for advanced custom converters.
	/// </summary>
	public Utf8JsonWriter Writer => writer;

	/// <inheritdoc/>
	public void WriteStartMap(int? propertyCount) => writer.WriteStartObject();

	/// <inheritdoc/>
	public void WriteEndMap() => writer.WriteEndObject();

	/// <inheritdoc/>
	public void WriteStartVector(int? itemCount) => writer.WriteStartArray();

	/// <inheritdoc/>
	public void WriteEndVector() => writer.WriteEndArray();

	/// <inheritdoc/>
	public void WritePropertyName(scoped ReadOnlySpan<char> name) => writer.WritePropertyName(name);

	/// <inheritdoc/>
	public void WriteNull() => writer.WriteNullValue();

	/// <inheritdoc/>
	public void WriteValue(bool value) => writer.WriteBooleanValue(value);

	/// <inheritdoc/>
	public void WriteValue(long value) => writer.WriteNumberValue(value);

	/// <inheritdoc/>
	public void WriteValue(ulong value) => writer.WriteNumberValue(value);

	/// <inheritdoc/>
	public void WriteValue(Int128 value) => writer.WriteRawValue(value.ToString(System.Globalization.CultureInfo.InvariantCulture));

	/// <inheritdoc/>
	public void WriteValue(UInt128 value) => writer.WriteRawValue(value.ToString(System.Globalization.CultureInfo.InvariantCulture));

	/// <inheritdoc/>
	public void WriteValue(Half value) => writer.WriteNumberValue((float)value);

	/// <inheritdoc/>
	public void WriteValue(float value) => writer.WriteNumberValue(value);

	/// <inheritdoc/>
	public void WriteValue(double value) => writer.WriteNumberValue(value);

	/// <inheritdoc/>
	public void WriteValue(decimal value) => writer.WriteNumberValue(value);

	/// <inheritdoc/>
	public void WriteValue(DateTime value) => writer.WriteStringValue(value);

	/// <inheritdoc/>
	public void WriteValue(TimeSpan value) => writer.WriteStringValue(value.ToString("c", System.Globalization.CultureInfo.InvariantCulture));

	/// <inheritdoc/>
	public void WriteValue(BigInteger value) => writer.WriteRawValue(value.ToString(System.Globalization.CultureInfo.InvariantCulture));

	/// <inheritdoc/>
	public void WriteValue(string value) => writer.WriteStringValue(value);

	/// <inheritdoc/>
	public void WriteValue(scoped ReadOnlySpan<char> value) => writer.WriteStringValue(value);
}
