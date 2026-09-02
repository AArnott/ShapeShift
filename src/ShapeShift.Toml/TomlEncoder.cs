// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Numerics;

namespace ShapeShift.Toml;

/// <summary>
/// A ShapeShift-compatible TOML encoder.
/// </summary>
/// <param name="writer">The underlying text writer to which to write the TOML.</param>
public ref struct TomlEncoder(TextWriter writer) : IEncoder
{
	private readonly TextWriter writer = writer;
	private ContainerState[] containers = new ContainerState[8];
	private int depth;
	private bool pendingPropertyValue;

	private enum ContainerKind
	{
		Map,
		Vector,
	}

	private struct ContainerState(ContainerKind kind, bool inline)
	{
		internal ContainerKind Kind = kind;
		internal bool Inline = inline;
		internal int ItemCount;
	}

	/// <inheritdoc/>
	public void WriteStartMap(int? propertyCount)
	{
		bool inline = this.depth > 0;
		this.BeforeValue();
		if (inline)
		{
			this.writer.Write("{ ");
		}

		this.Push(new(ContainerKind.Map, inline));
	}

	/// <inheritdoc/>
	public void WriteEndMap()
	{
		ContainerState state = this.Pop(ContainerKind.Map);
		if (this.pendingPropertyValue)
		{
			throw new InvalidOperationException("A TOML property has no value.");
		}

		if (state.Inline)
		{
			this.writer.Write(state.ItemCount == 0 ? "{}" : " }");
		}
		else if (state.ItemCount == 0)
		{
			this.writer.Write("{}");
		}
	}

	/// <inheritdoc/>
	public void WriteStartVector(int? itemCount)
	{
		this.BeforeValue();
		this.writer.Write('[');
		this.Push(new(ContainerKind.Vector, inline: true));
	}

	/// <inheritdoc/>
	public void WriteEndVector()
	{
		this.Pop(ContainerKind.Vector);
		this.writer.Write(']');
	}

	/// <inheritdoc/>
	public void WritePropertyName(scoped ReadOnlySpan<char> name)
	{
		if (this.depth == 0 || this.containers[this.depth - 1].Kind != ContainerKind.Map)
		{
			throw new InvalidOperationException("Property names may only be written within a map.");
		}

		if (this.pendingPropertyValue)
		{
			throw new InvalidOperationException("The previous TOML property has no value.");
		}

		ref ContainerState map = ref this.containers[this.depth - 1];
		if (map.ItemCount > 0)
		{
			this.writer.Write(map.Inline ? ", " : Environment.NewLine);
		}

		this.WriteStringKey(name);
		this.writer.Write(" = ");
		map.ItemCount++;
		this.pendingPropertyValue = true;
	}

	/// <inheritdoc/>
	public void WriteNull() => this.WriteScalar("null");

	/// <inheritdoc/>
	public void WriteValue(bool value) => this.WriteScalar(value ? "true" : "false");

	/// <inheritdoc/>
	public void WriteValue(long value) => this.WriteScalar(value.ToString(CultureInfo.InvariantCulture));

	/// <inheritdoc/>
	public void WriteValue(ulong value) => this.WriteScalar(value.ToString(CultureInfo.InvariantCulture));

	/// <inheritdoc/>
	public void WriteValue(Int128 value) => this.WriteScalar(value.ToString(CultureInfo.InvariantCulture));

	/// <inheritdoc/>
	public void WriteValue(UInt128 value) => this.WriteScalar(value.ToString(CultureInfo.InvariantCulture));

	/// <inheritdoc/>
	public void WriteValue(BigInteger value) => this.WriteScalar(value.ToString(CultureInfo.InvariantCulture));

	/// <inheritdoc/>
	public void WriteValue(Half value) => this.WriteScalar(value.ToString("G", CultureInfo.InvariantCulture));

	/// <inheritdoc/>
	public void WriteValue(float value) => this.WriteFloatingPoint(value, float.IsNaN(value), float.IsPositiveInfinity(value), float.IsNegativeInfinity(value));

	/// <inheritdoc/>
	public void WriteValue(double value) => this.WriteFloatingPoint(value, double.IsNaN(value), double.IsPositiveInfinity(value), double.IsNegativeInfinity(value));

	/// <inheritdoc/>
	public void WriteValue(decimal value) => this.WriteScalar(value.ToString("G", CultureInfo.InvariantCulture));

	/// <inheritdoc/>
	public void WriteValue(DateTime value) => this.WriteScalar(value.ToString("O", CultureInfo.InvariantCulture));

	/// <inheritdoc/>
	public void WriteValue(TimeSpan value)
	{
		this.BeforeValue();
		this.WriteStringLiteral(value.ToString("c", CultureInfo.InvariantCulture));
	}

	/// <inheritdoc/>
	public void WriteValue(string? value)
	{
		if (value is null)
		{
			this.WriteNull();
			return;
		}

		this.BeforeValue();
		this.WriteStringLiteral(value);
	}

	/// <inheritdoc/>
	public void WriteValue(scoped ReadOnlySpan<char> value)
	{
		this.BeforeValue();
		this.WriteStringLiteral(value);
	}

	/// <inheritdoc/>
	public void WriteValue(scoped ReadOnlySpan<byte> value) => throw new NotSupportedException("TOML binary values are not supported.");

	private void WriteFloatingPoint<T>(T value, bool isNaN, bool isPositiveInfinity, bool isNegativeInfinity)
		where T : IFormattable
	{
		this.WriteScalar(isNaN ? "nan" : isPositiveInfinity ? "inf" : isNegativeInfinity ? "-inf" : value.ToString("G", CultureInfo.InvariantCulture));
	}

	private void WriteScalar(string scalar)
	{
		this.BeforeValue();
		this.writer.Write(scalar);
	}

	private void BeforeValue()
	{
		if (this.pendingPropertyValue)
		{
			this.pendingPropertyValue = false;
			return;
		}

		if (this.depth > 0)
		{
			ref ContainerState container = ref this.containers[this.depth - 1];
			if (container.Kind == ContainerKind.Map)
			{
				throw new InvalidOperationException("A TOML map value must follow a property name.");
			}

			if (container.ItemCount > 0)
			{
				this.writer.Write(", ");
			}

			container.ItemCount++;
		}
	}

	private void Push(ContainerState state)
	{
		if (this.depth == this.containers.Length)
		{
			Array.Resize(ref this.containers, this.containers.Length * 2);
		}

		this.containers[this.depth++] = state;
	}

	private ContainerState Pop(ContainerKind expectedKind)
	{
		if (this.depth == 0 || this.containers[this.depth - 1].Kind != expectedKind)
		{
			throw new InvalidOperationException("Attempted to close a TOML container that is not open.");
		}

		return this.containers[--this.depth];
	}

	private void WriteStringKey(scoped ReadOnlySpan<char> key)
	{
		bool isBareKey = key.Length > 0;
		foreach (char character in key)
		{
			if (!IsBareKeyCharacter(character))
			{
				isBareKey = false;
				break;
			}
		}

		if (isBareKey)
		{
			this.writer.Write(key);
		}
		else
		{
			this.WriteStringLiteral(key);
		}
	}

	private void WriteStringLiteral(scoped ReadOnlySpan<char> value)
	{
		this.writer.Write('"');
		foreach (char character in value)
		{
			switch (character)
			{
				case '"': this.writer.Write("\\\""); break;
				case '\\': this.writer.Write("\\\\"); break;
				case '\b': this.writer.Write("\\b"); break;
				case '\f': this.writer.Write("\\f"); break;
				case '\n': this.writer.Write("\\n"); break;
				case '\r': this.writer.Write("\\r"); break;
				case '\t': this.writer.Write("\\t"); break;
				default:
					if (character < 0x20 || character == 0x7f)
					{
						this.writer.Write("\\u");
						this.writer.Write(((int)character).ToString("X4", CultureInfo.InvariantCulture));
					}
					else
					{
						this.writer.Write(character);
					}

					break;
			}
		}

		this.writer.Write('"');
	}

	private static bool IsBareKeyCharacter(char character)
		=> (character >= 'a' && character <= 'z') ||
			(character >= 'A' && character <= 'Z') ||
			(character >= '0' && character <= '9') ||
			character is '-' or '_';
}
