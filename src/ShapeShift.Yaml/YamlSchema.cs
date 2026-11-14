// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Numerics;

namespace ShapeShift.Yaml;

/// <summary>
/// The core-schema kind of a scalar.
/// </summary>
internal enum YamlScalarKind
{
	/// <summary>Text.</summary>
	String,

	/// <summary>Null.</summary>
	Null,

	/// <summary>A Boolean.</summary>
	Boolean,

	/// <summary>An integer.</summary>
	Integer,

	/// <summary>A floating-point number.</summary>
	Float,

	/// <summary>Base64 binary.</summary>
	Binary,
}

/// <summary>
/// A scalar resolved through the YAML 1.2 core schema.
/// </summary>
internal readonly struct YamlResolved
{
	/// <summary>Gets the resolved kind.</summary>
	public YamlScalarKind Kind { get; init; }

	/// <summary>Gets or initializes a value indicating whether the resolved Boolean is <see langword="true"/>.</summary>
	public bool Boolean { get; init; }

	/// <summary>Gets the integer, when <see cref="Kind"/> is <see cref="YamlScalarKind.Integer"/>.</summary>
	public BigInteger Integer { get; init; }

	/// <summary>Gets the floating-point value, when <see cref="Kind"/> is <see cref="YamlScalarKind.Float"/>.</summary>
	public double Float { get; init; }

	/// <summary>Gets the decoded bytes, when <see cref="Kind"/> is <see cref="YamlScalarKind.Binary"/>.</summary>
	public byte[]? Binary { get; init; }

	/// <summary>Gets the scalar text callers see for a string.</summary>
	public string Text { get; init; }
}

/// <summary>
/// Resolves scalars with the YAML 1.2 core schema.
/// </summary>
internal static class YamlSchema
{
	/// <summary>The YAML 1.2 string tag.</summary>
	public const string StrTag = "tag:yaml.org,2002:str";

	/// <summary>The YAML 1.2 null tag.</summary>
	public const string NullTag = "tag:yaml.org,2002:null";

	/// <summary>The YAML 1.2 Boolean tag.</summary>
	public const string BoolTag = "tag:yaml.org,2002:bool";

	/// <summary>The YAML 1.2 integer tag.</summary>
	public const string IntTag = "tag:yaml.org,2002:int";

	/// <summary>The YAML 1.2 float tag.</summary>
	public const string FloatTag = "tag:yaml.org,2002:float";

	/// <summary>The YAML 1.2 binary tag.</summary>
	public const string BinaryTag = "tag:yaml.org,2002:binary";

	/// <summary>The YAML 1.2 sequence tag.</summary>
	public const string SeqTag = "tag:yaml.org,2002:seq";

	/// <summary>The YAML 1.2 mapping tag.</summary>
	public const string MapTag = "tag:yaml.org,2002:map";

	/// <summary>
	/// Resolves <paramref name="node"/> for the decoder. Aliases must already have been followed.
	/// </summary>
	/// <param name="node">The scalar node.</param>
	/// <returns>The resolved value.</returns>
	/// <exception cref="DecoderException">The node carries a core tag its content does not satisfy.</exception>
	public static YamlResolved Resolve(YamlNode node)
	{
		string text = node.Scalar;
		string? tag = node.Tag;
		if (tag == BinaryTag)
		{
			return new YamlResolved { Kind = YamlScalarKind.Binary, Binary = DecodeBase64(text), Text = text };
		}

		if (tag == StrTag)
		{
			return new YamlResolved { Kind = YamlScalarKind.String, Text = text };
		}

		if (tag == NullTag)
		{
			if (!IsNullText(text) && text.Length != 0)
			{
				throw new DecoderException($"The tag {NullTag} cannot represent '{text}'.");
			}

			return new YamlResolved { Kind = YamlScalarKind.Null, Text = text };
		}

		if (tag == BoolTag)
		{
			if (!TryBool(text, out bool boolean))
			{
				throw new DecoderException($"The tag {BoolTag} cannot represent '{text}'.");
			}

			return new YamlResolved { Kind = YamlScalarKind.Boolean, Boolean = boolean, Text = text };
		}

		if (tag == IntTag)
		{
			if (!TryInt(text, out BigInteger integer))
			{
				throw new DecoderException($"The tag {IntTag} cannot represent '{text}'.");
			}

			return new YamlResolved { Kind = YamlScalarKind.Integer, Integer = integer, Text = text };
		}

		if (tag == FloatTag)
		{
			// An explicit !!float accepts a decimal integer spelling. Implicit resolution still prefers !!int.
			if (!TryFloat(text, out double number, allowInteger: true))
			{
				throw new DecoderException($"The tag {FloatTag} cannot represent '{text}'.");
			}

			return new YamlResolved { Kind = YamlScalarKind.Float, Float = number, Text = text };
		}

		// The non-specific "!" tag disables schema resolution and forces a string.
		if (tag == "!")
		{
			return new YamlResolved { Kind = YamlScalarKind.String, Text = text };
		}

		bool plain = node.Style == YamlScalarStyle.Plain && tag is null;
		if (!plain)
		{
			return new YamlResolved { Kind = YamlScalarKind.String, Text = text };
		}

		if (IsNullText(text))
		{
			return new YamlResolved { Kind = YamlScalarKind.Null, Text = text };
		}

		if (TryBool(text, out bool coreBool))
		{
			return new YamlResolved { Kind = YamlScalarKind.Boolean, Boolean = coreBool, Text = text };
		}

		if (TryInt(text, out BigInteger coreInt))
		{
			return new YamlResolved { Kind = YamlScalarKind.Integer, Integer = coreInt, Text = text };
		}

		if (TryFloat(text, out double coreFloat, allowInteger: false))
		{
			return new YamlResolved { Kind = YamlScalarKind.Float, Float = coreFloat, Text = text };
		}

		return new YamlResolved { Kind = YamlScalarKind.String, Text = text };
	}

