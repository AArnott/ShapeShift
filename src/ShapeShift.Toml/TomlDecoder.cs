// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Numerics;
using Tomlyn.Syntax;

namespace ShapeShift.Toml;

/// <summary>
/// A ShapeShift-compatible TOML 1.0 decoder.
/// </summary>
public ref struct TomlDecoder : IDecoder
{
	private readonly Token[] tokens;
	private int position;

	/// <summary>
	/// Initializes a new instance of the <see cref="TomlDecoder"/> struct.
	/// </summary>
	/// <param name="reader">The underlying text reader from which to get the TOML.</param>
	public TomlDecoder(TextReader reader)
	{
		ArgumentNullException.ThrowIfNull(reader);
		this.tokens = Parse(reader.ReadToEnd());
	}

	/// <inheritdoc/>
	public TokenType NextTokenType => this.position < this.tokens.Length ? this.tokens[this.position].Type : TokenType.EndDocument;

	/// <inheritdoc/>
	public bool TryReadNull() => false;

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
	public ReadOnlySpan<char> ReadPropertyName() => this.ReadToken(TokenType.PropertyName).Text;

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
	public void ReadNull() => throw new DecoderException("TOML has no null value.");

	/// <inheritdoc/>
	public bool ReadBoolean() => this.ReadToken(TokenType.Boolean).Value is true;

	/// <inheritdoc/>
	public long ReadInt64() => this.ReadInteger<long>(long.TryParse, "Int64");

	/// <inheritdoc/>
	public ulong ReadUInt64() => this.ReadInteger<ulong>(ulong.TryParse, "UInt64");

	/// <inheritdoc/>
	public Int128 ReadInt128() => this.ReadInteger<Int128>(Int128.TryParse, "Int128");

	/// <inheritdoc/>
	public UInt128 ReadUInt128() => this.ReadInteger<UInt128>(UInt128.TryParse, "UInt128");

	/// <inheritdoc/>
	public BigInteger ReadBigInteger() => this.ReadInteger<BigInteger>(BigInteger.TryParse, "BigInteger");

	/// <inheritdoc/>
	public Half ReadHalf() => this.ReadFloatingPoint<Half>(Half.TryParse, "Half");

	/// <inheritdoc/>
	public float ReadSingle() => this.ReadFloatingPoint<float>(float.TryParse, "Single");

	/// <inheritdoc/>
	public double ReadDouble() => this.ReadFloatingPoint<double>(double.TryParse, "Double");

	/// <inheritdoc/>
	public decimal ReadDecimal() => this.ReadFloatingPoint<decimal>(decimal.TryParse, "Decimal");

	/// <inheritdoc/>
	public DateTime ReadDateTime()
	{
		Token token = this.ReadToken(TokenType.String);
		if (token.Value is Tomlyn.TomlDateTime tomlDateTime)
		{
			return tomlDateTime.Kind is Tomlyn.TomlDateTimeKind.OffsetDateTimeByZ or Tomlyn.TomlDateTimeKind.OffsetDateTimeByNumber
				? tomlDateTime.DateTime.UtcDateTime
				: tomlDateTime.DateTime.DateTime;
		}

		throw new DecoderException("Expected a TOML date/time value.");
	}

	/// <inheritdoc/>
	public TimeSpan ReadTimeSpan() => throw new NotSupportedException("TOML has no duration value.");

	/// <inheritdoc/>
	public string ReadString()
	{
		Token token = this.ReadToken(TokenType.String);
		return token.Value is string value ? value : throw new DecoderException("Expected a TOML string value.");
	}

	/// <inheritdoc/>
	public ReadOnlySpan<char> ReadCharSpan()
	{
		Token token = this.ReadToken(TokenType.String);
		return token.Value is string value ? value.AsSpan() : throw new DecoderException("Expected a TOML string value.");
	}

	/// <inheritdoc/>
	public byte[] ReadByteArray() => throw new NotSupportedException("TOML has no binary value.");

	/// <inheritdoc/>
	public ShapeShiftNumber ReadDynamicNumber()
	{
		Token token = this.ReadToken(TokenType.Number);
		return token.Value switch
		{
			long value => new ShapeShiftInteger(value),
			double value => new ShapeShiftFloat(value),
			_ => throw new DecoderException("Unsupported TOML number representation."),
		};
	}

#pragma warning disable SA1201
	private delegate bool NumberParser<T>(ReadOnlySpan<char> value, NumberStyles style, IFormatProvider? provider, out T result);
#pragma warning restore SA1201

	private T ReadInteger<T>(NumberParser<T> parser, string typeName)
	{
		Token token = this.ReadToken(TokenType.Number);
		return parser(token.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out T result)
			? result
			: throw new DecoderException($"The TOML number is not a valid {typeName} value.");
	}

	private T ReadFloatingPoint<T>(NumberParser<T> parser, string typeName)
	{
		Token token = this.ReadToken(TokenType.Number);
		string text = token.Value switch
		{
			double value when double.IsPositiveInfinity(value) => "Infinity",
			double value when double.IsNegativeInfinity(value) => "-Infinity",
			double value when double.IsNaN(value) => "NaN",
			_ => token.Text,
		};
		return parser(text, NumberStyles.Float, CultureInfo.InvariantCulture, out T result)
			? result
			: throw new DecoderException($"The TOML number is not a valid {typeName} value.");
	}

	private Token ReadToken(TokenType expectedType)
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
		return token;
	}

