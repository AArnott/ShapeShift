// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Numerics;

namespace ShapeShift.Toml;

/// <summary>
/// A ShapeShift-compatible TOML 1.0 encoder.
/// </summary>
public ref struct TomlEncoder : IEncoder
{
	private readonly TextWriter writer;
	private ContainerNode[] containers;
	private int depth;
	private string? pendingPropertyName;
	private ValueNode? root;
	private bool hasOutput;

	/// <summary>
	/// Initializes a new instance of the <see cref="TomlEncoder"/> struct.
	/// </summary>
	/// <param name="writer">The underlying text writer to which to write the TOML.</param>
	public TomlEncoder(TextWriter writer)
	{
		ArgumentNullException.ThrowIfNull(writer);
		this.writer = writer;
		this.containers = new ContainerNode[8];
	}

	/// <inheritdoc/>
	public void WriteStartMap(int? propertyCount)
	{
		MapNode map = new(propertyCount ?? 0);
		this.AddValue(map);
		this.Push(map);
	}

	/// <inheritdoc/>
	public void WriteEndMap()
	{
		this.Pop<MapNode>();
		if (this.depth == 0)
		{
			this.RenderTable((MapNode)this.root!, []);
		}
	}

	/// <inheritdoc/>
	public void WriteStartVector(int? itemCount)
	{
		VectorNode vector = new(itemCount ?? 0);
		this.AddValue(vector);
		this.Push(vector);
	}

	/// <inheritdoc/>
	public void WriteEndVector() => this.Pop<VectorNode>();

	/// <inheritdoc/>
	public void WritePropertyName(scoped ReadOnlySpan<char> name)
	{
		if (this.depth == 0 || this.containers[this.depth - 1] is not MapNode)
		{
			throw new InvalidOperationException("Property names may only be written within a TOML table.");
		}

		if (this.pendingPropertyName is not null)
		{
			throw new InvalidOperationException("The previous TOML property has no value.");
		}

		this.pendingPropertyName = name.ToString();
	}

	/// <inheritdoc/>
	public void WritePropertyName(scoped ReadOnlySpan<char> name, object? preparedName) => this.WritePropertyName(name);

	/// <inheritdoc/>
	public void WriteNull()
	{
		if (this.depth > 0 && this.containers[this.depth - 1] is MapNode && this.pendingPropertyName is not null)
		{
			this.pendingPropertyName = null;
			return;
		}

		throw new NotSupportedException("TOML has no null value.");
	}

	/// <inheritdoc/>
	public void WriteValue(bool value) => this.AddValue(new ScalarNode(value));

	/// <inheritdoc/>
	public void WriteValue(long value) => this.AddValue(new ScalarNode(value));

	/// <inheritdoc/>
	public void WriteValue(ulong value) => this.AddValue(new ScalarNode(CheckedInt64(value)));

	/// <inheritdoc/>
	public void WriteValue(Int128 value) => this.AddValue(new ScalarNode(CheckedInt64(value)));

	/// <inheritdoc/>
	public void WriteValue(UInt128 value) => this.AddValue(new ScalarNode(CheckedInt64(value)));

	/// <inheritdoc/>
	public void WriteValue(BigInteger value) => this.AddValue(new ScalarNode(CheckedInt64(value)));

	/// <inheritdoc/>
	public void WriteValue(Half value) => this.AddValue(new ScalarNode((double)value));

	/// <inheritdoc/>
	public void WriteValue(float value) => this.AddValue(new ScalarNode((double)value));

	/// <inheritdoc/>
	public void WriteValue(double value) => this.AddValue(new ScalarNode(value));

	/// <inheritdoc/>
	public void WriteValue(decimal value) => this.AddValue(new ScalarNode((double)value));

	/// <inheritdoc/>
	public void WriteValue(DateTime value) => this.AddValue(new ScalarNode(value));

	/// <inheritdoc/>
	public void WriteValue(TimeSpan value) => throw new NotSupportedException("TOML has no duration value.");

	/// <inheritdoc/>
	public void WriteValue(string? value)
	{
		if (value is null)
		{
			this.WriteNull();
			return;
		}

		this.AddValue(new ScalarNode(value));
	}

	/// <inheritdoc/>
	public void WriteValue(scoped ReadOnlySpan<char> value) => this.AddValue(new ScalarNode(value.ToString()));

	/// <inheritdoc/>
	public void WriteValue(scoped ReadOnlySpan<byte> value) => throw new NotSupportedException("TOML has no binary value.");

	private static long CheckedInt64(ulong value) => value <= long.MaxValue ? (long)value : throw IntegerOutOfRange();

	private static long CheckedInt64(Int128 value) => value >= long.MinValue && value <= long.MaxValue ? (long)value : throw IntegerOutOfRange();

	private static long CheckedInt64(UInt128 value) => value <= long.MaxValue ? (long)value : throw IntegerOutOfRange();

	private static long CheckedInt64(BigInteger value) => value >= long.MinValue && value <= long.MaxValue ? (long)value : throw IntegerOutOfRange();

	private static NotSupportedException IntegerOutOfRange() => new("TOML integers are limited to signed 64-bit values.");

	private void AddValue(ValueNode value)
	{
		if (this.depth == 0)
		{
			if (this.root is not null)
			{
				throw new InvalidOperationException("A TOML document can contain only one root value.");
			}

			if (value is not MapNode)
			{
				throw new NotSupportedException("A TOML document must have a table at its root.");
			}

			this.root = value;
			return;
		}

		ContainerNode parent = this.containers[this.depth - 1];
		if (parent is MapNode map)
		{
			string name = this.pendingPropertyName ?? throw new InvalidOperationException("A TOML table value must follow a property name.");
			map.Properties.Add(new(name, value));
			this.pendingPropertyName = null;
		}
		else
		{
			((VectorNode)parent).Items.Add(value);
		}
	}

	private void Push(ContainerNode container)
	{
		if (this.depth == this.containers.Length)
		{
			Array.Resize(ref this.containers, this.containers.Length * 2);
		}

		this.containers[this.depth++] = container;
	}

	private void Pop<TContainer>()
		where TContainer : ContainerNode
	{
		if (this.depth == 0 || this.containers[this.depth - 1] is not TContainer)
		{
			throw new InvalidOperationException("Attempted to close a TOML container that is not open.");
		}

		if (this.pendingPropertyName is not null)
		{
			throw new InvalidOperationException("A TOML property has no value.");
		}

		this.containers[--this.depth] = null!;
	}

	private void RenderTable(MapNode map, List<string> path)
	{
		for (int i = 0; i < map.Properties.Count; i++)
		{
			(string name, ValueNode value) = map.Properties[i];
			bool canUseHeader = true;
			for (int j = i + 1; canUseHeader && j < map.Properties.Count; j++)
			{
				canUseHeader = this.IsHeaderValue(map.Properties[j].Value);
			}

			if (canUseHeader && value is MapNode childMap)
			{
				path.Add(name);
				this.WriteHeader(path, tableArray: false);
				this.RenderTable(childMap, path);
				path.RemoveAt(path.Count - 1);
			}
			else if (canUseHeader && value is VectorNode { Items.Count: > 0 } vector && vector.Items.All(static item => item is MapNode))
			{
				path.Add(name);
				foreach (MapNode item in vector.Items.Cast<MapNode>())
				{
					this.WriteHeader(path, tableArray: true);
					this.RenderTable(item, path);
				}

				path.RemoveAt(path.Count - 1);
			}
			else
			{
				this.BeginLine();
				this.WriteKey(name);
				this.writer.Write(" = ");
				this.WriteInlineValue(value);
			}
		}
	}

	private void WriteHeader(List<string> path, bool tableArray)
	{
		this.BeginLine();
		this.writer.Write(tableArray ? "[[" : "[");
		for (int i = 0; i < path.Count; i++)
		{
			if (i > 0)
			{
				this.writer.Write('.');
			}

			this.WriteKey(path[i]);
		}

		this.writer.Write(tableArray ? "]]" : "]");
	}

	private void WriteInlineValue(ValueNode value)
	{
		switch (value)
		{
			case ScalarNode scalar:
				this.WriteScalar(scalar.Value);
				break;
			case MapNode map:
				this.writer.Write("{ ");
				for (int i = 0; i < map.Properties.Count; i++)
				{
					if (i > 0)
					{
						this.writer.Write(", ");
					}

					this.WriteKey(map.Properties[i].Key);
					this.writer.Write(" = ");
					this.WriteInlineValue(map.Properties[i].Value);
				}

				this.writer.Write(" }");
				break;
			case VectorNode vector:
				this.writer.Write('[');
				for (int i = 0; i < vector.Items.Count; i++)
				{
					if (i > 0)
					{
						this.writer.Write(", ");
					}

					this.WriteInlineValue(vector.Items[i]);
				}

				this.writer.Write(']');
				break;
		}
	}

	private void WriteScalar(object value)
	{
		switch (value)
		{
			case string text:
				this.WriteString(text);
				break;
			case bool boolean:
				this.writer.Write(boolean ? "true" : "false");
				break;
			case long integer:
				this.writer.Write(integer.ToString(CultureInfo.InvariantCulture));
				break;
			case double floatingPoint when double.IsNaN(floatingPoint):
				this.writer.Write("nan");
				break;
			case double floatingPoint when double.IsPositiveInfinity(floatingPoint):
				this.writer.Write("inf");
				break;
			case double floatingPoint when double.IsNegativeInfinity(floatingPoint):
				this.writer.Write("-inf");
				break;
			case double floatingPoint:
				this.writer.Write(floatingPoint.ToString("R", CultureInfo.InvariantCulture));
				break;
			case DateTime dateTime:
				this.writer.Write(dateTime.ToString("O", CultureInfo.InvariantCulture));
				break;
			default:
				throw new InvalidOperationException($"Unsupported TOML scalar {value.GetType().FullName}.");
		}
	}

	private void WriteKey(string key)
	{
		if (key.Length > 0 && key.All(static character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_'))
		{
			this.writer.Write(key);
		}
		else
		{
			this.WriteString(key);
		}
	}

	private void WriteString(string value)
	{
		this.writer.Write('"');
		foreach (char character in value)
		{
			switch (character)
			{
				case '"': this.writer.Write("\\\""); break;
				case '\\': this.writer.Write("\\\\"); break;
				case '\b': this.writer.Write("\\b"); break;
				case '\t': this.writer.Write("\\t"); break;
				case '\n': this.writer.Write("\\n"); break;
				case '\f': this.writer.Write("\\f"); break;
				case '\r': this.writer.Write("\\r"); break;
				default:
					if (character < 0x20 || character == 0x7f)
					{
						this.writer.Write($"\\u{(int)character:X4}");
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

	private void BeginLine()
	{
		if (this.hasOutput)
		{
			this.writer.WriteLine();
		}

		this.hasOutput = true;
	}

	private bool IsHeaderValue(ValueNode value)
		=> value is MapNode || (value is VectorNode { Items.Count: > 0 } vector && vector.Items.All(static item => item is MapNode));

	private abstract class ValueNode;

	private abstract class ContainerNode : ValueNode;

	private sealed class ScalarNode(object value) : ValueNode
	{
		internal object Value { get; } = value;
	}

	private sealed class MapNode(int capacity) : ContainerNode
	{
		internal List<KeyValuePair<string, ValueNode>> Properties { get; } = new(capacity);
	}

	private sealed class VectorNode(int capacity) : ContainerNode
	{
		internal List<ValueNode> Items { get; } = new(capacity);
	}
}
