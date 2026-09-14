// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Json;

/// <summary>
/// A ShapeShift encoder that writes UTF-8 JSON.
/// </summary>
/// <param name="writer">The underlying JSON writer.</param>
/// <param name="allowNamedFloatingPointValues">Whether named non-finite floating-point strings are enabled.</param>
public ref struct JsonEncoder(Utf8JsonWriter writer, bool allowNamedFloatingPointValues = false) : IEncoder
{
	/// <summary>
	/// Gets a string encoder that escapes exactly those characters required by RFC 8259.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <see cref="JsonSerializer"/> uses this encoder automatically. Callers that construct their own
	/// <see cref="Utf8JsonWriter"/> for use with <see cref="JsonEncoder"/> should assign this value to
	/// <see cref="JsonWriterOptions.Encoder"/>.
	/// </para>
	/// <para>
	/// Output produced with this encoder must be HTML-encoded before it is embedded in HTML.
	/// </para>
	/// </remarks>
	public static System.Text.Encodings.Web.JavaScriptEncoder Rfc8259StringEncoder { get; } = new Rfc8259JavaScriptEncoder();

	/// <summary>
	/// Gets the underlying JSON writer for advanced custom converters.
	/// </summary>
	public Utf8JsonWriter Writer => writer;

	/// <inheritdoc/>
	public static object PreparePropertyName(string name) => JsonEncodedText.Encode(name, Rfc8259StringEncoder);

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
	public void WritePropertyName(scoped ReadOnlySpan<char> name, object? preparedName)
	{
		ArgumentNullException.ThrowIfNull(preparedName);
		writer.WritePropertyName((JsonEncodedText)preparedName);
	}

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
	public void WriteValue(Half value) => this.WriteFloatingPoint((float)value);

	/// <inheritdoc/>
	public void WriteValue(float value) => this.WriteFloatingPoint(value);

	/// <inheritdoc/>
	public void WriteValue(double value) => this.WriteFloatingPoint(value);

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

	/// <inheritdoc/>
	public void WriteValue(scoped ReadOnlySpan<byte> value) => writer.WriteBase64StringValue(value);

	private void WriteFloatingPoint(float value)
	{
		if (allowNamedFloatingPointValues && !float.IsFinite(value))
		{
			writer.WriteStringValue(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
		}
		else
		{
			writer.WriteNumberValue(value);
		}
	}

	private void WriteFloatingPoint(double value)
	{
		if (allowNamedFloatingPointValues && !double.IsFinite(value))
		{
			writer.WriteStringValue(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
		}
		else
		{
			writer.WriteNumberValue(value);
		}
	}
}
