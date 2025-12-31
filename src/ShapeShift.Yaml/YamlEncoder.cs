// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Numerics;

namespace ShapeShift.Yaml;

/// <summary>
/// A ShapeShift-compatible YAML encoder.
/// </summary>
/// <param name="writer">The underlying text writer to which to write the YAML.</param>
public ref struct YamlEncoder(TextWriter writer) : IEncoder
{
	private readonly TextWriter writer = writer;
	private ContainerKind[] containerKinds = new ContainerKind[8];
	private int depth;

	private int indentLevel;
	private bool atLineStart = true;
	private bool pendingPropertyValue;
	private bool pendingDashPrefix;

	private enum ContainerKind
	{
		Map,
		Vector,
	}

	/// <inheritdoc/>
	public void WriteStartVector(int? itemCount)
	{
		this.WriteStartContainer(ContainerKind.Vector);
	}

	/// <inheritdoc/>
	public void WriteEndVector()
	{
		this.WriteEndContainer(ContainerKind.Vector);
	}

	/// <inheritdoc/>
	public void WriteStartMap(int? propertyCount)
	{
		this.WriteStartContainer(ContainerKind.Map);
	}

	/// <inheritdoc/>
	public void WriteEndMap()
	{
		this.WriteEndContainer(ContainerKind.Map);
	}

	/// <inheritdoc/>
	public void WritePropertyName(scoped ReadOnlySpan<char> name)
	{
		if (this.depth == 0 || this.containerKinds[this.depth - 1] != ContainerKind.Map)
		{
			throw new InvalidOperationException("Property names may only be written within a map.");
		}

		if (!this.atLineStart && !this.pendingDashPrefix)
		{
			this.writer.WriteLine();
			this.atLineStart = true;
		}

		if (this.atLineStart)
		{
			this.WriteIndentation();
		}

		if (this.pendingDashPrefix)
		{
			// We're writing the first map entry for a sequence item, and the "- " prefix has already been emitted.
			this.pendingDashPrefix = false;
		}

		this.writer.Write(name);
		this.writer.Write(':');
		this.pendingPropertyValue = true;
		this.atLineStart = false;
	}

	/// <inheritdoc/>
	public void WriteNull()
	{
		this.WriteScalar("null".AsSpan());
	}

	/// <inheritdoc/>
	public void WriteValue(bool value)
	{
		this.WriteScalar((value ? "true" : "false").AsSpan());
	}

	/// <inheritdoc/>
	public void WriteValue(long value)
	{
		this.WriteScalar(value.ToString(CultureInfo.InvariantCulture).AsSpan());
	}

	/// <inheritdoc/>
	public void WriteValue(ulong value)
	{
		this.WriteScalar(value.ToString(CultureInfo.InvariantCulture).AsSpan());
	}

	/// <inheritdoc/>
	public void WriteValue(Int128 value)
	{
		this.WriteScalar(value.ToString(CultureInfo.InvariantCulture).AsSpan());
	}

	/// <inheritdoc/>
	public void WriteValue(UInt128 value)
	{
		this.WriteScalar(value.ToString(CultureInfo.InvariantCulture).AsSpan());
	}

	/// <inheritdoc/>
	public void WriteValue(BigInteger value)
	{
		this.WriteScalar(value.ToString(CultureInfo.InvariantCulture).AsSpan());
	}

	/// <inheritdoc/>
	public void WriteValue(Half value)
	{
		this.WriteScalar(value.ToString(CultureInfo.InvariantCulture).AsSpan());
	}

	/// <inheritdoc/>
	public void WriteValue(float value)
	{
		this.WriteScalar(value.ToString(CultureInfo.InvariantCulture).AsSpan());
	}

	/// <inheritdoc/>
	public void WriteValue(double value)
	{
		this.WriteScalar(value.ToString(CultureInfo.InvariantCulture).AsSpan());
	}

	/// <inheritdoc/>
	public void WriteValue(decimal value)
	{
		this.WriteScalar(value.ToString(CultureInfo.InvariantCulture).AsSpan());
	}

	/// <inheritdoc/>
	public void WriteValue(DateTime value)
	{
		this.WriteScalar(value.ToString("o", CultureInfo.InvariantCulture).AsSpan());
	}

	/// <inheritdoc/>
	public void WriteValue(TimeSpan value)
	{
		this.WriteScalar(value.ToString("c", CultureInfo.InvariantCulture).AsSpan());
	}

	/// <inheritdoc/>
	public void WriteValue(string? value)
	{
		if (value is null)
		{
			this.WriteNull();
			return;
		}

		if (this.depth == 0 && !this.pendingPropertyValue)
		{
			// Top-level scalar should not include trailing newline.
			this.WriteStringScalar(value, includeTrailingNewline: false);
			this.atLineStart = false;
			return;
		}

		this.WriteStringScalar(value, includeTrailingNewline: true);
	}

	/// <inheritdoc/>
	public void WriteValue(scoped ReadOnlySpan<char> value)
	{
		if (this.depth == 0 && !this.pendingPropertyValue)
		{
			// Top-level scalar should not include trailing newline.
			this.WriteStringScalar(value.ToString(), includeTrailingNewline: false);
			this.atLineStart = false;
			return;
		}

		this.WriteStringScalar(value.ToString(), includeTrailingNewline: true);
	}

	private void WriteStartContainer(ContainerKind kind)
	{
		if (this.pendingPropertyValue)
		{
			this.writer.WriteLine();
			this.pendingPropertyValue = false;
			this.atLineStart = true;
		}
		else if (this.depth > 0 && this.containerKinds[this.depth - 1] == ContainerKind.Vector && this.atLineStart)
		{
			// Start a complex sequence item.
			this.WriteIndentation();
			this.writer.Write("- ");
			this.pendingDashPrefix = true;
			this.atLineStart = false;
		}

		this.Push(kind);
		this.indentLevel++;
	}

	private void WriteEndContainer(ContainerKind kind)
	{
		if (this.depth == 0 || this.containerKinds[this.depth - 1] != kind)
		{
			throw new InvalidOperationException("Attempted to close a YAML container that is not open.");
		}

		// YAML has no explicit end token; we just adjust indentation.
		this.Pop();
		this.indentLevel = Math.Max(0, this.indentLevel - 1);
		this.pendingPropertyValue = false;
		this.pendingDashPrefix = false;
		this.atLineStart = true;
	}

	private void WriteScalar(ReadOnlySpan<char> scalar)
	{
		if (this.pendingPropertyValue)
		{
			this.writer.Write(' ');
			this.writer.Write(scalar);
			this.writer.WriteLine();
			this.pendingPropertyValue = false;
			this.atLineStart = true;
		}
		else if (this.depth > 0 && this.containerKinds[this.depth - 1] == ContainerKind.Vector)
		{
			if (!this.atLineStart)
			{
				this.writer.WriteLine();
				this.atLineStart = true;
			}

			this.WriteIndentation();
			this.writer.Write("- ");
			this.writer.Write(scalar);
			this.writer.WriteLine();
			this.atLineStart = true;
		}
		else
		{
			if (this.atLineStart)
			{
				this.WriteIndentation();
			}

			this.writer.Write(scalar);
			this.writer.WriteLine();
			this.atLineStart = true;
		}
	}

	private void WriteStringScalar(string value, bool includeTrailingNewline)
	{
		ReadOnlySpan<char> span = value.AsSpan();
		bool needsQuotes = span.IsEmpty || this.RequiresQuoting(span);
		if (!needsQuotes)
		{
			this.WriteStringAsScalar(span, includeTrailingNewline);
			return;
		}

		// Minimal double-quoted scalar.
		string escaped = value
			.Replace("\\", "\\\\", StringComparison.Ordinal)
			.Replace("\"", "\\\"", StringComparison.Ordinal)
			.Replace("\r", "\\r", StringComparison.Ordinal)
			.Replace("\n", "\\n", StringComparison.Ordinal);

		this.WriteStringAsScalar(("\"" + escaped + "\"").AsSpan(), includeTrailingNewline);
	}

	private void WriteStringAsScalar(ReadOnlySpan<char> scalar, bool includeTrailingNewline)
	{
		if (this.pendingPropertyValue)
		{
			this.writer.Write(' ');
			this.writer.Write(scalar);
			if (includeTrailingNewline)
			{
				this.writer.WriteLine();
				this.atLineStart = true;
			}

			this.pendingPropertyValue = false;
			return;
		}

		if (this.depth > 0 && this.containerKinds[this.depth - 1] == ContainerKind.Vector)
		{
			if (!this.atLineStart)
			{
				this.writer.WriteLine();
				this.atLineStart = true;
			}

			this.WriteIndentation();
			this.writer.Write("- ");
			this.writer.Write(scalar);
			if (includeTrailingNewline)
			{
				this.writer.WriteLine();
				this.atLineStart = true;
			}

			return;
		}

		if (this.atLineStart)
		{
			this.WriteIndentation();
		}

		this.writer.Write(scalar);
		if (includeTrailingNewline)
		{
			this.writer.WriteLine();
			this.atLineStart = true;
		}
	}

	private bool RequiresQuoting(ReadOnlySpan<char> value)
	{
		if (value.IndexOfAny('\r', '\n') >= 0)
		{
			return true;
		}

		if (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]))
		{
			return true;
		}

		// Avoid ambiguous YAML indicators.
		char first = value[0];
		if (first is '-' or ':' or '?' or '#' or '!' or '&' or '*' or '|' or '>' or '\'' or '"' or '%' or '@' or '`')
		{
			return true;
		}

		// Avoid comment-start sequences.
		int hash = value.IndexOf('#');
		if (hash >= 0)
		{
			if (hash == 0 || char.IsWhiteSpace(value[hash - 1]))
			{
				return true;
			}
		}

		return false;
	}

	private void WriteIndentation()
	{
		int spaces = this.indentLevel * 2;
		for (int i = 0; i < spaces; i++)
		{
			this.writer.Write(' ');
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

	private void Pop() => this.depth--;
}
