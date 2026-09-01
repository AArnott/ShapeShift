// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Formats.Cbor;
using System.Numerics;

namespace ShapeShift.Cbor;

/// <summary>
/// Writes ShapeShift tokens as CBOR.
/// </summary>
/// <param name="writer">The underlying CBOR writer.</param>
public ref struct CborEncoder(CborWriter writer) : IEncoder
{
	/// <summary>
	/// Gets the underlying CBOR writer for advanced custom converters.
	/// </summary>
	public CborWriter Writer => writer;

	/// <inheritdoc/>
	public void WriteStartMap(int? propertyCount) => writer.WriteStartMap(propertyCount);

	/// <inheritdoc/>
	public void WriteEndMap() => writer.WriteEndMap();

	/// <inheritdoc/>
	public void WriteStartVector(int? itemCount) => writer.WriteStartArray(itemCount);

	/// <inheritdoc/>
	public void WriteEndVector() => writer.WriteEndArray();

	/// <inheritdoc/>
	public void WritePropertyName(scoped ReadOnlySpan<char> name) => writer.WriteTextString(name);

	/// <inheritdoc/>
	public void WriteNull() => writer.WriteNull();

	/// <inheritdoc/>
	public void WriteValue(bool value) => writer.WriteBoolean(value);

	/// <inheritdoc/>
	public void WriteValue(long value) => writer.WriteInt64(value);

	/// <inheritdoc/>
	public void WriteValue(ulong value) => writer.WriteUInt64(value);

	/// <inheritdoc/>
	public void WriteValue(Int128 value) => writer.WriteBigInteger(value);

	/// <inheritdoc/>
	public void WriteValue(UInt128 value) => writer.WriteBigInteger(value);

	/// <inheritdoc/>
	public void WriteValue(Half value) => writer.WriteHalf(value);

	/// <inheritdoc/>
	public void WriteValue(float value) => writer.WriteSingle(value);

	/// <inheritdoc/>
	public void WriteValue(double value) => writer.WriteDouble(value);

	/// <inheritdoc/>
	public void WriteValue(decimal value) => writer.WriteDecimal(value);

	/// <inheritdoc/>
	public void WriteValue(DateTime value)
	{
		writer.WriteTag(CborTag.DateTimeString);
		writer.WriteTextString(value.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
	}

	/// <inheritdoc/>
	public void WriteValue(TimeSpan value) => writer.WriteInt64(value.Ticks);

	/// <inheritdoc/>
	public void WriteValue(BigInteger value) => writer.WriteBigInteger(value);

	/// <inheritdoc/>
	public void WriteValue(string value)
	{
		ArgumentNullException.ThrowIfNull(value);
		writer.WriteTextString(value);
	}

	/// <inheritdoc/>
	public void WriteValue(scoped ReadOnlySpan<char> value) => writer.WriteTextString(value);

	/// <inheritdoc/>
	public void WriteValue(scoped ReadOnlySpan<byte> value) => writer.WriteByteString(value);
}
