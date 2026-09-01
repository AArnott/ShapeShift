// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

namespace ShapeShift.Json;

/// <summary>
/// A ShapeShift decoder that reads UTF-8 JSON.
/// </summary>
public ref struct JsonDecoder : IDecoder
{
	private readonly bool allowNamedFloatingPointValues;
	private readonly JsonReaderOptions options;
	private ReadOnlySpan<byte> unconsumed;
	private Utf8JsonReader reader;
	private bool hasToken;

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonDecoder"/> struct.
	/// </summary>
	/// <param name="json">The UTF-8 JSON document.</param>
	/// <param name="options">Options that control JSON tokenization.</param>
	/// <param name="allowNamedFloatingPointValues">Whether named non-finite floating-point strings are enabled.</param>
	public JsonDecoder(ReadOnlySpan<byte> json, JsonReaderOptions options = default, bool allowNamedFloatingPointValues = false)
	{
		this.options = options;
		this.unconsumed = json;
		this.reader = new(json, options);
		this.allowNamedFloatingPointValues = allowNamedFloatingPointValues;
		try
		{
			this.hasToken = this.reader.Read();
		}
		catch (JsonException ex)
		{
			throw new DecoderException("The input is not well-formed JSON.", ex);
		}
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

	/// <summary>
	/// Gets the type of the token the decoder is positioned at, or <see cref="JsonTokenType.None"/> once the
	/// document has been fully consumed.
	/// </summary>
	/// <remarks>
	/// <see cref="Utf8JsonReader.TokenType"/> keeps reporting the last token it read after
	/// <see cref="Utf8JsonReader.Read"/> returns <see langword="false" />, so every read path must consult this
	/// instead. Otherwise a read attempted past the end of the document is dispatched to the last token's
	/// accessor, which fails with whatever exception that accessor happens to throw rather than with a
	/// <see cref="DecoderException"/> that says the document ended.
	/// </remarks>
	private readonly JsonTokenType CurrentTokenType => this.hasToken ? this.reader.TokenType : JsonTokenType.None;

	/// <inheritdoc/>
	public bool TryReadNull()
	{
		if (this.NextTokenType != TokenType.Null)
		{
			return false;
		}

		this.ReadNull();
		return true;
	}

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

		try
		{
			this.reader.Skip();
		}
		catch (JsonException ex)
		{
			throw new DecoderException("The JSON value to skip is not well-formed.", ex);
		}

		this.MoveNext();
	}

	/// <inheritdoc/>
	public void ReadNull() => this.ReadExpected(JsonTokenType.Null);

	/// <inheritdoc/>
	public bool ReadBoolean()
	{
		bool result = this.CurrentTokenType switch
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
	public Int128 ReadInt128() => ParseNumber(this.ReadNumberText(), static text => Int128.Parse(text, CultureInfo.InvariantCulture));

	/// <inheritdoc/>
	public UInt128 ReadUInt128() => ParseNumber(this.ReadNumberText(), static text => UInt128.Parse(text, CultureInfo.InvariantCulture));

	/// <inheritdoc/>
	public Half ReadHalf() => (Half)this.ReadFloatingPoint(single: true);

	/// <inheritdoc/>
	public float ReadSingle() => (float)this.ReadFloatingPoint(single: true);

	/// <inheritdoc/>
	public double ReadDouble() => this.ReadFloatingPoint(single: false);

	/// <inheritdoc/>
	public decimal ReadDecimal() => this.ReadNumber(static reader => reader.GetDecimal());

	/// <inheritdoc/>
	public DateTime ReadDateTime()
	{
		if (this.CurrentTokenType != JsonTokenType.String || !this.reader.TryGetDateTime(out DateTime value))
		{
			throw this.Unexpected("an ISO 8601 date/time string");
		}

		this.MoveNext();
		return value;
	}

	/// <inheritdoc/>
	public TimeSpan ReadTimeSpan()
	{
		string text = this.ReadStringToken(JsonTokenType.String).ToString();
		if (!TimeSpan.TryParseExact(text, "c", CultureInfo.InvariantCulture, out TimeSpan value))
		{
			throw new DecoderException($"\"{text}\" is not a valid duration.");
		}

		return value;
	}

	/// <inheritdoc/>
	public BigInteger ReadBigInteger() => ParseNumber(this.ReadNumberText(), static text => BigInteger.Parse(text, CultureInfo.InvariantCulture));

	/// <inheritdoc/>
	public string ReadString() => this.ReadStringToken(JsonTokenType.String).ToString();

	/// <inheritdoc/>
	public ReadOnlySpan<char> ReadCharSpan() => this.ReadStringToken(JsonTokenType.String);

	/// <inheritdoc/>
	public byte[] ReadByteArray()
	{
		if (this.CurrentTokenType != JsonTokenType.String)
		{
			throw this.Unexpected("a base64 string");
		}

		byte[] value;
		try
		{
			value = this.reader.GetBytesFromBase64();
		}
		catch (FormatException ex)
		{
			throw new DecoderException("The JSON string is not valid base64.", ex);
		}

		this.MoveNext();
		return value;
	}

	/// <inheritdoc/>
	public ShapeShiftNumber ReadDynamicNumber()
	{
		if (this.CurrentTokenType != JsonTokenType.Number)
		{
			throw this.Unexpected("a number");
		}

		ShapeShiftNumber value;
		if (this.reader.TryGetInt64(out long signed))
		{
			value = new ShapeShiftInteger(signed);
		}
		else if (this.reader.TryGetUInt64(out ulong unsigned))
		{
			value = new ShapeShiftUnsignedInteger(unsigned);
		}
		else
		{
			string text = this.reader.HasValueSequence
				? Encoding.UTF8.GetString(this.reader.ValueSequence)
				: Encoding.UTF8.GetString(this.reader.ValueSpan);
			value = BigInteger.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out BigInteger integer)
				? new ShapeShiftBigInteger(integer)
				: decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal exact)
					? new ShapeShiftDecimal(exact)
					: double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double approximate)
						? new ShapeShiftFloat(approximate)
						: throw new DecoderException($"\"{text}\" is not a number this decoder can represent.");
		}

		this.MoveNext();
		return value;
	}

	/// <summary>
	/// Reads and clones the next complete JSON value.
	/// </summary>
	/// <returns>The detached JSON element.</returns>
	public JsonElement ReadJsonElement()
	{
		if (!this.hasToken)
		{
			throw this.Unexpected("a JSON value");
		}

		using JsonDocument document = ParseValue(ref this.reader);
		JsonElement value = document.RootElement.Clone();
		this.MoveNext();
		return value;
	}

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

	private static JsonDocument ParseValue(ref Utf8JsonReader reader)
	{
		try
		{
			return JsonDocument.ParseValue(ref reader);
		}
		catch (JsonException ex)
		{
			throw new DecoderException("The JSON value is not well-formed.", ex);
		}
	}

	private static T ParseNumber<T>(string text, Func<string, T> parse)
	{
		try
		{
			return parse(text);
		}
		catch (Exception ex) when (ex is FormatException or OverflowException)
		{
			throw new DecoderException($"\"{text}\" cannot be represented as a {typeof(T).Name}.", ex);
		}
	}

	/// <summary>
	/// Transcodes the current string or property-name token to UTF-16.
	/// </summary>
	/// <param name="reader">The reader positioned at the token.</param>
	/// <returns>The decoded text.</returns>
	/// <exception cref="DecoderException">Thrown when the token is not valid UTF-8 or contains an invalid escape sequence.</exception>
	/// <remarks>
	/// <see cref="Utf8JsonReader.GetString"/> reports invalid UTF-8 by throwing
	/// <see cref="InvalidOperationException"/>, which reads as a caller bug rather than as the bad input it
	/// actually is. Corrupted bytes reach here routinely, so the failure is translated into the
	/// <see cref="DecoderException"/> that the rest of the decoder promises.
	/// </remarks>
	private static string GetString(ref Utf8JsonReader reader)
	{
		try
		{
			return reader.GetString()!;
		}
		catch (Exception ex) when (ex is InvalidOperationException or JsonException)
		{
			throw new DecoderException("The JSON string is not valid UTF-8 text.", ex);
		}
	}

	private T ReadNumber<T>(Func<Utf8JsonReader, T> read)
	{
		if (this.CurrentTokenType != JsonTokenType.Number)
		{
			throw this.Unexpected("a number");
		}

		T value;
		try
		{
			value = read(this.reader);
		}
		catch (Exception ex) when (ex is FormatException or OverflowException)
		{
			throw new DecoderException($"The JSON number cannot be represented as a {typeof(T).Name}.", ex);
		}

		this.MoveNext();
		return value;
	}

	private double ReadFloatingPoint(bool single)
	{
		if (this.CurrentTokenType == JsonTokenType.Number)
		{
			return single ? this.ReadNumber(static reader => reader.GetSingle()) : this.ReadNumber(static reader => reader.GetDouble());
		}

		if (!this.allowNamedFloatingPointValues || this.CurrentTokenType != JsonTokenType.String)
		{
			throw this.Unexpected("a number");
		}

		string text = GetString(ref this.reader);
		double value = text switch
		{
			"NaN" => double.NaN,
			"Infinity" => double.PositiveInfinity,
			"-Infinity" => double.NegativeInfinity,
			_ => throw this.Unexpected("a named floating-point value"),
		};
		this.MoveNext();
		return value;
	}

	private string ReadNumberText()
	{
		if (this.CurrentTokenType != JsonTokenType.Number)
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
		if (this.CurrentTokenType != expected)
		{
			throw this.Unexpected(expected == JsonTokenType.PropertyName ? "a property name" : "a string");
		}

		string value = GetString(ref this.reader);
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

	private void MoveNext()
	{
		// Utf8JsonReader is a single-document reader: once it has produced a token that completes the
		// top-level value (a scalar read directly at depth 0, or the token that closes the top-level
		// map/vector back to depth 0), asking it to Read() again throws if any further, non-whitespace
		// content follows -- as it legitimately may for a stream of concatenated top-level values (e.g.
		// NDJSON). To support that, once such a boundary is crossed and genuine further content remains
		// (as opposed to nothing, or only trailing insignificant whitespace, both of which the current
		// reader already handles gracefully), hand off to a fresh reader over what remains, so each
		// top-level value is parsed by its own single-document reader.
		if (this.reader.TokenType is not JsonTokenType.None and not JsonTokenType.StartObject and not JsonTokenType.StartArray and not JsonTokenType.PropertyName
			&& this.reader.CurrentDepth == 0)
		{
			ReadOnlySpan<byte> remainder = this.unconsumed[checked((int)this.reader.BytesConsumed)..];
			if (!remainder.TrimStart(" \t\r\n"u8).IsEmpty)
			{
				this.unconsumed = remainder;
				this.reader = new(remainder, this.options);
			}
		}

		try
		{
			this.hasToken = this.reader.Read();
		}
		catch (JsonException ex)
		{
			throw new DecoderException("The JSON that follows the value just read is not well-formed.", ex);
		}
	}

	private readonly DecoderException Unexpected(string expected)
		=> new($"Expected {expected} but found {(this.hasToken ? this.reader.TokenType : "the end of the document")}.");
}
