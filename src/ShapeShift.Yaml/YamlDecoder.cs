// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Numerics;

namespace ShapeShift.Yaml;

/// <summary>
/// Reads YAML 1.2 as ShapeShift tokens.
/// </summary>
/// <remarks>
/// <para>
/// The reader accepts the YAML 1.2 syntax: block and flow collections, plain, quoted, and block scalars,
/// anchors and aliases, tags, directives, and multi-document streams. Scalars use the YAML 1.2 core schema.
/// Quoted and block scalars stay strings unless a tag says otherwise. A non-specific <c>!</c> tag forces a
/// string, so <c>! true</c> and <c>! 12</c> are text. <c>yes</c>, <c>no</c>, <c>on</c>, and <c>off</c> are
/// strings. Timestamps stay strings, so <see cref="ReadDateTime"/> and <see cref="ReadTimeSpan"/> still parse text.
/// </para>
/// <para>
/// The constructor reads the whole stream and rejects a document that is not valid YAML. After each document,
/// <see cref="NextTokenType"/> is <see cref="TokenType.EndDocument"/> until <see cref="TryAdvanceDocument"/>
/// moves to the next one. An empty or comment-only stream has <see cref="DocumentCount"/> zero and presents
/// one null so a caller that reads a single value still sees a complete document.
/// </para>
/// </remarks>
public ref struct YamlDecoder : IDecoder
{
	private readonly YamlNode[] documents;
	private readonly int maxDepth;
	private readonly int maxAliasExpansions;
	private readonly int maxExpandedNodes;
	private Frame[] frames;
	private int frameCount;
	private int documentIndex;
	private int aliasExpansions;
	private int expandedNodes;
	private bool rootStarted;
	private bool hasToken;
	private bool consumePops;
	private bool advancePhaseOnConsume;
	private FramePhase phaseAfterConsume;
	private TokenType tokenType;
	private NumberKind numberKind;
	private string tokenText;
	private bool tokenBoolean;
	private BigInteger tokenInteger;
	private double tokenFloat;
	private byte[]? tokenBinary;

	/// <summary>
	/// Initializes a new instance of the <see cref="YamlDecoder"/> struct with the default load options.
	/// </summary>
	/// <param name="reader">The YAML text. The reader is consumed immediately.</param>
	/// <exception cref="ArgumentNullException"><paramref name="reader"/> is <see langword="null"/>.</exception>
	/// <exception cref="DecoderException">The text is not valid YAML 1.2.</exception>
	public YamlDecoder(TextReader reader)
		: this(reader, new YamlLoadOptions())
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="YamlDecoder"/> struct.
	/// </summary>
	/// <param name="reader">The YAML text. The reader is consumed immediately.</param>
	/// <param name="options">Duplicate-key and resource limits. <see langword="null"/> uses the defaults.</param>
	/// <exception cref="ArgumentNullException"><paramref name="reader"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException">A limit in <paramref name="options"/> is not positive.</exception>
	/// <exception cref="DecoderException">The text is not valid YAML 1.2.</exception>
	public YamlDecoder(TextReader reader, YamlLoadOptions? options)
	{
		ArgumentNullException.ThrowIfNull(reader);
		options ??= new YamlLoadOptions();
		if (options.MaxDepth <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(options), "MaxDepth must be positive.");
		}

		if (options.MaxAliasExpansions <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(options), "MaxAliasExpansions must be positive.");
		}

		if (options.MaxExpandedNodes <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(options), "MaxExpandedNodes must be positive.");
		}

		this.documents = [.. YamlParser.Parse(reader.ReadToEnd(), options)];
		this.maxDepth = options.MaxDepth;
		this.maxAliasExpansions = options.MaxAliasExpansions;
		this.maxExpandedNodes = options.MaxExpandedNodes;
		this.frames = new Frame[8];
		this.tokenText = string.Empty;
	}

	private enum FramePhase
	{
		Start,
		Entry,
		Value,
		End,
	}

	private enum NumberKind
	{
		None,
		Integer,
		Float,
	}

	/// <summary>
	/// Gets the number of documents in the stream. An empty or comment-only stream has zero documents.
	/// </summary>
	/// <value>The document count.</value>
	public int DocumentCount => this.documents.Length;

	/// <summary>
	/// Gets the zero-based index of the document <see cref="NextTokenType"/> is reading.
	/// </summary>
	/// <value>The current document index. An empty stream reports zero.</value>
	public int DocumentIndex => this.documentIndex;

	/// <inheritdoc/>
	public TokenType NextTokenType
	{
		get
		{
			this.EnsureToken();
			return this.hasToken ? this.tokenType : TokenType.EndDocument;
		}
	}

	/// <summary>
	/// Moves to the next document after the current one has been fully read.
	/// </summary>
	/// <returns>
	/// <see langword="true"/> when another document is now current;
	/// <see langword="false"/> when the stream has no further document.
	/// </returns>
	/// <exception cref="InvalidOperationException">The current document still has unread tokens.</exception>
	public bool TryAdvanceDocument()
	{
		if (this.NextTokenType != TokenType.EndDocument)
		{
			throw new InvalidOperationException("The current YAML document has not been fully read.");
		}

		if (this.documentIndex + 1 >= this.documents.Length)
		{
			return false;
		}

		this.documentIndex++;
		this.rootStarted = false;
		this.frameCount = 0;
		this.aliasExpansions = 0;
		this.expandedNodes = 0;
		this.hasToken = false;
		this.consumePops = false;
		this.advancePhaseOnConsume = false;
		return true;
	}

	/// <inheritdoc/>
	public bool TryReadNull()
	{
		if (this.NextTokenType != TokenType.Null)
		{
			return false;
		}

		this.Consume();
		return true;
	}

	/// <inheritdoc/>
	public int? ReadStartMap()
	{
		this.ReadStructural(TokenType.StartMap);
		return null;
	}

	/// <inheritdoc/>
	public void ReadEndMap() => this.ReadStructural(TokenType.EndMap);

	/// <inheritdoc/>
	public int? ReadStartVector()
	{
		this.ReadStructural(TokenType.StartVector);
		return null;
	}

	/// <inheritdoc/>
	public void ReadEndVector() => this.ReadStructural(TokenType.EndVector);

	/// <inheritdoc/>
	public ReadOnlySpan<char> ReadPropertyName()
	{
		this.Require(TokenType.PropertyName);
		ReadOnlySpan<char> name = this.tokenText.AsSpan();
		this.Consume();
		return name;
	}

	/// <inheritdoc/>
	public void Skip()
	{
		switch (this.NextTokenType)
		{
			case TokenType.StartMap:
				this.ReadStartMap();
				while (this.NextTokenType != TokenType.EndMap)
				{
					if (this.NextTokenType == TokenType.PropertyName)
					{
						_ = this.ReadPropertyName();
					}
					else
					{
						this.Skip();
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
			case TokenType.EndDocument:
				return;
			default:
				this.Consume();
				break;
		}
	}

	/// <inheritdoc/>
	public void ReadNull() => this.Require(TokenType.Null);

	/// <inheritdoc/>
	public bool ReadBoolean()
	{
		this.Require(TokenType.Boolean);
		bool value = this.tokenBoolean;
		this.Consume();
		return value;
	}

	/// <inheritdoc/>
	public long ReadInt64() => (long)this.ReadInteger(long.MinValue, long.MaxValue);

	/// <inheritdoc/>
	public ulong ReadUInt64() => (ulong)this.ReadInteger(ulong.MinValue, ulong.MaxValue);

	/// <inheritdoc/>
	public Int128 ReadInt128() => (Int128)this.ReadInteger(Int128.MinValue, Int128.MaxValue);

	/// <inheritdoc/>
	public UInt128 ReadUInt128() => (UInt128)this.ReadInteger(UInt128.MinValue, UInt128.MaxValue);

	/// <inheritdoc/>
	public BigInteger ReadBigInteger()
	{
		this.RequireInteger();
		BigInteger value = this.tokenInteger;
		this.Consume();
		return value;
	}

	/// <inheritdoc/>
	public Half ReadHalf()
	{
		double value = this.ReadDouble();
		if (double.IsNaN(value))
		{
			return Half.NaN;
		}

		if (double.IsPositiveInfinity(value))
		{
			return Half.PositiveInfinity;
		}

		if (double.IsNegativeInfinity(value))
		{
			return Half.NegativeInfinity;
		}

		if (value > (double)Half.MaxValue || value < -(double)Half.MaxValue)
		{
			throw new DecoderException($"Invalid floating point value: {value.ToString(CultureInfo.InvariantCulture)}.");
		}

		return (Half)value;
	}

	/// <inheritdoc/>
	public float ReadSingle()
	{
		double value = this.ReadDouble();
		if (double.IsNaN(value) || double.IsInfinity(value))
		{
			return (float)value;
		}

		if (value > float.MaxValue || value < -float.MaxValue)
		{
			throw new DecoderException($"Invalid floating point value: {value.ToString(CultureInfo.InvariantCulture)}.");
		}

		return (float)value;
	}

	/// <inheritdoc/>
	public double ReadDouble()
	{
		this.RequireNumber();
		double value = this.numberKind == NumberKind.Integer ? (double)this.tokenInteger : this.tokenFloat;
		this.Consume();
		return value;
	}

	/// <inheritdoc/>
	public decimal ReadDecimal()
	{
		this.RequireNumber();
		try
		{
			decimal value = this.numberKind == NumberKind.Integer
				? (decimal)this.tokenInteger
				: this.DecimalFromFloatText();
			this.Consume();
			return value;
		}
		catch (OverflowException ex)
		{
			throw new DecoderException("The number is outside the range of decimal.", ex);
		}
	}

	/// <inheritdoc/>
	public DateTime ReadDateTime()
	{
		string text = this.ReadString();
		if (!DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime value))
		{
			throw new DecoderException($"Invalid DateTime value: {text}.");
		}

		return value;
	}

	/// <inheritdoc/>
	public TimeSpan ReadTimeSpan()
	{
		string text = this.ReadString();
		if (!TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out TimeSpan value))
		{
			throw new DecoderException($"Invalid TimeSpan value: {text}.");
		}

		return value;
	}

	/// <inheritdoc/>
	public string ReadString()
	{
		this.Require(TokenType.String);
		string value = this.tokenText;
		this.Consume();
		return value;
	}

	/// <inheritdoc/>
	public ReadOnlySpan<char> ReadCharSpan()
	{
		this.Require(TokenType.String);
		ReadOnlySpan<char> value = this.tokenText.AsSpan();
		this.Consume();
		return value;
	}

	/// <inheritdoc/>
	public ReadOnlySpan<char> ReadCharSpan(scoped Span<char> buffer, out int charactersWritten)
	{
		charactersWritten = -1;
		return this.ReadCharSpan();
	}

	/// <inheritdoc/>
	public byte[] ReadByteArray()
	{
		this.Require(TokenType.Binary);
		byte[] value = this.tokenBinary ?? [];
		this.Consume();
		return value;
	}

	/// <inheritdoc/>
	public ShapeShiftNumber ReadDynamicNumber()
	{
		this.RequireNumber();
		ShapeShiftNumber value;
		if (this.numberKind == NumberKind.Integer)
		{
			BigInteger integer = this.tokenInteger;
			if (integer >= long.MinValue && integer <= long.MaxValue)
			{
				value = new ShapeShiftInteger((long)integer);
			}
			else if (integer > long.MaxValue && integer <= ulong.MaxValue)
			{
				value = new ShapeShiftUnsignedInteger((ulong)integer);
			}
			else
			{
				value = new ShapeShiftBigInteger(integer);
			}
		}
		else if (!double.IsFinite(this.tokenFloat))
		{
			value = new ShapeShiftFloat(this.tokenFloat);
		}
		else if (this.TryExactDecimal(out decimal exact))
		{
			value = new ShapeShiftDecimal(exact);
		}
		else
		{
			value = new ShapeShiftFloat(this.tokenFloat);
		}

		this.Consume();
		return value;
	}

	/// <summary>
	/// Determines whether an unquoted scalar should be classified as a decimal number by the encoder.
	/// </summary>
	/// <param name="span">The scalar text.</param>
	/// <returns><see langword="true"/> when the text is a decimal integer or floating-point literal.</returns>
	internal static bool LooksLikeNumberCore(ReadOnlySpan<char> span) => YamlSchema.LooksLikeDecimalNumber(span);

	private decimal DecimalFromFloatText()
	{
		if (!double.IsFinite(this.tokenFloat))
		{
			throw new DecoderException("A non-finite float cannot be read as a decimal.");
		}

		decimal value;
		try
		{
			value = decimal.Parse(this.tokenText, NumberStyles.Float, CultureInfo.InvariantCulture);
		}
		catch (FormatException ex)
		{
			throw new DecoderException("The number is not a valid decimal.", ex);
		}

		if (value == 0m && this.tokenFloat != 0d)
		{
			throw new DecoderException("The number is outside the range of decimal.");
		}

		return value;
	}

	private bool TryExactDecimal(out decimal value)
	{
		value = 0m;
		if (!double.IsFinite(this.tokenFloat))
		{
			return false;
		}

		try
		{
			value = decimal.Parse(this.tokenText, NumberStyles.Float, CultureInfo.InvariantCulture);
		}
		catch (FormatException)
		{
			return false;
		}
		catch (OverflowException)
		{
			return false;
		}

		if (value == 0m && this.tokenFloat != 0d)
		{
			return false;
		}

		return true;
	}

	private BigInteger ReadInteger(BigInteger min, BigInteger max)
	{
		this.RequireInteger();
		BigInteger value = this.tokenInteger;
		if (value < min || value > max)
		{
			throw new DecoderException($"Invalid integer value: {value.ToString(CultureInfo.InvariantCulture)}.");
		}

		this.Consume();
		return value;
	}

	private void RequireInteger()
	{
		this.RequireNumber();
		if (this.numberKind != NumberKind.Integer)
		{
			throw new DecoderException("Expected an integer.");
		}
	}

	private void RequireNumber() => this.Require(TokenType.Number);

	private void ReadStructural(TokenType expected) => this.Require(expected);

	private void Require(TokenType expected)
	{
		this.EnsureToken();
		if (!this.hasToken || this.tokenType != expected)
		{
			TokenType actual = this.hasToken ? this.tokenType : TokenType.EndDocument;
			throw new DecoderException($"Expected a {expected} token but instead got {actual}.");
		}

		if (expected is TokenType.Null or TokenType.StartMap or TokenType.EndMap or TokenType.StartVector or TokenType.EndVector)
		{
			this.Consume();
		}
	}

	private void EnsureToken()
	{
		if (this.hasToken)
		{
			return;
		}

		while (true)
		{
			if (this.frameCount == 0)
			{
				if (!this.rootStarted)
				{
					this.rootStarted = true;
					if (this.documents.Length == 0)
					{
						this.SetNull();
						return;
					}

					this.Open(this.documents[this.documentIndex]);
					continue;
				}

				return;
			}

			ref Frame frame = ref this.frames[this.frameCount - 1];
			switch (frame.Phase)
			{
				case FramePhase.Start:
					bool mapping = frame.Node.Kind == YamlNodeKind.Mapping;
					bool empty = mapping
						? frame.Node.Pairs!.Count == 0
						: frame.Node.Items!.Count == 0;
					this.tokenType = mapping ? TokenType.StartMap : TokenType.StartVector;
					this.hasToken = true;
					this.advancePhaseOnConsume = true;
					this.phaseAfterConsume = empty ? FramePhase.End : FramePhase.Entry;
					return;
				case FramePhase.Entry:
					if (frame.Node.Kind == YamlNodeKind.Sequence)
					{
						YamlNode item = frame.Node.Items![frame.Index];
						frame.Index++;
						if (frame.Index >= frame.Node.Items.Count)
						{
							frame.Phase = FramePhase.End;
						}

						this.Open(item);
						if (this.hasToken)
						{
							return;
						}

						continue;
					}

					YamlPair pair = frame.Node.Pairs![frame.Index];
					YamlNode key = this.Follow(pair.Key);
					this.NoteVisit(key);
					if (key.Kind == YamlNodeKind.Scalar)
					{
						this.tokenType = TokenType.PropertyName;
						this.tokenText = key.Scalar;
						this.hasToken = true;
						frame.Phase = FramePhase.Value;
						return;
					}

					frame.Phase = FramePhase.Value;
					this.Push(key);
					continue;
				case FramePhase.Value:
					YamlNode value = frame.Node.Pairs![frame.Index].Value;
					frame.Index++;
					frame.Phase = frame.Index >= frame.Node.Pairs.Count ? FramePhase.End : FramePhase.Entry;
					this.Open(value);
					if (this.hasToken)
					{
						return;
					}

					continue;
				default:
					this.tokenType = frame.Node.Kind == YamlNodeKind.Mapping ? TokenType.EndMap : TokenType.EndVector;
					this.hasToken = true;
					this.consumePops = true;
					return;
			}
		}
	}

	private void Open(YamlNode node)
	{
		node = this.Follow(node);
		this.NoteVisit(node);
		if (node.Kind == YamlNodeKind.Scalar)
		{
			this.SetScalar(node);
			return;
		}

		this.Push(node);
	}

	private void Push(YamlNode node)
	{
		if (this.frameCount >= this.maxDepth)
		{
			throw new DecoderException($"YAML nesting exceeds the maximum depth of {this.maxDepth}.");
		}

		if (this.frameCount == this.frames.Length)
		{
			Array.Resize(ref this.frames, this.frames.Length * 2);
		}

		this.frames[this.frameCount++] = new Frame { Node = node, Phase = FramePhase.Start };
	}

	/// <summary>
	/// Counts a repeated read of a node. The first read is free.
	/// </summary>
	/// <param name="node">The node being read, after aliases have been followed.</param>
	private void NoteVisit(YamlNode node)
	{
		if (++node.Reads > 1)
		{
			this.expandedNodes++;
			if (this.expandedNodes > this.maxExpandedNodes)
			{
				throw new DecoderException($"YAML alias expansion exceeds the maximum of {this.maxExpandedNodes} nodes.");
			}
		}
	}

	private YamlNode Follow(YamlNode node)
	{
		while (node.Alias is not null)
		{
			this.aliasExpansions++;
			if (this.aliasExpansions > this.maxAliasExpansions)
			{
				throw new DecoderException($"YAML alias expansion exceeds the maximum of {this.maxAliasExpansions}.");
			}

			node = node.Alias;
		}

		for (int i = 0; i < this.frameCount; i++)
		{
			if (ReferenceEquals(this.frames[i].Node, node))
			{
				throw new DecoderException("A YAML alias refers to a node that is still being read.");
			}
		}

		return node;
	}

	private void SetScalar(YamlNode node)
	{
		YamlResolved resolved = YamlSchema.Resolve(node);
		this.tokenText = resolved.Text ?? node.Scalar;
		this.numberKind = NumberKind.None;
		this.tokenBinary = null;
		this.hasToken = true;
		switch (resolved.Kind)
		{
			case YamlScalarKind.Null:
				this.tokenType = TokenType.Null;
				break;
			case YamlScalarKind.Boolean:
				this.tokenType = TokenType.Boolean;
				this.tokenBoolean = resolved.Boolean;
				break;
			case YamlScalarKind.Integer:
				this.tokenType = TokenType.Number;
				this.numberKind = NumberKind.Integer;
				this.tokenInteger = resolved.Integer;
				break;
			case YamlScalarKind.Float:
				this.tokenType = TokenType.Number;
				this.numberKind = NumberKind.Float;
				this.tokenFloat = resolved.Float;
				break;
			case YamlScalarKind.Binary:
				this.tokenType = TokenType.Binary;
				this.tokenBinary = resolved.Binary;
				break;
			default:
				this.tokenType = TokenType.String;
				this.tokenText = node.Scalar;
				break;
		}
	}

	private void SetNull()
	{
		this.tokenType = TokenType.Null;
		this.tokenText = string.Empty;
		this.numberKind = NumberKind.None;
		this.hasToken = true;
	}

	private void Consume()
	{
		if (!this.hasToken)
		{
			this.EnsureToken();
		}

		this.hasToken = false;
		if (this.consumePops)
		{
			this.consumePops = false;
			if (this.frameCount > 0)
			{
				this.frameCount--;
			}

			return;
		}

		if (this.advancePhaseOnConsume && this.frameCount > 0)
		{
			this.frames[this.frameCount - 1].Phase = this.phaseAfterConsume;
		}

		this.advancePhaseOnConsume = false;
	}

	private struct Frame
	{
		public YamlNode Node;
		public int Index;
		public FramePhase Phase;
	}
}
