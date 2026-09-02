// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Numerics;
using System.Text;

namespace ShapeShift.Protobuf;

/// <summary>
/// Encodes the ShapeShift token stream to a protobuf-style binary wire representation.
/// </summary>
/// <param name="stream">The destination stream.</param>
public ref struct ProtobufEncoder(Stream stream) : IEncoder
{
	private readonly Stream stream = stream ?? throw new ArgumentNullException(nameof(stream));
	private ContainerKind[] containerKinds = new ContainerKind[8];
	private int depth;

	private enum ContainerKind
	{
		Map,
		Vector,
	}

	/// <inheritdoc/>
	public static object? PreparePropertyName(string name) => name;

	/// <inheritdoc/>
	public void WriteStartMap(int? propertyCount)
	{
		this.WriteTag(0x70);
		this.WriteCount(propertyCount ?? 0);
		this.Push(ContainerKind.Map);
	}

	/// <inheritdoc/>
	public void WriteEndMap()
	{
		this.AssertCurrentContainer(ContainerKind.Map);
		this.Pop();
		this.WriteTag(0x71);
	}

	/// <inheritdoc/>
	public void WriteStartVector(int? itemCount)
	{
		this.WriteTag(0x80);
		this.WriteCount(itemCount ?? 0);
		this.Push(ContainerKind.Vector);
	}

	/// <inheritdoc/>
	public void WriteEndVector()
	{
		this.AssertCurrentContainer(ContainerKind.Vector);
		this.Pop();
		this.WriteTag(0x81);
	}

	/// <inheritdoc/>
	public void WritePropertyName(scoped ReadOnlySpan<char> name)
	{
		this.WriteStringPayload(0x60, name);
	}

	/// <inheritdoc/>
	public void WritePropertyName(scoped ReadOnlySpan<char> name, object? preparedName)
	{
		string propertyName = preparedName as string ?? name.ToString();
		this.WriteStringPayload(0x60, propertyName);
	}

	/// <inheritdoc/>
	public void WriteNull()
	{
		this.WriteTag(0x50);
	}

	/// <inheritdoc/>
	public void WriteValue(bool value)
	{
		this.WriteTag(0x40);
		this.stream.WriteByte((byte)(value ? 1 : 0));
	}

	/// <inheritdoc/>
	public void WriteValue(long value)
		=> this.WriteNumericPayload(0x20, value.ToString(CultureInfo.InvariantCulture));

	/// <inheritdoc/>
	public void WriteValue(ulong value)
		=> this.WriteNumericPayload(0x20, value.ToString(CultureInfo.InvariantCulture));

	/// <inheritdoc/>
	public void WriteValue(Int128 value)
		=> this.WriteNumericPayload(0x20, value.ToString(CultureInfo.InvariantCulture));

	/// <inheritdoc/>
	public void WriteValue(UInt128 value)
		=> this.WriteNumericPayload(0x20, value.ToString(CultureInfo.InvariantCulture));

	/// <inheritdoc/>
	public void WriteValue(Half value)
		=> this.WriteNumericPayload(0x21, value.ToString(CultureInfo.InvariantCulture));

	/// <inheritdoc/>
	public void WriteValue(float value)
		=> this.WriteNumericPayload(0x21, value.ToString(CultureInfo.InvariantCulture));

	/// <inheritdoc/>
	public void WriteValue(double value)
		=> this.WriteNumericPayload(0x21, value.ToString(CultureInfo.InvariantCulture));

	/// <inheritdoc/>
	public void WriteValue(decimal value)
		=> this.WriteNumericPayload(0x22, value.ToString(CultureInfo.InvariantCulture));

	/// <inheritdoc/>
	public void WriteValue(DateTime value)
		=> this.WriteStringPayload(0x30, value.ToString("O", CultureInfo.InvariantCulture));

	/// <inheritdoc/>
	public void WriteValue(TimeSpan value)
		=> this.WriteStringPayload(0x30, value.ToString("c", CultureInfo.InvariantCulture));

	/// <inheritdoc/>
	public void WriteValue(BigInteger value)
		=> this.WriteNumericPayload(0x20, value.ToString(CultureInfo.InvariantCulture));

	/// <inheritdoc/>
	public void WriteValue(string value)
	{
		if (value is null)
		{
			this.WriteNull();
			return;
		}

		this.WriteStringPayload(0x30, value);
	}

	/// <inheritdoc/>
	public void WriteValue(scoped ReadOnlySpan<char> value)
		=> this.WriteStringPayload(0x30, value);

	/// <inheritdoc/>
	public void WriteValue(scoped ReadOnlySpan<byte> value)
	{
		this.WriteTag(0x31);
		byte[] bytes = value.ToArray();
		this.WriteLength(bytes.Length);
		this.stream.Write(bytes, 0, bytes.Length);
	}

	private static void ThrowBadContainer(ContainerKind expected, ContainerKind actual)
		=> throw new InvalidOperationException($"Expected to close a {expected} container, but found a {actual} container.");

	private void AssertCurrentContainer(ContainerKind expected)
	{
		if (this.depth == 0 || this.containerKinds[this.depth - 1] != expected)
		{
			if (this.depth > 0)
			{
				ThrowBadContainer(expected, this.containerKinds[this.depth - 1]);
			}

			throw new InvalidOperationException($"Attempted to close a {expected} container when none is open.");
		}
	}

	private void Push(ContainerKind kind)
	{
		if (this.depth == this.containerKinds.Length)
		{
			Array.Resize(ref this.containerKinds, this.containerKinds.Length * 2);
		}

		this.containerKinds[this.depth++] = kind;
	}

	private void Pop()
	{
		if (this.depth == 0)
		{
			throw new InvalidOperationException("Attempted to pop a container when none are open.");
		}

		this.depth--;
	}

	private void WriteTag(byte tag)
		=> this.stream.WriteByte(tag);

	private void WriteCount(int value)
	{
		this.WriteVarint((uint)value);
	}

	private void WriteLength(int length)
	{
		this.WriteVarint((uint)length);
	}

	private void WriteVarint(uint value)
	{
		while (value >= 0x80)
		{
			this.stream.WriteByte((byte)((value & 0x7F) | 0x80));
			value >>= 7;
		}

		this.stream.WriteByte((byte)value);
	}

	private void WriteNumericPayload(byte tag, string text)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(text);
		this.WriteTag(tag);
		this.WriteLength(bytes.Length);
		this.stream.Write(bytes, 0, bytes.Length);
	}

	private void WriteStringPayload(byte tag, string value)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(value);
		this.WriteTag(tag);
		this.WriteLength(bytes.Length);
		this.stream.Write(bytes, 0, bytes.Length);
	}

	private void WriteStringPayload(byte tag, scoped ReadOnlySpan<char> value)
	{
		string text = value.ToString();
		this.WriteStringPayload(tag, text);
	}
}
