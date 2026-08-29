// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

namespace ShapeShift.Json;

/// <summary>
/// A ShapeShift decoder that reads UTF-8 JSON.
/// </summary>
public ref struct JsonDecoder : IDecoder
{
	private Utf8JsonReader reader;
	private bool hasToken;

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonDecoder"/> struct.
	/// </summary>
	/// <param name="json">The UTF-8 JSON document.</param>
	/// <param name="options">Options that control JSON tokenization.</param>
	public JsonDecoder(ReadOnlySpan<byte> json, JsonReaderOptions options = default)
	{
		this.reader = new(json, options);
		this.hasToken = this.reader.Read();
	}

	/// <summary>
	/// Gets a copy of the underlying reader positioned at the next token.
	/// </summary>
	public readonly Utf8JsonReader Reader => this.reader;

	/// <inheritdoc/>
	public readonly TokenType NextTokenType => this.hasToken ? this.reader.TokenType switch
	{
		JsonTokenType.StartObject => TokenType.StartMap,
		JsonTokenType.EndObject => TokenType.EndMap,
		JsonTokenType.StartArray => TokenType.StartVector,
		JsonTokenType.EndArray => TokenType.EndVector,
		JsonTokenType.PropertyName => TokenType.PropertyName,
		JsonTokenType.Null => TokenType.Null,
		JsonTokenType.Number => TokenType.Number,
		JsonTokenType.String => TokenType.String,
		JsonTokenType.True or JsonTokenType.False => TokenType.Boolean,
		_ => throw new DecoderException($"Unsupported JSON token {this.reader.TokenType}."),
	} : TokenType.EndDocument;

	/// <inheritdoc/>
	public readonly bool TryReadNull() => this.NextTokenType == TokenType.Null;

	/// <inheritdoc/>
	public int? ReadStartMap()
	{
		this.ReadExpected(JsonTokenType.StartObject);
		return null;
	}

	/// <inheritdoc/>
	public void ReadEndMap() => this.ReadExpected(JsonTokenType.EndObject);

	/// <inheritdoc/>
	public int? ReadStartVector()
	{
		this.ReadExpected(JsonTokenType.StartArray);
		return null;
	}

	/// <inheritdoc/>
	public void ReadEndVector() => this.ReadExpected(JsonTokenType.EndArray);

	/// <inheritdoc/>
	public ReadOnlySpan<char> ReadPropertyName() => this.ReadStringToken(JsonTokenType.PropertyName);

	/// <inheritdoc/>
	public void Skip()
	{
		if (!this.hasToken)
		{
			throw new DecoderException("Cannot skip beyond the end of the JSON document.");
		}

		this.reader.Skip();
		this.MoveNext();
	}

	/// <inheritdoc/>
	public void ReadNull() => this.ReadExpected(JsonTokenType.Null);

	/// <inheritdoc/>
	public bool ReadBoolean()
	{
		bool result = this.reader.TokenType switch
		{
			JsonTokenType.True => true,
			JsonTokenType.False => false,
			_ => throw this.Unexpected("a boolean"),
		};
		this.MoveNext();
		return result;
	}

	/// <inheritdoc/>
	public long ReadInt64() => this.ReadNumber(static reader => reader.GetInt64());

	/// <inheritdoc/>
	public ulong ReadUInt64() => this.ReadNumber(static reader => reader.GetUInt64());

	/// <inheritdoc/>
	public Int128 ReadInt128() => Int128.Parse(this.ReadNumberText(), CultureInfo.InvariantCulture);

	/// <inheritdoc/>
	public UInt128 ReadUInt128() => UInt128.Parse(this.ReadNumberText(), CultureInfo.InvariantCulture);

	/// <inheritdoc/>
	public Half ReadHalf() => (Half)this.ReadNumber(static reader => reader.GetSingle());

	/// <inheritdoc/>
	public float ReadSingle() => this.ReadNumber(static reader => reader.GetSingle());

	/// <inheritdoc/>
	public double ReadDouble() => this.ReadNumber(static reader => reader.GetDouble());

	/// <inheritdoc/>
	public decimal ReadDecimal() => this.ReadNumber(static reader => reader.GetDecimal());

	/// <inheritdoc/>
	public DateTime ReadDateTime()
	{
		if (this.reader.TokenType != JsonTokenType.String || !this.reader.TryGetDateTime(out DateTime value))
		{
			throw this.Unexpected("an ISO 8601 date/time string");
		}

		this.MoveNext();
		return value;
	}

	/// <inheritdoc/>
	public TimeSpan ReadTimeSpan()
	{
		ReadOnlySpan<char> text = this.ReadStringToken(JsonTokenType.String);
		return TimeSpan.ParseExact(text, "c", CultureInfo.InvariantCulture);
	}

	/// <inheritdoc/>
	public BigInteger ReadBigInteger() => BigInteger.Parse(this.ReadNumberText(), CultureInfo.InvariantCulture);

	/// <inheritdoc/>
	public string ReadString() => this.ReadStringToken(JsonTokenType.String).ToString();

	/// <inheritdoc/>
	public ReadOnlySpan<char> ReadCharSpan() => this.ReadStringToken(JsonTokenType.String);

	/// <summary>
	/// Verifies that the complete JSON document was consumed.
	/// </summary>
	/// <exception cref="DecoderException">Thrown when another token follows the deserialized value.</exception>
	public readonly void EnsureEndOfDocument()
	{
		if (this.hasToken)
		{
			throw new DecoderException("Additional JSON content follows the deserialized value.");
		}
	}

	private T ReadNumber<T>(Func<Utf8JsonReader, T> read)
	{
		if (this.reader.TokenType != JsonTokenType.Number)
		{
			throw this.Unexpected("a number");
		}

		T value = read(this.reader);
		this.MoveNext();
		return value;
	}

	private string ReadNumberText()
	{
		if (this.reader.TokenType != JsonTokenType.Number)
		{
			throw this.Unexpected("a number");
		}

		string value = this.reader.HasValueSequence
			? Encoding.UTF8.GetString(this.reader.ValueSequence)
			: Encoding.UTF8.GetString(this.reader.ValueSpan);
		this.MoveNext();
		return value;
	}

	private ReadOnlySpan<char> ReadStringToken(JsonTokenType expected)
	{
		if (this.reader.TokenType != expected)
		{
			throw this.Unexpected(expected == JsonTokenType.PropertyName ? "a property name" : "a string");
		}

		string value = this.reader.GetString()!;
		this.MoveNext();
		return value;
	}

	private void ReadExpected(JsonTokenType expected)
	{
		if (!this.hasToken || this.reader.TokenType != expected)
		{
			throw this.Unexpected(expected.ToString());
		}

		this.MoveNext();
	}

	private void MoveNext() => this.hasToken = this.reader.Read();

	private readonly DecoderException Unexpected(string expected)
		=> new($"Expected {expected} but found {(this.hasToken ? this.reader.TokenType : "the end of the document")}.");
}
