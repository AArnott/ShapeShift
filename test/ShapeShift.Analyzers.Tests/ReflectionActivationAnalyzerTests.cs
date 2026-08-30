// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace ShapeShift.Analyzers.Tests;

/// <summary>
/// Verifies SHIFT007 reported by <see cref="ReflectionActivationAnalyzer"/>.
/// </summary>
public class ReflectionActivationAnalyzerTests
{
	[Test]
	public async Task WithReflectionConverterTypes_ReportsShift007()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
			public class Caller
			{
				#pragma warning disable IL2026, IL3050
				public object Run(JsonSerializer serializer, ConverterTypeCollection types)
					=> serializer.WithReflectionConverterTypes(types);
				#pragma warning restore IL2026, IL3050
			}
			""");

		await TestSources.AssertIdsAsync(diagnostics, "SHIFT007");
		await Assert.That(diagnostics[0].Severity).IsEqualTo(DiagnosticSeverity.Info);
		await Assert.That(diagnostics[0].GetMessage()).Contains("WithReflectionConverterTypes");
		await Assert.That(diagnostics[0].Descriptor.HelpLinkUri).IsEqualTo("https://aarnott.github.io/ShapeShift/docs/analyzers/SHIFT007.html");
	}

	[Test]
	public async Task ReflectionTypeShapeProviderDefault_ReportsShift007()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
			using PolyType.ReflectionProvider;

			public class Caller
			{
				#pragma warning disable IL2026, IL3050
				public ITypeShapeProvider Run() => ReflectionTypeShapeProvider.Default;
				#pragma warning restore IL2026, IL3050
			}
			""");

		await TestSources.AssertIdsAsync(diagnostics, "SHIFT007");
		await Assert.That(diagnostics[0].GetMessage()).Contains("ReflectionTypeShapeProvider");
	}

	[Test]
	public async Task SourceGeneratedUsage_ReportsNothing()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
			public partial class Person : IShapeable<Person>
			{
				static ITypeShape<Person> IShapeable<Person>.GetTypeShape() => throw new NotImplementedException();
			}

			public class Caller
			{
				public string Run(JsonSerializer serializer, Person person) => serializer.Serialize(person);
			}
			""");

		await TestSources.AssertIdsAsync(diagnostics);
	}

	[Test]
	public async Task UnrelatedMethodWithSameName_ReportsNothing()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
			public class NotASerializer
			{
				public object WithReflectionConverterTypes(int value) => value;
			}

			public class Caller
			{
				public object Run(NotASerializer other) => other.WithReflectionConverterTypes(1);
			}
			""");

		await TestSources.AssertIdsAsync(diagnostics);
	}

	private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string body)
		=> AnalyzerHarness.GetDiagnosticsAsync(new ReflectionActivationAnalyzer(), TestSources.Source(body));
}
