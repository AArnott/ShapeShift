// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Toml;

/// <summary>
/// Rejects syntax that TOML 1.1 added and TOML 1.0 does not allow.
/// </summary>
internal static class TomlV1Syntax
{
	private enum Mode
	{
		Code,
		Comment,
		Basic,
		BasicMulti,
		Literal,
		LiteralMulti,
	}

	private enum Container
	{
		Array,
		InlineTable,
	}

	/// <summary>
	/// Throws when <paramref name="text"/> uses a TOML 1.1 extension.
	/// </summary>
	/// <param name="text">The TOML document.</param>
	/// <exception cref="DecoderException">The document uses syntax that TOML 1.0 forbids.</exception>
	/// <remarks>
	/// Tomlyn 2 accepts TOML 1.1. This scan keeps the reader on the TOML 1.0 grammar: no <c>\x</c> or <c>\e</c> escapes, no newlines or trailing commas in inline tables, and no times that omit seconds.
	/// </remarks>
	public static void RejectLaterExtensions(string text)
	{
		List<Container> containers = [];
		Mode mode = Mode.Code;
		bool lineStart = true;
		bool inHeader = false;

		for (int i = 0; i < text.Length; i++)
		{
			char c = text[i];
			if (mode == Mode.Comment)
			{
				if (c is '\n' or '\r')
				{
					mode = Mode.Code;
					i--;
				}

				continue;
			}

			if (mode is Mode.Basic or Mode.BasicMulti)
			{
				if (c == '\\')
				{
					if (i + 1 >= text.Length)
					{
						break;
					}

					char escaped = text[i + 1];
					if (escaped is 'x' or 'e')
					{
						throw new DecoderException("TOML 1.0 does not allow the \\x or \\e escape.");
					}

					i++;
					if (escaped == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
					{
						i++;
					}

					continue;
				}

				if (mode == Mode.Basic && c == '"')
				{
					mode = Mode.Code;
				}
				else if (mode == Mode.BasicMulti && TryConsumeMultilineClose(text, ref i, '"'))
				{
					mode = Mode.Code;
				}

				continue;
			}

			if (mode == Mode.Literal)
			{
				if (c == '\'')
				{
					mode = Mode.Code;
				}

				continue;
			}

			if (mode == Mode.LiteralMulti)
			{
				if (TryConsumeMultilineClose(text, ref i, '\''))
				{
					mode = Mode.Code;
				}

				continue;
			}

			if (c is ' ' or '\t' or '\uFEFF')
			{
				continue;
			}

			if (c is '\n' or '\r')
			{
				if (containers.Count > 0 && containers[^1] == Container.InlineTable)
				{
					throw new DecoderException("TOML 1.0 does not allow a newline in an inline table.");
				}

				if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
				{
					i++;
				}

				lineStart = true;
				inHeader = false;
				continue;
			}

			if (c == '#')
			{
				mode = Mode.Comment;
				lineStart = false;
				continue;
			}

			if (c == '"')
			{
				if (IsTriple(text, i, '"'))
				{
					mode = Mode.BasicMulti;
					i += 2;
				}
				else
				{
					mode = Mode.Basic;
				}

				lineStart = false;
				continue;
			}

			if (c == '\'')
			{
				if (IsTriple(text, i, '\''))
				{
					mode = Mode.LiteralMulti;
					i += 2;
				}
				else
				{
					mode = Mode.Literal;
				}

				lineStart = false;
				continue;
			}

			if (c == '{')
			{
				containers.Add(Container.InlineTable);
				lineStart = false;
				continue;
			}

			if (c == '}')
			{
				if (containers.Count > 0 && containers[^1] == Container.InlineTable)
				{
					containers.RemoveAt(containers.Count - 1);
				}

				lineStart = false;
				continue;
			}

			if (c == '[')
			{
				if (inHeader || (lineStart && containers.Count == 0))
				{
					inHeader = true;
				}
				else
				{
					containers.Add(Container.Array);
				}

				lineStart = false;
				continue;
			}

			if (c == ']')
			{
				if (!inHeader && containers.Count > 0 && containers[^1] == Container.Array)
				{
					containers.RemoveAt(containers.Count - 1);
				}

				lineStart = false;
				continue;
			}

			if (c == ',' && containers.Count > 0 && containers[^1] == Container.InlineTable && IsTrailingInlineComma(text, i + 1))
			{
				throw new DecoderException("TOML 1.0 does not allow a trailing comma in an inline table.");
			}

			if (c is >= '0' and <= '9' && IsTimeMissingSeconds(text, i))
			{
				throw new DecoderException("TOML 1.0 requires seconds in a time value.");
			}

			lineStart = false;
		}
	}

	private static bool IsTriple(string text, int index, char quote)
		=> index + 2 < text.Length && text[index] == quote && text[index + 1] == quote && text[index + 2] == quote;

	/// <summary>
	/// Closes a multiline string on a run of three, four, or five quotes. One or two quotes are body text. A longer run closes on the first three quotes. Counting stops at six quotes so a long malformed run stays a linear scan.
	/// </summary>
	/// <param name="text">The TOML document.</param>
	/// <param name="index">The index of the first quote. On success, the index of the last quote consumed.</param>
	/// <param name="quote">The quote character that delimits the string.</param>
	/// <returns><see langword="true"/> when the run closes the string.</returns>
	private static bool TryConsumeMultilineClose(string text, ref int index, char quote)
	{
		if (text[index] != quote)
		{
			return false;
		}

		int run = 1;
		while (run < 6 && index + run < text.Length && text[index + run] == quote)
		{
			run++;
		}

		if (run < 3)
		{
			return false;
		}

		int consumed = run > 5 ? 3 : run;
		index += consumed - 1;
		return true;
	}

	private static bool IsTrailingInlineComma(string text, int index)
	{
		while (index < text.Length)
		{
			char c = text[index];
			if (c is ' ' or '\t' or '\uFEFF')
			{
				index++;
				continue;
			}

			if (c == '#')
			{
				while (index < text.Length && text[index] is not '\n' and not '\r')
				{
					index++;
				}

				continue;
			}

			return c == '}';
		}

		return false;
	}

	private static bool IsTimeMissingSeconds(string text, int index)
	{
		if (index > 0)
		{
			char previous = text[index - 1];
			if (previous is (>= '0' and <= '9') or ':' or '+' or '-')
			{
				return false;
			}
		}

		if (!TryDigits(text, index, 2, out int afterHour) || afterHour >= text.Length || text[afterHour] != ':')
		{
			return false;
		}

		if (!TryDigits(text, afterHour + 1, 2, out int afterMinute))
		{
			return false;
		}

		return afterMinute >= text.Length || text[afterMinute] != ':';
	}

	private static bool TryDigits(string text, int index, int count, out int end)
	{
		end = index;
		for (int i = 0; i < count; i++)
		{
			if (end >= text.Length || text[end] is < '0' or > '9')
			{
				return false;
			}

			end++;
		}

		return true;
	}
}
