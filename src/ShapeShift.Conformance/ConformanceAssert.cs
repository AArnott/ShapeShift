// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace ShapeShift.Conformance;

/// <summary>
/// The assertions the conformance suites are written against.
/// </summary>
/// <remarks>
/// These are intentionally minimal and framework-neutral. Format authors writing their own cases
/// through <see cref="FormatConformanceAdapter{TEncoder, TDecoder}.AddFormatSpecificTests"/> may
/// use them, or any assertion library they prefer.
/// </remarks>
public static class ConformanceAssert
{
	/// <summary>
	/// Asserts that a condition holds.
	/// </summary>
	/// <param name="condition">The condition that must be <see langword="true" />.</param>
	/// <param name="message">A description of what the condition means.</param>
	/// <exception cref="ConformanceAssertionException">Thrown when <paramref name="condition"/> is <see langword="false" />.</exception>
	public static void True([DoesNotReturnIf(false)] bool condition, string message)
	{
		if (!condition)
		{
			throw new ConformanceAssertionException(message);
		}
	}

	/// <summary>
	/// Asserts that a condition does not hold.
	/// </summary>
	/// <param name="condition">The condition that must be <see langword="false" />.</param>
	/// <param name="message">A description of what the condition means.</param>
	/// <exception cref="ConformanceAssertionException">Thrown when <paramref name="condition"/> is <see langword="true" />.</exception>
	public static void False([DoesNotReturnIf(true)] bool condition, string message)
		=> True(!condition, message);

	/// <summary>
	/// Asserts that two values are equal.
	/// </summary>
	/// <typeparam name="T">The type of value being compared.</typeparam>
	/// <param name="expected">The expected value.</param>
	/// <param name="actual">The observed value.</param>
	/// <param name="context">A description of what produced <paramref name="actual"/>.</param>
	/// <exception cref="ConformanceAssertionException">Thrown when the values differ.</exception>
	public static void Equal<T>(T expected, T actual, string context)
	{
		if (!EqualityComparer<T>.Default.Equals(expected, actual))
		{
			throw new ConformanceAssertionException($"{context}: expected {Describe(expected)} but found {Describe(actual)}.");
		}
	}

	/// <summary>
	/// Asserts that two byte sequences are equal.
	/// </summary>
	/// <param name="expected">The expected bytes.</param>
	/// <param name="actual">The observed bytes.</param>
	/// <param name="context">A description of what produced <paramref name="actual"/>.</param>
	/// <exception cref="ConformanceAssertionException">Thrown when the sequences differ.</exception>
	public static void EqualBytes(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual, string context)
	{
		if (!expected.SequenceEqual(actual))
		{
			throw new ConformanceAssertionException($"{context}: expected {expected.Length} bytes [{Convert.ToHexString(expected)}] but found {actual.Length} bytes [{Convert.ToHexString(actual)}].");
		}
	}

	/// <summary>
	/// Asserts that the decoder's next token is of an expected type.
	/// </summary>
	/// <typeparam name="TDecoder">The decoder type.</typeparam>
	/// <param name="expected">The expected token type.</param>
	/// <param name="decoder">The decoder to inspect. It is not advanced.</param>
	/// <param name="context">A description of the position being inspected.</param>
	/// <exception cref="ConformanceAssertionException">Thrown when the decoder reports a different token type.</exception>
	public static void NextToken<TDecoder>(TokenType expected, ref TDecoder decoder, string context)
		where TDecoder : IDecoder, allows ref struct
	{
		TokenType actual = decoder.NextTokenType;
		if (actual != expected)
		{
			throw new ConformanceAssertionException($"{context}: expected the next token to be {expected} but found {actual}.");
		}
	}