#pragma warning disable SA1204 // Static model adapters follow the instance token reader they support.
	private static Token[] Parse(string text)
	{
		DocumentSyntax document = Tomlyn.Toml.Parse(text);
		if (document.HasErrors)
		{
			throw new DecoderException($"The input is not well-formed TOML 1.0.{Environment.NewLine}{document.Diagnostics}");
		}

		Dictionary<string, object> root = [];
		AddKeyValues(root, document.KeyValues);
		foreach (TableSyntaxBase tableSyntax in document.Tables)
		{
			Dictionary<string, object> table = GetTable(root, GetKey(tableSyntax.Name ?? throw InvalidSyntaxTree()), tableSyntax is TableArraySyntax);
			AddKeyValues(table, tableSyntax.Items);
		}

		List<Token> tokens = [];
		AppendTable(tokens, root);
		return [.. tokens];
	}

	private static void AddKeyValues(Dictionary<string, object> table, SyntaxList<KeyValueSyntax> keyValues)
	{
		foreach (KeyValueSyntax keyValue in keyValues)
		{
			AddValue(table, GetKey(keyValue.Key ?? throw InvalidSyntaxTree()), ConvertValue(keyValue.Value ?? throw InvalidSyntaxTree()));
		}
	}

	private static void AddValue(Dictionary<string, object> table, List<string> path, object value)
	{
		for (int i = 0; i < path.Count - 1; i++)
		{
			table = GetOrAddTable(table, path[i]);
		}

		table.Add(path[^1], value);
	}

	private static Dictionary<string, object> GetTable(Dictionary<string, object> root, List<string> path, bool isTableArray)
	{
		Dictionary<string, object> table = root;
		for (int i = 0; i < path.Count - 1; i++)
		{
			table = GetOrAddTable(table, path[i]);
		}

		string name = path[^1];
		if (isTableArray)
		{
			if (!table.TryGetValue(name, out object? value))
			{
				value = new List<object>();
				table.Add(name, value);
			}

			List<object> tables = (List<object>)value;
			Dictionary<string, object> item = [];
			tables.Add(item);
			return item;
		}

		return GetOrAddTable(table, name);
	}

	private static Dictionary<string, object> GetOrAddTable(Dictionary<string, object> table, string name)
	{
		if (!table.TryGetValue(name, out object? value))
		{
			Dictionary<string, object> child = [];
			table.Add(name, child);
			return child;
		}

		return value switch
		{
			Dictionary<string, object> child => child,
			List<object> { Count: > 0 } tables => (Dictionary<string, object>)tables[^1],
			_ => throw new DecoderException($"The TOML key '{name}' is not a table."),
		};
	}

	private static List<string> GetKey(KeySyntax key)
	{
		List<string> path = [GetKeyPart(key.Key ?? throw InvalidSyntaxTree())];
		foreach (DottedKeyItemSyntax item in key.DotKeys)
		{
			path.Add(GetKeyPart(item.Key ?? throw InvalidSyntaxTree()));
		}

		return path;
	}

	private static string GetKeyPart(BareKeyOrStringValueSyntax key) => key switch
	{
		BareKeySyntax bareKey => bareKey.Key?.Text ?? throw InvalidSyntaxTree(),
		StringValueSyntax stringKey => stringKey.Value ?? throw InvalidSyntaxTree(),
		_ => throw new DecoderException("Unsupported TOML key syntax."),
	};

	private static object ConvertValue(ValueSyntax value) => value switch
	{
		StringValueSyntax text => text.Value ?? throw InvalidSyntaxTree(),
		IntegerValueSyntax integer => integer.Value,
		FloatValueSyntax floatingPoint => floatingPoint.Value,
		BooleanValueSyntax boolean => boolean.Value,
		DateTimeValueSyntax dateTime => dateTime.Value,
		ArraySyntax array => array.Items.Select(static item => ConvertValue(item.Value ?? throw InvalidSyntaxTree())).ToList(),
		InlineTableSyntax inlineTable => ConvertInlineTable(inlineTable),
		_ => throw new DecoderException($"Unsupported TOML value syntax {value.GetType().FullName}."),
	};

	private static Dictionary<string, object> ConvertInlineTable(InlineTableSyntax syntax)
	{
		Dictionary<string, object> table = [];
		foreach (InlineTableItemSyntax item in syntax.Items)
		{
			KeyValueSyntax keyValue = item.KeyValue ?? throw InvalidSyntaxTree();
			AddValue(table, GetKey(keyValue.Key ?? throw InvalidSyntaxTree()), ConvertValue(keyValue.Value ?? throw InvalidSyntaxTree()));
		}

		return table;
	}

	private static DecoderException InvalidSyntaxTree() => new("Tomlyn returned an incomplete syntax tree for valid TOML.");

	private static void AppendTable(List<Token> tokens, Dictionary<string, object> table)
	{
		tokens.Add(new(TokenType.StartMap));
		foreach ((string name, object value) in table)
		{
			tokens.Add(new(TokenType.PropertyName, name, name));
			AppendValue(tokens, value);
		}

		tokens.Add(new(TokenType.EndMap));
	}

	private static void AppendVector(List<Token> tokens, IEnumerable<object> values)
	{
		tokens.Add(new(TokenType.StartVector));
		foreach (object value in values)
		{
			AppendValue(tokens, value);
		}

		tokens.Add(new(TokenType.EndVector));
	}

	private static void AppendValue(List<Token> tokens, object value)
	{
		switch (value)
		{
			case Dictionary<string, object> table:
				AppendTable(tokens, table);
				break;
			case List<object> array:
				AppendVector(tokens, array);
				break;
			case bool boolean:
				tokens.Add(new(TokenType.Boolean, boolean ? "true" : "false", boolean));
				break;
			case long integer:
				tokens.Add(new(TokenType.Number, integer.ToString(CultureInfo.InvariantCulture), integer));
				break;
			case double floatingPoint:
				tokens.Add(new(TokenType.Number, floatingPoint.ToString("R", CultureInfo.InvariantCulture), floatingPoint));
				break;
			case string text:
				tokens.Add(new(TokenType.String, text, text));
				break;
			case Tomlyn.TomlDateTime dateTime:
				tokens.Add(new(TokenType.String, dateTime.ToString(), dateTime));
				break;
			default:
				throw new DecoderException($"Unsupported TOML model value {value.GetType().FullName}.");
		}
	}

	private readonly record struct Token(TokenType Type, string Text = "", object? Value = null);
#pragma warning restore SA1204
}
