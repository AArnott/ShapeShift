// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Numerics;
using System.Text;

namespace ShapeShift.Taml;

/// <summary>
/// A ShapeShift-compatible TAML decoder.
/// </summary>
/// <param name="reader">The underlying text reader from which to get the TAML.</param>
public ref struct TamlDecoder(TextReader reader) : IDecoder
{
	private const NumberStyles FloatingPointStyle = NumberStyles.Float | NumberStyles.AllowHexSpecifier;
	private const NumberStyles IntegerPointStyle = NumberStyles.Integer;

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

	public TokenType NextTokenType
	{
		get
		{
			this.EnsureBufferedToken();
			return this.bufferedToken.Type;
		}
	}

	public bool TryReadNull()
	{
		return this.NextTokenType == TokenType.Null;
	}

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

	public void ReadEndMap()
	{
		this.ReadToken(TokenType.EndMap);
	}

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

	public void ReadEndVector()
	{
		this.ReadToken(TokenType.EndVector);
	}

	public ReadOnlySpan<char> ReadPropertyName()
	{
		return this.ReadToken(TokenType.PropertyName);
	}

	public void Skip()
	{
		this.EnsureBufferedToken();
		switch (this.bufferedToken.Type)
		{
			case TokenType.StartMap:
				this.ReadStartMap();
				while (this.NextTokenType != TokenType.EndMap)
				{
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

	public long ReadInt64()
	{
		ReadOnlySpan<char> token = this.ReadToken(TokenType.Number);
		if (!long.TryParse(token, IntegerPointStyle, CultureInfo.InvariantCulture, out long value))
		{
			throw new DecoderException($"Invalid integer value: {token.ToString()}.");
		}

		return value;
	}

	public ulong ReadUInt64()
	{
		ReadOnlySpan<char> token = this.ReadToken(TokenType.Number);
		if (!ulong.TryParse(token, IntegerPointStyle, CultureInfo.InvariantCulture, out ulong value))
		{
			throw new DecoderException($"Invalid integer value: {token.ToString()}.");
		}

		return value;
	}

	public Int128 ReadInt128()
	{
		ReadOnlySpan<char> token = this.ReadToken(TokenType.Number);
		if (!Int128.TryParse(token, IntegerPointStyle, CultureInfo.InvariantCulture, out Int128 value))
		{
			throw new DecoderException($"Invalid integer value: {token.ToString()}.");
		}

		return value;
	}

	public UInt128 ReadUInt128()
	{
		ReadOnlySpan<char> token = this.ReadToken(TokenType.Number);
		if (!UInt128.TryParse(token, IntegerPointStyle, CultureInfo.InvariantCulture, out UInt128 value))
		{
			throw new DecoderException($"Invalid integer value: {token.ToString()}.");
		}

		return value;
	}

	public BigInteger ReadBigInteger()
	{
		ReadOnlySpan<char> token = this.ReadToken(TokenType.Number);
		if (!BigInteger.TryParse(token, IntegerPointStyle, CultureInfo.InvariantCulture, out BigInteger value))
		{
			throw new DecoderException($"Invalid BigInteger value: {token.ToString()}.");
		}

		return value;
	}

	public Half ReadHalf()
	{
		ReadOnlySpan<char> token = this.ReadToken(TokenType.Number);
		if (!Half.TryParse(token, FloatingPointStyle, CultureInfo.InvariantCulture, out Half value))
		{
			throw new DecoderException($"Invalid floating point value: {token.ToString()}.");
		}

		return value;
	}

	public float ReadSingle()
	{
		ReadOnlySpan<char> token = this.ReadToken(TokenType.Number);
		if (!float.TryParse(token, FloatingPointStyle, CultureInfo.InvariantCulture, out float value))
		{
			throw new DecoderException($"Invalid floating point value: {token.ToString()}.");
		}

		return value;
	}

	public double ReadDouble()
	{
		ReadOnlySpan<char> token = this.ReadToken(TokenType.Number);
		if (!double.TryParse(token, FloatingPointStyle, CultureInfo.InvariantCulture, out double value))
		{
			throw new DecoderException($"Invalid floating point value: {token.ToString()}.");
		}

		return value;
	}

	public decimal ReadDecimal()
	{
		ReadOnlySpan<char> token = this.ReadToken(TokenType.Number);
		if (!decimal.TryParse(token, FloatingPointStyle, CultureInfo.InvariantCulture, out decimal value))
		{
			throw new DecoderException($"Invalid floating point value: {token.ToString()}.");
		}

		return value;
	}

	public DateTime ReadDateTime()
	{
		ReadOnlySpan<char> token = this.ReadToken(TokenType.Number);
		if (!DateTime.TryParse(token, CultureInfo.InvariantCulture, out DateTime value))
		{
			throw new DecoderException($"Invalid DateTime value: {token.ToString()}.");
		}

		return value;
	}

	public TimeSpan ReadTimeSpan()
	{
		ReadOnlySpan<char> token = this.ReadToken(TokenType.String);
		if (!TimeSpan.TryParse(token, CultureInfo.InvariantCulture, out TimeSpan value))
		{
			throw new DecoderException($"Invalid TimeSpan value: {token.ToString()}.");
		}

		return value;
	}

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
			throw new DecoderException("Unexpected end of TAML input.");
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
		int nextLineStart = this.FindNextSignificantLineStart(this.scanOffset, out int nextIndent);
		while (this.containerDepth > 0 && (nextLineStart < 0 || nextIndent < this.containers[this.containerDepth - 1].Indent))
		{
			this.EnqueueSyntheticEndForTopContainer();
			this.Pop();
			return;
		}

		if (nextLineStart < 0)
		{
			return;
		}

		if (!this.rootContainerEmitted && this.containerDepth == 0)
		{
			int lineStart = nextLineStart;
			int indent = nextIndent;
			if (lineStart < 0)
			{
				return;
			}

			int lineEndNoNewline = this.GetLineEndNoNewline(lineStart);
			if (this.TrySplitKeyValue(lineStart, lineEndNoNewline, indent, out _, out _, out _, out _))
			{
				this.rootContainerEmitted = true;
				this.EnqueueSyntheticStart(TokenType.StartMap, indent);
				return;
			}

			this.rootContainerEmitted = true;
			this.scanOffset = this.AdvanceToAfterLine(lineStart);
			this.EnqueueScalarToken(lineStart + indent, lineEndNoNewline);
			return;
		}

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
			this.EnqueueScalarToken(start + currentIndent, lineEndNoNewline2);
			return;
		}

		ContainerFrame top = this.containers[this.containerDepth - 1];
		if (top.Kind == ContainerKind.Map)
		{
			if (!this.TrySplitKeyValue(start, lineEndNoNewline2, currentIndent, out int keyStart, out int keyLength, out int valueStart, out int valueLength))
			{
				throw new DecoderException("Expected a key-value pair.");
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

		if (this.TrySplitKeyValue(start, lineEndNoNewline2, currentIndent, out int itemKeyStart, out int itemKeyLength, out int itemValueStart, out int itemValueLength))
		{
			this.EnqueueSyntheticStart(TokenType.StartMap, currentIndent);
			this.Enqueue(new Token { Type = TokenType.PropertyName, Start = itemKeyStart, Length = itemKeyLength, IsSynthetic = false });

			if (itemValueLength > 0)
			{
				this.EnqueueScalarToken(itemValueStart, itemValueStart + itemValueLength);
			}
			else
			{
				this.EnqueueNestedValueStartOrScalar(currentIndent);
			}

			return;
		}

		this.EnqueueScalarToken(start + currentIndent, lineEndNoNewline2);
	}

	private void EnqueueNestedValueStartOrScalar(int parentIndent)
	{
		int childLineStart = this.FindNextSignificantLineStart(this.scanOffset, out int childIndent);
		if (childLineStart < 0)
		{
			this.Enqueue(new Token { Type = TokenType.Null, IsSynthetic = true });
			return;
		}

		if (childIndent <= parentIndent)
		{
			this.Enqueue(new Token { Type = TokenType.Null, IsSynthetic = true });
			return;
		}

		int childLineEndNoNewline = this.GetLineEndNoNewline(childLineStart);
		if (this.TrySplitKeyValue(childLineStart, childLineEndNoNewline, childIndent, out _, out _, out _, out _))
		{
			if (this.HasRepeatedKeysIndicatingVector(childLineStart, childIndent))
			{
				this.EnqueueSyntheticStart(TokenType.StartVector, childIndent);
				return;
			}

			this.EnqueueSyntheticStart(TokenType.StartMap, childIndent);
			return;
		}

		this.EnqueueSyntheticStart(TokenType.StartVector, childIndent);
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

		if (span.Equals("~".AsSpan(), StringComparison.Ordinal) || span.Equals("null".AsSpan(), StringComparison.OrdinalIgnoreCase))
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
		return this.FindNextSignificantLineStart(from, out indent, skipBlankLines: true);
	}

	private int FindNextSignificantLineStart(int from, out int indent, bool skipBlankLines)
	{
		indent = 0;
		int i = from;
		while (i < this.text.Length)
		{
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
			while (i < this.text.Length && this.text[i] == '\t')
			{
				currentIndent++;
				i++;
			}

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
				if (!skipBlankLines)
				{
					indent = currentIndent;
					return lineStart;
				}

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

		int end = i;
		if (end > lineStart && this.text[end - 1] == '\r')
		{
			end--;
		}

		return end;
	}

	private bool TrySplitKeyValue(int lineStart, int lineEndNoNewline, int indent, out int keyStart, out int keyLength, out int valueStart, out int valueLength)
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

		int tabIndex = this.text.IndexOf('\t', contentStart, lineEndNoNewline - contentStart);
		if (tabIndex < 0)
		{
			keyStart = 0;
			keyLength = 0;
			valueStart = 0;
			valueLength = 0;
			return false;
		}

		(int ks, int ke) = this.Trim(contentStart, tabIndex);
		if (ks >= ke)
		{
			keyStart = 0;
			keyLength = 0;
			valueStart = 0;
			valueLength = 0;
			return false;
		}

		int vs = tabIndex + 1;
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

	private bool HasRepeatedKeysIndicatingVector(int startLine, int indent)
	{
		HashSet<string> seenKeys = new();
		int scanPos = startLine;
		int blankLinesSinceLastContent = 0;
		bool hasContent = false;

		while (true)
		{
			int lineStart = this.FindNextSignificantLineStart(scanPos, out int lineIndent, skipBlankLines: false);
			if (lineStart < 0)
			{
				break;
			}

			int lineEndNoNewline = this.GetLineEndNoNewline(lineStart);
			int contentStart = lineStart + lineIndent;

			bool isBlankLine = contentStart >= lineEndNoNewline;
			if (!isBlankLine)
			{
				ReadOnlySpan<char> content = this.text.AsSpan(contentStart, lineEndNoNewline - contentStart).Trim();
				isBlankLine = content.IsEmpty || content[0] == '#';
			}

			if (isBlankLine)
			{
				if (hasContent)
				{
					blankLinesSinceLastContent++;
				}

				scanPos = this.AdvanceToAfterLine(lineStart);
				continue;
			}

			if (lineIndent < indent)
			{
				break;
			}

			if (lineIndent > indent)
			{
				scanPos = this.AdvanceToAfterLine(lineStart);
				continue;
			}

			if (this.TrySplitKeyValue(lineStart, lineEndNoNewline, indent, out int keyStart, out int keyLength, out _, out _))
			{
				string key = this.text.Substring(keyStart, keyLength);

				if (blankLinesSinceLastContent > 0 && hasContent)
				{
					return true;
				}

				if (seenKeys.Contains(key))
				{
					return true;
				}

				seenKeys.Add(key);
				hasContent = true;
				blankLinesSinceLastContent = 0;
			}

			scanPos = this.AdvanceToAfterLine(lineStart);
		}

		return false;
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
