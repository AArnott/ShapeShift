// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Json;

/// <summary>
/// Escapes exactly those string characters that RFC 8259 requires JSON writers to escape.
/// </summary>
internal sealed unsafe class Rfc8259JavaScriptEncoder : System.Text.Encodings.Web.JavaScriptEncoder
{
	private const string HexDigits = "0123456789ABCDEF";

	/// <inheritdoc/>
	public override int MaxOutputCharactersPerInputCharacter => 6;

	/// <inheritdoc/>
	public override int FindFirstCharacterToEncode(char* text, int textLength)
	{
		ArgumentNullException.ThrowIfNull(text);

		for (int i = 0; i < textLength; i++)
		{
			char character = text[i];
			if (character is '"' or '\\' || character <= '\u001F')
			{
				return i;
			}

			if (char.IsHighSurrogate(character))
			{
				if (i + 1 >= textLength || !char.IsLowSurrogate(text[i + 1]))
				{
					return i;
				}

				i++;
			}
			else if (char.IsLowSurrogate(character))
			{
				return i;
			}
		}

		return -1;
	}

	/// <inheritdoc/>
	public override bool WillEncode(int unicodeScalar)
		=> unicodeScalar is '"' or '\\' || unicodeScalar <= 0x1F;

	/// <inheritdoc/>
	public override bool TryEncodeUnicodeScalar(int unicodeScalar, char* buffer, int bufferLength, out int numberOfCharactersWritten)
	{
		ArgumentNullException.ThrowIfNull(buffer);

		switch (unicodeScalar)
		{
			case '"':
				return TryWriteTwoCharacters('\\', '"', buffer, bufferLength, out numberOfCharactersWritten);
			case '\\':
				return TryWriteTwoCharacters('\\', '\\', buffer, bufferLength, out numberOfCharactersWritten);
			case <= 0x1F:
				if (bufferLength < 6)
				{
					numberOfCharactersWritten = 0;
					return false;
				}

				buffer[0] = '\\';
				buffer[1] = 'u';
				buffer[2] = '0';
				buffer[3] = '0';
				buffer[4] = HexDigits[unicodeScalar >> 4];
				buffer[5] = HexDigits[unicodeScalar & 0xF];
				numberOfCharactersWritten = 6;
				return true;
			default:
				numberOfCharactersWritten = 0;
				return false;
		}
	}

	private static bool TryWriteTwoCharacters(char first, char second, char* buffer, int bufferLength, out int numberOfCharactersWritten)
	{
		if (bufferLength < 2)
		{
			numberOfCharactersWritten = 0;
			return false;
		}

		buffer[0] = first;
		buffer[1] = second;
		numberOfCharactersWritten = 2;
		return true;
	}
}