	/// <summary>
	/// Reports whether an unquoted scalar uses the decimal form the YAML encoder writes for numbers.
	/// </summary>
	/// <param name="span">The scalar text.</param>
	/// <returns><see langword="true"/> when the text is a decimal integer or floating-point literal.</returns>
	internal static bool LooksLikeDecimalNumber(ReadOnlySpan<char> span)
	{
		if (span.IsEmpty)
		{
			return false;
		}

		int i = 0;
		if (span[i] is '-' or '+')
		{
			i++;
		}

		int integerDigits = 0;
		while (i < span.Length && char.IsAsciiDigit(span[i]))
		{
			i++;
			integerDigits++;
		}

		int fractionDigits = 0;
		if (i < span.Length && span[i] == '.')
		{
			i++;
			while (i < span.Length && char.IsAsciiDigit(span[i]))
			{
				i++;
				fractionDigits++;
			}
		}

		if (integerDigits == 0 && fractionDigits == 0)
		{
			return false;
		}

		if (i < span.Length && span[i] is 'e' or 'E')
		{
			i++;
			if (i < span.Length && span[i] is '-' or '+')
			{
				i++;
			}

			int exponentDigits = 0;
			while (i < span.Length && char.IsAsciiDigit(span[i]))
			{
				i++;
				exponentDigits++;
			}

			if (exponentDigits == 0)
			{
				return false;
			}
		}

		return i == span.Length;
	}

