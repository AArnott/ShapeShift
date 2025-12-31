// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Numerics;

namespace ShapeShift.Taml;

/// <summary>
/// A ShapeShift-compatible TAML encoder.
/// </summary>
/// <param name="writer">The underlying text writer to which to write the TAML.</param>
public ref struct TamlEncoder(TextWriter writer) : IEncoder
{
	private readonly TextWriter writer = writer;
	private ContainerKind[] containerKinds = new ContainerKind[8];
	private int depth;

	private int indentLevel;
	private bool atLineStart = true;
	private bool pendingPropertyValue;
	private bool firstItemInContainer = true;

	private enum ContainerKind
	{
		Map,
		Vector,
	}

	public void WriteStartVector(int? itemCount)
	{
		this.WriteStartContainer(ContainerKind.Vector);
	}

	public void WriteEndVector()
	{
		this.WriteEndContainer(ContainerKind.Vector);
	}

	public void WriteStartMap(int? propertyCount)
	{
		this.WriteStartContainer(ContainerKind.Map);
	}

	public void WriteEndMap()
	{
		this.WriteEndContainer(ContainerKind.Map);
	}

	public void WritePropertyName(scoped ReadOnlySpan<char> name)
	{
		if (this.depth == 0 || this.containerKinds[this.depth - 1] != ContainerKind.Map)
		{
			throw new InvalidOperationException("Property names may only be written within a map.");
		}

		if (!this.atLineStart)
		{
			this.writer.WriteLine();
			this.atLineStart = true;
		}

		this.WriteIndentation();
		this.writer.Write(name);
		this.pendingPropertyValue = true;
		this.atLineStart = false;
		this.firstItemInContainer = false;
	}

	public void WriteNull()
	{
		this.WriteScalar("~".AsSpan());
	}

	public void WriteValue(bool value)
	{
		this.WriteScalar((value ? "true" : "false").AsSpan());
	}

	public void WriteValue(long value)
	{
		this.WriteScalar(value.ToString(CultureInfo.InvariantCulture).AsSpan());
	}

	public void WriteValue(ulong value)
	{
		this.WriteScalar(value.ToString(CultureInfo.InvariantCulture).AsSpan());
	}

	public void WriteValue(Int128 value)
	{
		this.WriteScalar(value.ToString(CultureInfo.InvariantCulture).AsSpan());
	}

	public void WriteValue(UInt128 value)
	{
		this.WriteScalar(value.ToString(CultureInfo.InvariantCulture).AsSpan());
	}

	public void WriteValue(BigInteger value)
	{
		this.WriteScalar(value.ToString(CultureInfo.InvariantCulture).AsSpan());
	}

	public void WriteValue(Half value)
	{
		this.WriteScalar(value.ToString(CultureInfo.InvariantCulture).AsSpan());
	}

	public void WriteValue(float value)
	{
		this.WriteScalar(value.ToString(CultureInfo.InvariantCulture).AsSpan());
	}

	public void WriteValue(double value)
	{
		this.WriteScalar(value.ToString(CultureInfo.InvariantCulture).AsSpan());
	}

	public void WriteValue(decimal value)
	{
		this.WriteScalar(value.ToString(CultureInfo.InvariantCulture).AsSpan());
	}

	public void WriteValue(DateTime value)
	{
		this.WriteScalar(value.ToString("o", CultureInfo.InvariantCulture).AsSpan());
	}

	public void WriteValue(TimeSpan value)
	{
		this.WriteScalar(value.ToString("c", CultureInfo.InvariantCulture).AsSpan());
	}

	public void WriteValue(string? value)
	{
		if (value is null)
		{
			this.WriteNull();
			return;
		}

		if (this.depth == 0 && !this.pendingPropertyValue)
		{
			this.WriteStringScalar(value, includeTrailingNewline: false);
			this.atLineStart = false;
			return;
		}

		this.WriteStringScalar(value, includeTrailingNewline: true);
	}

	public void WriteValue(scoped ReadOnlySpan<char> value)
	{
		if (this.depth == 0 && !this.pendingPropertyValue)
		{
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
		else if (this.depth > 0 && this.containerKinds[this.depth - 1] == ContainerKind.Vector)
		{
			if (!this.atLineStart)
			{
				this.writer.WriteLine();
				this.atLineStart = true;
			}

			if (!this.firstItemInContainer && kind == ContainerKind.Map)
			{
				this.writer.WriteLine();
			}
		}

		this.Push(kind);
		this.indentLevel++;
		this.firstItemInContainer = true;
	}

	private void WriteEndContainer(ContainerKind kind)
	{
		if (this.depth == 0 || this.containerKinds[this.depth - 1] != kind)
		{
			throw new InvalidOperationException("Attempted to close a TAML container that is not open.");
		}

		this.Pop();
		this.indentLevel = Math.Max(0, this.indentLevel - 1);
		this.pendingPropertyValue = false;
		this.atLineStart = true;
		this.firstItemInContainer = false;
	}

	private void WriteScalar(ReadOnlySpan<char> scalar)
	{
		if (this.pendingPropertyValue)
		{
			this.writer.Write('\t');
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
			this.writer.Write(scalar);
			this.writer.WriteLine();
			this.atLineStart = true;
			this.firstItemInContainer = false;
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
		bool needsQuotes = this.RequiresQuoting(span);
		if (!needsQuotes)
		{
			this.WriteStringAsScalar(span, includeTrailingNewline);
			return;
		}

		string escaped = value
			.Replace("\\", "\\\\", StringComparison.Ordinal)
			.Replace("\"", "\\\"", StringComparison.Ordinal)
			.Replace("\r", "\\r", StringComparison.Ordinal)
			.Replace("\n", "\\n", StringComparison.Ordinal)
			.Replace("\t", "\\t", StringComparison.Ordinal);

		this.WriteStringAsScalar(("\"" + escaped + "\"").AsSpan(), includeTrailingNewline);
	}

	private void WriteStringAsScalar(ReadOnlySpan<char> scalar, bool includeTrailingNewline)
	{
		if (this.pendingPropertyValue)
		{
			this.writer.Write('\t');
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
			this.writer.Write(scalar);
			if (includeTrailingNewline)
			{
				this.writer.WriteLine();
				this.atLineStart = true;
			}

			this.firstItemInContainer = false;
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
		if (value.IsEmpty)
		{
			return true;
		}

		if (value.IndexOfAny('\r', '\n', '\t') >= 0)
		{
			return true;
		}

		if (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]))
		{
			return true;
		}

		if (value.Equals("~".AsSpan(), StringComparison.Ordinal) ||
			value.Equals("null".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
			value.Equals("true".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
			value.Equals("false".AsSpan(), StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		if (value.Equals("\"\"".AsSpan(), StringComparison.Ordinal))
		{
			return false;
		}

		return false;
	}

	private void WriteIndentation()
	{
		for (int i = 0; i < this.indentLevel; i++)
		{
			this.writer.Write('\t');
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