	/// <summary>
	/// Asserts that an operation throws a particular exception type.
	/// </summary>
	/// <typeparam name="TException">The expected exception type. Derived types satisfy the assertion.</typeparam>
	/// <param name="action">The operation expected to throw.</param>
	/// <param name="context">A description of the operation.</param>
	/// <returns>The exception that was thrown, for further inspection.</returns>
	/// <exception cref="ConformanceAssertionException">Thrown when nothing, or something else, was thrown.</exception>
	public static TException Throws<TException>(Action action, string context)
		where TException : Exception
	{
		Requires.NotNull(action);
		try
		{
			action();
		}
		catch (TException ex)
		{
			return ex;
		}
		catch (Exception ex)
		{
			throw new ConformanceAssertionException($"{context}: expected {typeof(TException).Name} but {ex.GetType().FullName} was thrown: {ex.Message}", ex);
		}

		throw new ConformanceAssertionException($"{context}: expected {typeof(TException).Name} but nothing was thrown.");
	}

	/// <summary>
	/// Asserts that an operation either completes or fails in a way the format is allowed to fail.
	/// </summary>
	/// <param name="action">The operation to run.</param>
	/// <param name="context">A description of the operation.</param>
	/// <remarks>
	/// A decoder handed malformed input is required to fail cleanly. "Cleanly" means
	/// <see cref="DecoderException"/> or <see cref="ShapeShiftSerializationException"/>; an
	/// <see cref="IndexOutOfRangeException"/>, <see cref="NullReferenceException"/>, or similar signals a
	/// missing bounds check, which is exactly the class of bug this kit exists to catch.
	/// </remarks>
	/// <exception cref="ConformanceAssertionException">Thrown when the operation fails with an unexpected exception type.</exception>
	public static void FailsCleanlyOrSucceeds(Action action, string context)
	{
		Requires.NotNull(action);
		try
		{
			action();
		}
		catch (Exception ex) when (IsCleanFailure(ex))
		{
		}
		catch (Exception ex)
		{
			throw new ConformanceAssertionException($"{context}: expected either success or a {nameof(DecoderException)}/{nameof(ShapeShiftSerializationException)}, but {ex.GetType().FullName} was thrown: {ex.Message}", ex);
		}
	}

	/// <summary>
	/// Asserts that an operation fails in a way the format is allowed to fail.
	/// </summary>
	/// <param name="action">The operation expected to fail.</param>
	/// <param name="context">A description of the operation.</param>
	/// <exception cref="ConformanceAssertionException">Thrown when the operation succeeds or fails with an unexpected exception type.</exception>
	public static void FailsCleanly(Action action, string context)
	{
		Requires.NotNull(action);
		try
		{
			action();
		}
		catch (Exception ex) when (IsCleanFailure(ex))
		{
			return;
		}
		catch (Exception ex)
		{
			throw new ConformanceAssertionException($"{context}: expected a {nameof(DecoderException)} or {nameof(ShapeShiftSerializationException)}, but {ex.GetType().FullName} was thrown: {ex.Message}", ex);
		}

		throw new ConformanceAssertionException($"{context}: expected a {nameof(DecoderException)} or {nameof(ShapeShiftSerializationException)}, but the operation succeeded.");
	}

	/// <summary>
	/// Gets a value indicating whether an exception represents an orderly rejection of bad input.
	/// </summary>
	/// <param name="exception">The exception to classify.</param>
	/// <returns><see langword="true" /> when the exception is one a decoder is permitted to throw for bad input.</returns>
	internal static bool IsCleanFailure(Exception exception)
		=> exception is DecoderException or ShapeShiftSerializationException
			|| (exception.InnerException is not null && IsCleanFailure(exception.InnerException));

	private static string Describe(object? value) => value switch
	{
		null => "<null>",
		string s => $"\"{s}\"",
		byte[] bytes => $"0x{Convert.ToHexString(bytes)}",
		IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
		IEnumerable enumerable => $"[{string.Join(", ", enumerable.Cast<object?>().Select(Describe))}]",
		_ => value.ToString() ?? "<null>",
	};
}