	/// <summary>
	/// Reports whether an unquoted scalar is a core-schema value the decimal-number check does not cover.
	/// </summary>
	/// <param name="span">The scalar text.</param>
	/// <returns>
	/// <see langword="true"/> for hexadecimal and octal integers and for the core infinity and NaN spellings.
	/// </returns>
	internal static bool LooksLikeNonDecimalCore(ReadOnlySpan<char> span)
	{
		if (span.IsEmpty)
		{
			return false;
		}

		int i = 0;
		if (span[0] is '+' or '-')
		{
			i = 1;
		}

		if (i >= span.Length)
		{
			return false;
		}

		ReadOnlySpan<char> body = span[i..];
		if (body is ".inf" or ".Inf" or ".INF")
		{
			return true;
		}

		if (i == 0 && body is ".nan" or ".NaN" or ".NAN")
		{
			return true;
		}

		if (body.Length < 3 || body[0] != '0' || body[1] is not ('x' or 'X' or 'o' or 'O'))
		{
			return false;
		}

		int radix = body[1] is 'x' or 'X' ? 16 : 8;
		for (int k = 2; k < body.Length; k++)
		{
			int digit = HexValue(body[k]);
			if (digit < 0 || digit >= radix)
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Builds a canonical identity for a scalar mapping key.
	/// </summary>
	/// <param name="node">The scalar. Aliases must already have been followed.</param>
	/// <returns>A string that is equal for schema-equal scalars and different otherwise.</returns>
	internal static string CanonicalScalar(YamlNode node)
	{
		YamlResolved resolved = Resolve(node);
		switch (resolved.Kind)
		{
			case YamlScalarKind.Null:
				return "n";
			case YamlScalarKind.Boolean:
				return resolved.Boolean ? "b1" : "b0";
			case YamlScalarKind.Integer:
				return "i" + resolved.Integer.ToString(CultureInfo.InvariantCulture);
			case YamlScalarKind.Float:
				if (double.IsNaN(resolved.Float))
				{
					return "fnan";
				}

				if (double.IsPositiveInfinity(resolved.Float))
				{
					return "finf";
				}

				if (double.IsNegativeInfinity(resolved.Float))
				{
					return "f-inf";
				}

				return "f" + resolved.Float.ToString("R", CultureInfo.InvariantCulture);
			case YamlScalarKind.Binary:
				return "y" + Convert.ToBase64String(resolved.Binary ?? []);
			default:
				string text = resolved.Text ?? node.Scalar;
				if (node.Tag is { } tag && tag is not ("!" or StrTag))
				{
					return "t" + tag + "=" + text;
				}

				return "s" + text;
		}
	}

	private static bool IsNullText(string text)
		=> text.Length == 0 || text == "~" || text.Equals("null", StringComparison.OrdinalIgnoreCase);

	private static bool TryBool(string text, out bool value)
	{
		if (text.Equals("true", StringComparison.OrdinalIgnoreCase))
		{
			value = true;
			return true;
		}

		if (text.Equals("false", StringComparison.OrdinalIgnoreCase))
		{
			value = false;
			return true;
		}

		value = false;
		return false;
	}

	private static bool TryInt(string text, out BigInteger value)
	{
		value = default;
		if (text.Length == 0)
		{
			return false;
		}

		int i = 0;
		bool negative = false;
		if (text[i] is '+' or '-')
		{
			negative = text[i] == '-';
			i++;
		}

		if (i >= text.Length)
		{
			return false;
		}

		if (text[i] == '0' && i + 1 < text.Length && text[i + 1] is 'x' or 'X' or 'o' or 'O')
		{
			char kind = text[i + 1];
			int start = i + 2;
			if (start >= text.Length)
			{
				return false;
			}

			int radix = kind is 'x' or 'X' ? 16 : 8;
			BigInteger parsed = 0;
			for (int k = start; k < text.Length; k++)
			{
				int digit = HexValue(text[k]);
				if (digit < 0 || digit >= radix)
				{
					return false;
				}

				parsed = (parsed * radix) + digit;
			}

			value = negative ? -parsed : parsed;
			return true;
		}

		for (int k = i; k < text.Length; k++)
		{
			if (!char.IsAsciiDigit(text[k]))
			{
				return false;
			}
		}

		if (!BigInteger.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value))
		{
			return false;
		}

		return true;
	}

	private static bool TryFloat(string text, out double value, bool allowInteger)
	{
		value = default;
		if (text.Length == 0)
		{
			return false;
		}

		if (text is ".nan" or ".NaN" or ".NAN")
		{
			value = double.NaN;
			return true;
		}

		ReadOnlySpan<char> span = text.AsSpan();
		int i = 0;
		if (span[0] is '+' or '-')
		{
			i = 1;
		}

		ReadOnlySpan<char> body = span[i..];
		if (body is ".inf" or ".Inf" or ".INF")
		{
			value = span[0] == '-' ? double.NegativeInfinity : double.PositiveInfinity;
			return true;
		}

		bool dot = false;
		bool digit = false;
		int k = i;
		if (k < span.Length && span[k] == '.')
		{
			dot = true;
			k++;
		}

		int digits = 0;
		while (k < span.Length && char.IsAsciiDigit(span[k]))
		{
			digits++;
			k++;
		}

		if (digits == 0 && !dot)
		{
			return false;
		}

		digit = digits > 0;
		if (k < span.Length && span[k] == '.')
		{
			if (dot)
			{
				return false;
			}

			dot = true;
			k++;
			while (k < span.Length && char.IsAsciiDigit(span[k]))
			{
				digit = true;
				k++;
			}
		}

		if (!digit || (!dot && k >= span.Length))
		{
			// Integers are not floats. A float needs a dot or an exponent.
			if (!digit)
			{
				return false;
			}
		}

		if (k < span.Length && span[k] is 'e' or 'E')
		{
			k++;
			if (k < span.Length && span[k] is '+' or '-')
			{
				k++;
			}

			int exp = 0;
			while (k < span.Length && char.IsAsciiDigit(span[k]))
			{
				exp++;
				k++;
			}

			if (exp == 0)
			{
				return false;
			}
		}

		if (!allowInteger && !dot && text.IndexOf('e') < 0 && text.IndexOf('E') < 0)
		{
			return false;
		}

		if (k != span.Length)
		{
			return false;
		}

		return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
	}

	private static byte[] DecodeBase64(string text)
	{
		Span<char> filtered = text.Length <= 256 ? stackalloc char[text.Length] : new char[text.Length];
		int n = 0;
		foreach (char c in text)
		{
			if (!char.IsWhiteSpace(c))
			{
				filtered[n++] = c;
			}
		}

		try
		{
			return Convert.FromBase64String(filtered[..n].ToString());
		}
		catch (FormatException ex)
		{
			throw new DecoderException("The !!binary scalar is not valid base64.", ex);
		}
	}

	private static int HexValue(char c)
		=> c switch
		{
			>= '0' and <= '9' => c - '0',
			>= 'a' and <= 'f' => c - 'a' + 10,
			>= 'A' and <= 'F' => c - 'A' + 10,
			_ => -1,
		};
}
