// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace ShapeShift.Analyzers.Tests;

/// <summary>
/// Common source fragments and assertions shared by the analyzer tests.
/// </summary>
/// <remarks>
/// Test sources use the concrete <c>ShapeShift.Json</c> encoder and decoder so that converters can be
/// declared closed, which is the shape the analyzers are able to reason about.
/// </remarks>
internal static class TestSources
{
	/// <summary>
	/// The using directives that every analyzer test source starts with.
	/// </summary>
	internal const string Usings = /* lang=c#-test */ """
		using System;
		using System.Collections.Generic;
		using PolyType;
		using PolyType.Abstractions;
		using ShapeShift;
		using ShapeShift.Json;

		""";

	/// <summary>
	/// Asserts that a set of diagnostics has exactly the expected IDs, in source order.
	/// </summary>
	/// <param name="diagnostics">The diagnostics reported by the analyzer.</param>
	/// <param name="expectedIds">The expected IDs.</param>
	/// <returns>A task that completes when the assertion has run.</returns>
	internal static async Task AssertIdsAsync(ImmutableArray<Diagnostic> diagnostics, params string[] expectedIds)
	{
		string[] actual = [.. diagnostics.Select(d => d.Id)];
		await Assert.That(string.Join(", ", actual)).IsEqualTo(string.Join(", ", expectedIds));
	}

	/// <summary>
	/// Prefixes a source fragment with the standard using directives.
	/// </summary>
	/// <param name="body">The declarations under test.</param>
	/// <returns>A complete compilation unit.</returns>
	internal static string Source([StringSyntax("c#-test")] string body) => Usings + body;

	/// <summary>
	/// Normalizes source text line endings for cross-platform exact comparisons.
	/// </summary>
	/// <param name="source">The source text.</param>
	/// <returns>The source text with LF line endings.</returns>
	internal static string NormalizeLineEndings([StringSyntax("c#-test")] string source)
		=> source.Replace("\r\n", "\n", StringComparison.Ordinal);
}
