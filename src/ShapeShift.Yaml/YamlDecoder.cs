// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

namespace ShapeShift.Yaml;

/// <summary>
/// A ShapeShift-compatible YAML decoder.
/// </summary>
/// <param name="reader">The underlying text reader from which to get the YAML.</param>
public ref struct YamlDecoder(TextReader reader) : IDecoder
{
	private const NumberStyles FloatingPointStyle = NumberStyles.Float | NumberStyles.AllowHexSpecifier;
	private const NumberStyles IntegerPointStyle = NumberStyles.Integer | NumberStyles.AllowHexSpecifier;

	private readonly string text = reader.ReadToEnd();
	private int scanOffset;

	private ContainerFrame[] containers = new ContainerFrame[8];
	private int containerDepth;

	private Token[] queuedTokens = new Token[4];
	private int queuedTokenCount;

	private Token bufferedToken;
	private bool hasBufferedToken;

	private bool rootContainerEmitted;

	private enum ContainerKind
	{
		Map,
		Vector,
	}

	/// <inheritdoc/>
	public TokenType NextTokenType
	{
		get
		{
			this.EnsureBufferedToken();
			return this.bufferedToken.Type;
		}
	}

	/// <inheritdoc/>
	public bool TryReadNull()
	{
		return this.NextTokenType == TokenType.Null;
	}

	/// <inheritdoc/>
	public int? ReadStartMap()
	{
		this.EnsureBufferedToken();
		if (this.bufferedToken.Type != TokenType.StartMap)
		{
			throw new DecoderException($"Expected a {TokenType.StartMap} token but instead got {this.bufferedToken.Type}.");
		}

		int indent = this.bufferedToken.ContainerIndent;
		this.ConsumeBufferedToken();
		this.Push(ContainerKind.Map, indent);
		return null;
	}

	/// <inheritdoc/>
	public void ReadEndMap()
	{
		this.ReadToken(TokenType.EndMap);
	}

	/// <inheritdoc/>
	public int? ReadStartVector()
	{
		this.EnsureBufferedToken();
		if (this.bufferedToken.Type != TokenType.StartVector)
		{
			throw new DecoderException($"Expected a {TokenType.StartVector} token but instead got {this.bufferedToken.Type}.");
		}

		int indent = this.bufferedToken.ContainerIndent;
		this.ConsumeBufferedToken();
		this.Push(ContainerKind.Vector, indent);
		return null;
	}

	/// <inheritdoc/>
	public void ReadEndVector()
	{
		this.ReadToken(TokenType.EndVector);
	}

	/// <inheritdoc/>
	public ReadOnlySpan<char> ReadPropertyName()
	{
		return this.ReadToken(TokenType.PropertyName);
	}

	/// <inheritdoc/>
	public void Skip()
	{
		this.EnsureBufferedToken();
		switch (this.bufferedToken.Type)
		{
			case TokenType.StartMap:
				this.ReadStartMap();
				while (this.NextTokenType != TokenType.EndMap)
				{
					// Skip property name then value.
					if (this.NextTokenType == TokenType.PropertyName)
					{
						_ = this.ReadPropertyName();
					}

					this.Skip();
				}

				this.ReadEndMap();
				break;
			case TokenType.StartVector:
				this.ReadStartVector();
				while (this.NextTokenType != TokenType.EndVector)
				{
					this.Skip();
				}

				this.ReadEndVector();
				break;
			case TokenType.EndMap:
			case TokenType.EndVector:
				// Let caller consume structural end tokens.
				return;
			default:
				this.ConsumeBufferedToken();
				break;
		}
	}

	public void ReadNull()
	{
		this.ReadToken(TokenType.Null);
	}

	/// <inheritdoc/>
	public bool ReadBoolean()
	{
		ReadOnlySpan<char> token = this.ReadToken(TokenType.String);
		return token switch
		{
			"false" => false,
			"true" => true,
			_ => throw new DecoderException($"Invalid boolean value: {token.ToString()}."),
		};
	}

	/// <inheritdoc/>
	public long ReadInt64()
	{
		ReadOnlySpan<char> token = this.ReadToken(TokenType.Number);
		if (!long.TryParse(token, IntegerPointStyle, CultureInfo.InvariantCulture, out long value))
		{
			throw new DecoderException($"Invalid integer value: {token.ToString()}.");
		}

		return value;
	}

	/// <inheritdoc/>
	public ulong ReadUInt64()
	{
		ReadOnlySpan<char> token = this.ReadToken(TokenType.Number);
		if (!ulong.TryParse(token, IntegerPointStyle, CultureInfo.InvariantCulture, out ulong value))
		{
			throw new DecoderException($"Invalid integer value: {token.ToString()}.");
		}

		return value;
	}

	/// <inheritdoc/>
	public Half ReadHalf()
	{
		ReadOnlySpan<char> token = this.ReadToken(TokenType.Number);
		if (!Half.TryParse(token, FloatingPointStyle, CultureInfo.InvariantCulture, out Half value))
		{
			throw new DecoderException($"Invalid floating point value: {token.ToString()}.");
		}

		return value;
	}

	/// <inheritdoc/>
	public float ReadSingle()
	{
		ReadOnlySpan<char> token = this.ReadToken(TokenType.Number);
		if (!float.TryParse(token, FloatingPointStyle, CultureInfo.InvariantCulture, out float value))
		{
			throw new DecoderException($"Invalid floating point value: {token.ToString()}.");
		}

		return value;
	}

	/// <inheritdoc/>
	public double ReadDouble()
	{
		ReadOnlySpan<char> token = this.ReadToken(TokenType.Number);
		if (!double.TryParse(token, FloatingPointStyle, CultureInfo.InvariantCulture, out double value))
		{
			throw new DecoderException($"Invalid floating point value: {token.ToString()}.");
		}

		return value;
	}

	/// <inheritdoc/>
	public decimal ReadDecimal()
	{
		ReadOnlySpan<char> token = this.ReadToken(TokenType.Number);
		if (!decimal.TryParse(token, FloatingPointStyle, CultureInfo.InvariantCulture, out decimal value))
		{
			throw new DecoderException($"Invalid floating point value: {token.ToString()}.");
		}

		return value;
	}

	/// <inheritdoc/>
	public DateTime ReadDateTime()
	{
		ReadOnlySpan<char> token = this.ReadToken(TokenType.Number);
		if (!DateTime.TryParse(token, CultureInfo.InvariantCulture, out DateTime value))
		{
			throw new DecoderException($"Invalid DateTime value: {token.ToString()}.");
		}

		return value;
	}

	/// <inheritdoc/>
	public TimeSpan ReadTimeSpan()
	{
		ReadOnlySpan<char> token = this.ReadToken(TokenType.String);
		if (!TimeSpan.TryParse(token, CultureInfo.InvariantCulture, out TimeSpan value))
		{
			throw new DecoderException($"Invalid TimeSpan value: {token.ToString()}.");
		}

		return value;
	}

	/// <inheritdoc/>
	public string ReadString()
	{
		ReadOnlySpan<char> token = this.ReadToken(TokenType.String);
		return this.UnescapeString(token);
	}

	public ReadOnlySpan<char> ReadCharSpan()
	{
		ReadOnlySpan<char> token = this.ReadToken(TokenType.String);
		return this.UnescapeString(token);
	}

	private ReadOnlySpan<char> ReadToken(TokenType expectedType)
	{
		this.EnsureBufferedToken();
		if (this.bufferedToken.Type != expectedType)
		{
			throw new DecoderException($"Expected a {expectedType} token but instead got {this.bufferedToken.Type}.");
		}

		ReadOnlySpan<char> span = this.bufferedToken.IsSynthetic ? ReadOnlySpan<char>.Empty : this.text.AsSpan(this.bufferedToken.Start, this.bufferedToken.Length);
		this.ConsumeBufferedToken();
		return span;
	}

	private void EnsureBufferedToken()
	{
		if (this.hasBufferedToken)
		{
			return;
		}

		if (this.queuedTokenCount > 0)
		{
			this.bufferedToken = this.DequeueToken();
			this.hasBufferedToken = true;
			return;
		}

		this.EnqueueNextTokens();
		if (this.queuedTokenCount == 0)
		{
			throw new DecoderException("Unexpected end of YAML input.");
		}

		this.bufferedToken = this.DequeueToken();
		this.hasBufferedToken = true;
	}

	private void ConsumeBufferedToken()
	{
		this.hasBufferedToken = false;
		this.bufferedToken = default;
	}

	private Token DequeueToken()
	{
		Token t = this.queuedTokens[0];
		for (int i = 1; i < this.queuedTokenCount; i++)
		{
			this.queuedTokens[i - 1] = this.queuedTokens[i];
		}

		this.queuedTokenCount--;
		return t;
	}

	private void Enqueue(Token token)
	{
		if (this.queuedTokenCount == this.queuedTokens.Length)
		{
			Array.Resize(ref this.queuedTokens, this.queuedTokens.Length * 2);
		}

		this.queuedTokens[this.queuedTokenCount++] = token;
	}

	private void EnqueueSyntheticStart(TokenType startType, int indent)
	{
		this.Enqueue(new Token
		{
			Type = startType,
			IsSynthetic = true,
			ContainerIndent = indent,
		});
	}

	private void EnqueueSyntheticEndForTopContainer()
	{
		if (this.containerDepth == 0)
		{
			return;
		}

		TokenType endType = this.containers[this.containerDepth - 1].Kind == ContainerKind.Map ? TokenType.EndMap : TokenType.EndVector;
		this.Enqueue(new Token { Type = endType, IsSynthetic = true });
	}

	private void EnqueueNextTokens()
	{
		// Close containers on dedent or EOF.
		int nextLineStart = this.FindNextSignificantLineStart(this.scanOffset, out int nextIndent);
		while (this.containerDepth > 0 && (nextLineStart < 0 || nextIndent < this.containers[this.containerDepth - 1].Indent))
		{
			this.EnqueueSyntheticEndForTopContainer();
			this.Pop();

			// Emit one end token at a time.
			return;
		}

		if (nextLineStart < 0)
		{
			return;
		}

		// If we haven't emitted a root container token yet, do it now when appropriate.
		if (!this.rootContainerEmitted && this.containerDepth == 0)
		{
			int lineStart = nextLineStart;
			int indent = nextIndent;
			if (lineStart < 0)
			{
				return;
			}

			int lineEndNoNewline = this.GetLineEndNoNewline(lineStart);
			if (this.IsSequenceItemLine(lineStart, lineEndNoNewline, indent, out _, out _))
			{
				this.rootContainerEmitted = true;
				this.EnqueueSyntheticStart(TokenType.StartVector, indent);
				return;
			}

			if (this.TrySplitMappingLine(lineStart, lineEndNoNewline, indent, out _, out _, out _, out _))
			{
				this.rootContainerEmitted = true;
				this.EnqueueSyntheticStart(TokenType.StartMap, indent);
				return;
			}

			// Root scalar.
			this.rootContainerEmitted = true;
			this.scanOffset = this.AdvanceToAfterLine(lineStart);
			this.EnqueueScalarToken(lineStart + indent, lineEndNoNewline);
			return;
		}

		// Parse next line in the context of the current container (or as scalar if none).
		int start = nextLineStart;
		int currentIndent = nextIndent;
		if (start < 0)
		{
			return;
		}

		int lineEndNoNewline2 = this.GetLineEndNoNewline(start);
		this.scanOffset = this.AdvanceToAfterLine(start);

		if (this.containerDepth == 0)
		{
			// No container active; treat as scalar.
			this.EnqueueScalarToken(start + currentIndent, lineEndNoNewline2);
			return;
		}

		ContainerFrame top = this.containers[this.containerDepth - 1];
		if (top.Kind == ContainerKind.Map)
		{
			if (!this.TrySplitMappingLine(start, lineEndNoNewline2, currentIndent, out int keyStart, out int keyLength, out int valueStart, out int valueLength))
			{
				throw new DecoderException("Expected a mapping entry.");
			}

			this.Enqueue(new Token { Type = TokenType.PropertyName, Start = keyStart, Length = keyLength, IsSynthetic = false });

			if (valueLength > 0)
			{
				this.EnqueueScalarToken(valueStart, valueStart + valueLength);
			}
			else
			{
				this.EnqueueNestedValueStartOrScalar(currentIndent);
			}

			return;
		}

		// Vector
		if (!this.IsSequenceItemLine(start, lineEndNoNewline2, currentIndent, out int itemStart, out int itemLength))
		{
			throw new DecoderException("Expected a sequence item.");
		}

		if (itemLength > 0)
		{
			// Check if the inline item content is a mapping entry (has a colon separator).
			// We need to check from itemStart (where the actual content begins) to the end of the line.
			int inlineIndent = itemStart - start;
			if (this.TrySplitMappingLine(start, lineEndNoNewline2, inlineIndent, out int keyStart, out int keyLength, out int valueStart, out int valueLength))
			{
				// The inline content is a mapping entry. Enqueue StartMap, then the property.
				this.EnqueueSyntheticStart(TokenType.StartMap, inlineIndent);
				this.Enqueue(new Token { Type = TokenType.PropertyName, Start = keyStart, Length = keyLength, IsSynthetic = false });

				if (valueLength > 0)
				{
					this.EnqueueScalarToken(valueStart, valueStart + valueLength);
				}
				else
				{
					// Property value is on following indented lines.
					this.EnqueueNestedValueStartOrScalar(inlineIndent);
				}
			}
			else
			{
				// Inline scalar value.
				this.EnqueueScalarToken(itemStart, itemStart + itemLength);
			}
		}
		else
		{
			this.EnqueueNestedValueStartOrScalar(currentIndent);
		}
	}

	private void EnqueueNestedValueStartOrScalar(int parentIndent)
	{
		int childLineStart = this.FindNextSignificantLineStart(this.scanOffset, out int childIndent);
		if (childLineStart < 0)
		{
			// Missing value -> null.
			this.Enqueue(new Token { Type = TokenType.Null, IsSynthetic = true });
			return;
		}

		if (childIndent <= parentIndent)
		{
			// Missing indented value -> null.
			this.Enqueue(new Token { Type = TokenType.Null, IsSynthetic = true });
			return;
		}

		int childLineEndNoNewline = this.GetLineEndNoNewline(childLineStart);
		if (this.IsSequenceItemLine(childLineStart, childLineEndNoNewline, childIndent, out _, out _))
		{
			this.EnqueueSyntheticStart(TokenType.StartVector, childIndent);
			return;
		}

		if (this.TrySplitMappingLine(childLineStart, childLineEndNoNewline, childIndent, out _, out _, out _, out _))
		{
			this.EnqueueSyntheticStart(TokenType.StartMap, childIndent);
			return;
		}

		// Indented scalar value.
		this.scanOffset = this.AdvanceToAfterLine(childLineStart);
		this.EnqueueScalarToken(childLineStart + childIndent, childLineEndNoNewline);
	}

	private void EnqueueScalarToken(int start, int endExclusive)
	{
		(int trimmedStart, int trimmedEnd) = this.Trim(start, endExclusive);
		int length = trimmedEnd - trimmedStart;
		ReadOnlySpan<char> span = length > 0 ? this.text.AsSpan(trimmedStart, length) : ReadOnlySpan<char>.Empty;

		if (span.IsEmpty)
		{
			this.Enqueue(new Token { Type = TokenType.String, Start = trimmedStart, Length = 0, IsSynthetic = false });
			return;
		}

		if (span.Equals("null".AsSpan(), StringComparison.OrdinalIgnoreCase) || span.SequenceEqual("~".AsSpan()))
		{
			this.Enqueue(new Token { Type = TokenType.Null, Start = trimmedStart, Length = length, IsSynthetic = false });
			return;
		}

		if (this.LooksLikeNumber(span))
		{
			this.Enqueue(new Token { Type = TokenType.Number, Start = trimmedStart, Length = length, IsSynthetic = false });
			return;
		}

		this.Enqueue(new Token { Type = TokenType.String, Start = trimmedStart, Length = length, IsSynthetic = false });
	}

	private bool LooksLikeNumber(ReadOnlySpan<char> span)
	{
		if (span.IsEmpty)
		{
			return false;
		}

		int i = 0;
		if (span[0] == '-')
		{
			if (span.Length == 1)
			{
				return false;
			}

			i = 1;
		}

		for (; i < span.Length; i++)
		{
			if (!char.IsAsciiDigit(span[i]))
			{
				return false;
			}
		}

		return true;
	}

	private int FindNextSignificantLineStart(int from, out int indent)
	{
		indent = 0;
		int i = from;
		while (i < this.text.Length)
		{
			// Skip line breaks
			if (this.text[i] == '\r')
			{
				i++;
				continue;
			}

			if (this.text[i] == '\n')
			{
				i++;
				continue;
			}

			int lineStart = i;
			int currentIndent = 0;
			while (i < this.text.Length && this.text[i] == ' ')
			{
				currentIndent++;
				i++;
			}

			// Empty line
			if (i >= this.text.Length)
			{
				return -1;
			}

			int contentStart = i;
			int lineEndNoNewline = this.GetLineEndNoNewline(lineStart);
			ReadOnlySpan<char> line = this.text.AsSpan(lineStart, lineEndNoNewline - lineStart);
			ReadOnlySpan<char> trimmed = line.Slice(contentStart - lineStart).Trim();
			if (trimmed.IsEmpty)
			{
				i = this.AdvanceToAfterLine(lineStart);
				continue;
			}

			if (trimmed[0] == '#')
			{
				i = this.AdvanceToAfterLine(lineStart);
				continue;
			}

			indent = currentIndent;
			return lineStart;
		}

		return -1;
	}

	private int AdvanceToAfterLine(int lineStart)
	{
		int i = lineStart;
		while (i < this.text.Length && this.text[i] != '\n')
		{
			i++;
		}

		if (i < this.text.Length)
		{
			i++;
		}

		return i;
	}

	private int GetLineEndNoNewline(int lineStart)
	{
		int i = lineStart;
		while (i < this.text.Length && this.text[i] != '\n')
		{
			i++;
		}

		// Exclude a trailing '\r' if present.
		int end = i;
		if (end > lineStart && this.text[end - 1] == '\r')
		{
			end--;
		}

		return end;
	}

	private bool IsSequenceItemLine(int lineStart, int lineEndNoNewline, int indent, out int itemStart, out int itemLength)
	{
		int contentStart = lineStart + indent;
		if (contentStart >= lineEndNoNewline)
		{
			itemStart = 0;
			itemLength = 0;
			return false;
		}

		if (this.text[contentStart] != '-')
		{
			itemStart = 0;
			itemLength = 0;
			return false;
		}

		int afterDash = contentStart + 1;
		if (afterDash >= lineEndNoNewline)
		{
			itemStart = 0;
			itemLength = 0;
			return true;
		}

		if (this.text[afterDash] != ' ')
		{
			itemStart = 0;
			itemLength = 0;
			return false;
		}

		int start = afterDash + 1;
		(int ts, int te) = this.Trim(start, lineEndNoNewline);
		itemStart = ts;
		itemLength = te - ts;
		return true;
	}

	private bool TrySplitMappingLine(int lineStart, int lineEndNoNewline, int indent, out int keyStart, out int keyLength, out int valueStart, out int valueLength)
	{
		int contentStart = lineStart + indent;
		if (contentStart >= lineEndNoNewline)
		{
			keyStart = 0;
			keyLength = 0;
			valueStart = 0;
			valueLength = 0;
			return false;
		}

		int colonIndex = this.text.IndexOf(':', contentStart, lineEndNoNewline - contentStart);
		if (colonIndex < 0)
		{
			keyStart = 0;
			keyLength = 0;
			valueStart = 0;
			valueLength = 0;
			return false;
		}

		// Require ':' to be end-of-line or followed by whitespace.
		if (colonIndex + 1 < lineEndNoNewline && !char.IsWhiteSpace(this.text[colonIndex + 1]))
		{
			keyStart = 0;
			keyLength = 0;
			valueStart = 0;
			valueLength = 0;
			return false;
		}

		(int ks, int ke) = this.Trim(contentStart, colonIndex);
		if (ks >= ke)
		{
			keyStart = 0;
			keyLength = 0;
			valueStart = 0;
			valueLength = 0;
			return false;
		}

		int vs = colonIndex + 1;
		while (vs < lineEndNoNewline && this.text[vs] == ' ')
		{
			vs++;
		}

		(int vts, int vte) = this.Trim(vs, lineEndNoNewline);

		keyStart = ks;
		keyLength = ke - ks;
		valueStart = vts;
		valueLength = vte - vts;
		return true;
	}

	private string UnescapeString(ReadOnlySpan<char> token)
	{
		token = token.Trim();
		if (token.Length >= 2 && token[0] == '"' && token[^1] == '"')
		{
			ReadOnlySpan<char> inner = token.Slice(1, token.Length - 2);
			if (inner.IndexOf('\\') < 0)
			{
				return inner.ToString();
			}

			StringBuilder sb = new(inner.Length);
			for (int i = 0; i < inner.Length; i++)
			{
				char c = inner[i];
				if (c == '\\' && i + 1 < inner.Length)
				{
					char esc = inner[++i];
					c = esc switch
					{
						'n' => '\n',
						'r' => '\r',
						't' => '\t',
						'\\' => '\\',
						'"' => '"',
						_ => esc,
					};
				}

				sb.Append(c);
			}

			return sb.ToString();
		}

		if (token.Length >= 2 && token[0] == '\'' && token[^1] == '\'')
		{
			// Single-quoted YAML escapes '' -> '
			ReadOnlySpan<char> inner = token.Slice(1, token.Length - 2);
			return inner.ToString().Replace("''", "'", StringComparison.Ordinal);
		}

		return token.ToString();
	}

	private void Push(ContainerKind kind, int indent)
	{
		if (this.containerDepth == this.containers.Length)
		{
			Array.Resize(ref this.containers, this.containers.Length * 2);
		}

		this.containers[this.containerDepth++] = new ContainerFrame { Kind = kind, Indent = indent };
	}

	private void Pop() => this.containerDepth--;

	private (int Start, int EndExclusive) Trim(int start, int endExclusive)
	{
		while (start < endExclusive && char.IsWhiteSpace(this.text[start]))
		{
			start++;
		}

		while (endExclusive > start && char.IsWhiteSpace(this.text[endExclusive - 1]))
		{
			endExclusive--;
		}

		return (start, endExclusive);
	}

	private struct ContainerFrame
	{
		public ContainerKind Kind;
		public int Indent;
	}

	private struct Token
	{
		public TokenType Type;
		public int Start;
		public int Length;
		public int ContainerIndent;
		public bool IsSynthetic;
	}
}
