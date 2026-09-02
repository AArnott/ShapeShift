// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Numerics;
using System.Text;

namespace ShapeShift.Toml;

/// <summary>
/// A ShapeShift-compatible TOML decoder.
/// </summary>
/// <param name="reader">The underlying text reader from which to get the TOML.</param>
public ref struct TomlDecoder(TextReader reader) : IDecoder
{
	private readonly Token[] tokens = Parser.Parse(reader.ReadToEnd());
	private int position;

	/// <inheritdoc/>
	public TokenType NextTokenType => this.position < this.tokens.Length ? this.tokens[this.position].Type : TokenType.EndDocument;

	/// <inheritdoc/>
	public bool TryReadNull()
	{
		if (this.NextTokenType != TokenType.Null)
		{
			return false;
		}

		this.position++;
		return true;
	}

	/// <inheritdoc/>
	public int? ReadStartMap()
	{
		this.ReadToken(TokenType.StartMap);
		return null;
	}

	/// <inheritdoc/>
	public void ReadEndMap() => this.ReadToken(TokenType.EndMap);

	/// <inheritdoc/>
	public int? ReadStartVector()
	{
		this.ReadToken(TokenType.StartVector);
		return null;
	}

	/// <inheritdoc/>
	public void ReadEndVector() => this.ReadToken(TokenType.EndVector);

	/// <inheritdoc/>
	public ReadOnlySpan<char> ReadPropertyName() => this.ReadToken(TokenType.PropertyName);

	/// <inheritdoc/>
	public void Skip()
	{
		switch (this.NextTokenType)
		{
			case TokenType.StartMap:
				this.ReadStartMap();
				while (this.NextTokenType != TokenType.EndMap)
				{
					this.ReadPropertyName();
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
			case TokenType.EndDocument:
			case TokenType.EndMap:
			case TokenType.EndVector:
				throw new DecoderException("Expected a TOML value.");
			default:
				this.position++;
				break;
		}
	}

	/// <inheritdoc/>
	public void ReadNull() => this.ReadToken(TokenType.Null);

	/// <inheritdoc/>
	public bool ReadBoolean()
	{
		ReadOnlySpan<char> value = this.ReadToken(TokenType.Boolean);
		return value.SequenceEqual("true") ? true : value.SequenceEqual("false") ? false : throw new DecoderException("Invalid TOML Boolean.");
	}

	/// <inheritdoc/>
	public long ReadInt64() => this.ParseInteger<long>(long.TryParse, "Int64");

	/// <inheritdoc/>
	public ulong ReadUInt64() => this.ParseInteger<ulong>(ulong.TryParse, "UInt64");

	/// <inheritdoc/>
	public Int128 ReadInt128() => this.ParseInteger<Int128>(Int128.TryParse, "Int128");

	/// <inheritdoc/>
	public UInt128 ReadUInt128() => this.ParseInteger<UInt128>(UInt128.TryParse, "UInt128");

	/// <inheritdoc/>
	public BigInteger ReadBigInteger() => this.ParseInteger<BigInteger>(BigInteger.TryParse, "BigInteger");

	/// <inheritdoc/>
	public Half ReadHalf()
	{
		string value = NormalizeFloatingPoint(this.ReadToken(TokenType.Number));
		return Half.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out Half result) ? result : throw InvalidNumber("Half", value);
	}

	/// <inheritdoc/>
	public float ReadSingle()
	{
		string value = NormalizeFloatingPoint(this.ReadToken(TokenType.Number));
		return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result) ? result : throw InvalidNumber("Single", value);
	}

	/// <inheritdoc/>
	public double ReadDouble()
	{
		string value = NormalizeFloatingPoint(this.ReadToken(TokenType.Number));
		return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result) ? result : throw InvalidNumber("Double", value);
	}

	/// <inheritdoc/>
	public decimal ReadDecimal()
	{
		string value = this.ReadToken(TokenType.Number).ToString();
		return decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal result) ? result : throw InvalidNumber("Decimal", value);
	}

	/// <inheritdoc/>
	public DateTime ReadDateTime()
	{
		ReadOnlySpan<char> value = this.ReadToken(TokenType.String);
		return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime result)
			? result
			: throw new DecoderException($"Invalid TOML date/time: {value.ToString()}.");
	}

	/// <inheritdoc/>
	public TimeSpan ReadTimeSpan()
	{
		ReadOnlySpan<char> value = this.ReadToken(TokenType.String);
		return TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out TimeSpan result)
			? result
			: throw new DecoderException($"Invalid TOML duration: {value.ToString()}.");
	}

	/// <inheritdoc/>
	public string ReadString() => this.ReadToken(TokenType.String).ToString();

	/// <inheritdoc/>
	public ReadOnlySpan<char> ReadCharSpan() => this.ReadToken(TokenType.String);

	/// <inheritdoc/>
	public byte[] ReadByteArray() => throw new NotSupportedException("TOML binary values are not supported.");

	/// <inheritdoc/>
	public ShapeShiftNumber ReadDynamicNumber()
	{
		ReadOnlySpan<char> value = this.ReadToken(TokenType.Number);
		if (value.IndexOfAny('.', 'e', 'E') < 0 && BigInteger.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out BigInteger integer))
		{
			if (integer >= long.MinValue && integer <= long.MaxValue)
			{
				return new ShapeShiftInteger((long)integer);
			}

			return new ShapeShiftBigInteger(integer);
		}

		if (decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal decimalValue))
		{
			return new ShapeShiftDecimal(decimalValue);
		}

		string normalized = NormalizeFloatingPoint(value);
		if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleValue))
		{
			return new ShapeShiftFloat(doubleValue);
		}

		throw InvalidNumber("dynamic number", value.ToString());
	}

	private delegate bool IntegerParser<T>(ReadOnlySpan<char> value, NumberStyles style, IFormatProvider? provider, out T result);

	private T ParseInteger<T>(IntegerParser<T> parser, string typeName)
	{
		ReadOnlySpan<char> value = this.ReadToken(TokenType.Number);
		return parser(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out T result) ? result : throw InvalidNumber(typeName, value.ToString());
	}

	private ReadOnlySpan<char> ReadToken(TokenType expectedType)
	{
		if (this.position >= this.tokens.Length)
		{
			throw new DecoderException("Unexpected end of TOML input.");
		}

		Token token = this.tokens[this.position];
		if (token.Type != expectedType)
		{
			throw new DecoderException($"Expected a {expectedType} token but instead got {token.Type}.");
		}

		this.position++;
		return token.Value.AsSpan();
	}

	private static DecoderException InvalidNumber(string typeName, string value) => new($"Invalid TOML {typeName} value: {value}.");

	private static string NormalizeFloatingPoint(ReadOnlySpan<char> value)
		=> value switch
		{
			"inf" or "+inf" => "Infinity",
			"-inf" => "-Infinity",
			"nan" or "+nan" or "-nan" => "NaN",
			_ => value.ToString(),
		};

	private readonly record struct Token(TokenType Type, string Value = "");

	private sealed class Parser(string text)
	{
		private readonly string text = text;
		private readonly List<Token> tokens = [];
		private int position;

		internal static Token[] Parse(string text)
		{
			Parser parser = new(text);
			parser.ParseDocument();
			return [.. parser.tokens];
		}

		private void ParseDocument()
		{
			this.SkipTrivia(includeNewlines: true);
			if (this.position == this.text.Length)
			{
				throw this.Error("Expected a TOML document");
			}

			if (this.text[this.position] == '{')
			{
				this.ParseMap();
			}
			else if (this.LooksLikeRootMap())
			{
				this.ParseRootMap();
			}
			else
			{
				this.ParseValue();
			}

			this.SkipTrivia(includeNewlines: true);
			if (this.position != this.text.Length)
			{
				throw this.Error("Unexpected content after the root value");
			}
		}

		private void ParseRootMap()
		{
			this.tokens.Add(new(TokenType.StartMap));
			while (true)
			{
				this.SkipTrivia(includeNewlines: true);
				if (this.position == this.text.Length)
				{
					break;
				}

				this.ParseProperty();
				this.SkipTrivia(includeNewlines: false);
				if (this.position < this.text.Length && this.text[this.position] is not ('\r' or '\n' or '#'))
				{
					throw this.Error("Expected the end of a TOML key/value pair");
				}
			}

			this.tokens.Add(new(TokenType.EndMap));
		}

		private void ParseMap()
		{
			this.Expect('{');
			this.tokens.Add(new(TokenType.StartMap));
			this.SkipTrivia(includeNewlines: true);
			if (this.TryConsume('}'))
			{
				this.tokens.Add(new(TokenType.EndMap));
				return;
			}

			while (true)
			{
				this.ParseProperty();
				this.SkipTrivia(includeNewlines: true);
				if (this.TryConsume('}'))
				{
					break;
				}

				this.Expect(',');
				this.SkipTrivia(includeNewlines: true);
			}

			this.tokens.Add(new(TokenType.EndMap));
		}

		private void ParseProperty()
		{
			string key = this.ParseKey();
			this.SkipTrivia(includeNewlines: false);
			this.Expect('=');
			this.SkipTrivia(includeNewlines: false);
			this.tokens.Add(new(TokenType.PropertyName, key));
			this.ParseValue();
		}

		private void ParseArray()
		{
			this.Expect('[');
			this.tokens.Add(new(TokenType.StartVector));
			this.SkipTrivia(includeNewlines: true);
			if (this.TryConsume(']'))
			{
				this.tokens.Add(new(TokenType.EndVector));
				return;
			}

			while (true)
			{
				this.ParseValue();
				this.SkipTrivia(includeNewlines: true);
				if (this.TryConsume(']'))
				{
					break;
				}

				this.Expect(',');
				this.SkipTrivia(includeNewlines: true);
				if (this.TryConsume(']'))
				{
					break;
				}
			}

			this.tokens.Add(new(TokenType.EndVector));
		}

		private void ParseValue()
		{
			if (this.position >= this.text.Length)
			{
				throw this.Error("Expected a TOML value");
			}

			switch (this.text[this.position])
			{
				case '{': this.ParseMap(); return;
				case '[': this.ParseArray(); return;
				case '"': this.tokens.Add(new(TokenType.String, this.ParseBasicString())); return;
				case '\'': this.tokens.Add(new(TokenType.String, this.ParseLiteralString())); return;
			}

			int start = this.position;
			while (this.position < this.text.Length && this.text[this.position] is not (',' or ']' or '}' or '\r' or '\n' or '#'))
			{
				this.position++;
			}

			string value = this.text[start..this.position].Trim();
			if (value.Length == 0)
			{
				throw this.Error("Expected a TOML value");
			}

			if (value is "true" or "false")
			{
				this.tokens.Add(new(TokenType.Boolean, value));
			}
			else if (value == "null")
			{
				this.tokens.Add(new(TokenType.Null, value));
			}
			else if (LooksLikeNumber(value))
			{
				this.tokens.Add(new(TokenType.Number, value.Replace("_", string.Empty, StringComparison.Ordinal)));
			}
			else if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))
			{
				this.tokens.Add(new(TokenType.String, value));
			}
			else
			{
				throw this.Error($"Unrecognized TOML value '{value}'");
			}
		}

		private string ParseKey()
		{
			if (this.position >= this.text.Length)
			{
				throw this.Error("Expected a TOML key");
			}

			if (this.text[this.position] == '"')
			{
				return this.ParseBasicString();
			}

			if (this.text[this.position] == '\'')
			{
				return this.ParseLiteralString();
			}

			int start = this.position;
			while (this.position < this.text.Length && IsBareKeyCharacter(this.text[this.position]))
			{
				this.position++;
			}

			return this.position > start ? this.text[start..this.position] : throw this.Error("Expected a TOML key");
		}

		private string ParseBasicString()
		{
			this.Expect('"');
			StringBuilder result = new();
			while (this.position < this.text.Length)
			{
				char character = this.text[this.position++];
				if (character == '"')
				{
					return result.ToString();
				}

				if (character != '\\')
				{
					if (character is '\r' or '\n' || character < 0x20 && character != '\t')
					{
						throw this.Error("Control character in TOML basic string");
					}

					result.Append(character);
					continue;
				}

				if (this.position >= this.text.Length)
				{
					throw this.Error("Unterminated TOML escape sequence");
				}

				char escape = this.text[this.position++];
				result.Append(escape switch
				{
					'"' => '"',
					'\\' => '\\',
					'b' => '\b',
					'f' => '\f',
					'n' => '\n',
					'r' => '\r',
					't' => '\t',
					'u' => this.ParseUnicodeEscape(4),
					'U' => this.ParseUnicodeEscape(8),
					_ => throw this.Error($"Invalid TOML escape sequence '\\{escape}'"),
				});
			}

			throw this.Error("Unterminated TOML basic string");
		}

		private string ParseLiteralString()
		{
			this.Expect('\'');
			int start = this.position;
			while (this.position < this.text.Length && this.text[this.position] != '\'')
			{
				if (this.text[this.position] is '\r' or '\n')
				{
					throw this.Error("Newline in TOML literal string");
				}

				this.position++;
			}

			if (this.position >= this.text.Length)
			{
				throw this.Error("Unterminated TOML literal string");
			}

			string result = this.text[start..this.position];
			this.position++;
			return result;
		}

		private char ParseUnicodeEscape(int digits)
		{
			if (this.position + digits > this.text.Length || !int.TryParse(this.text.AsSpan(this.position, digits), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int value) || value > char.MaxValue)
			{
				throw this.Error("Invalid TOML Unicode escape");
			}

			this.position += digits;
			return (char)value;
		}

		private bool LooksLikeRootMap()
		{
			int savedPosition = this.position;
			try
			{
				this.ParseKey();
				this.SkipTrivia(includeNewlines: false);
				return this.position < this.text.Length && this.text[this.position] == '=';
			}
			catch (DecoderException)
			{
				return false;
			}
			finally
			{
				this.position = savedPosition;
			}
		}

		private void SkipTrivia(bool includeNewlines)
		{
			while (this.position < this.text.Length)
			{
				char character = this.text[this.position];
				if (character is ' ' or '\t' || includeNewlines && character is '\r' or '\n')
				{
					this.position++;
					continue;
				}

				if (character == '#')
				{
					while (this.position < this.text.Length && this.text[this.position] is not ('\r' or '\n'))
					{
						this.position++;
					}

					if (!includeNewlines)
					{
						return;
					}

					continue;
				}

				break;
			}
		}

		private void Expect(char expected)
		{
			if (!this.TryConsume(expected))
			{
				throw this.Error($"Expected '{expected}'");
			}
		}

		private bool TryConsume(char expected)
		{
			if (this.position < this.text.Length && this.text[this.position] == expected)
			{
				this.position++;
				return true;
			}

			return false;
		}

		private DecoderException Error(string message) => new($"{message} at character {this.position}.");

		private static bool LooksLikeNumber(string value)
		{
			ReadOnlySpan<char> span = value.AsSpan();
			if (span is "inf" or "+inf" or "-inf" or "nan" or "+nan" or "-nan")
			{
				return true;
			}

			span = value.Replace("_", string.Empty, StringComparison.Ordinal).AsSpan();
			return BigInteger.TryParse(span, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ||
				double.TryParse(span, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
		}

		private static bool IsBareKeyCharacter(char character)
			=> (character >= 'a' && character <= 'z') ||
				(character >= 'A' && character <= 'Z') ||
				(character >= '0' && character <= '9') ||
				character is '-' or '_';
	}
}
