// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace ShapeShift.Analyzers.Tests;

/// <summary>
/// Verifies SHIFT005 and SHIFT006 reported by <see cref="WireNameAnalyzer"/>.
/// </summary>
public class WireNameAnalyzerTests
{
	[Test]
	public async Task DistinctNames_ReportNothing()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
			[GenerateShape]
			public partial class Person
			{
				public string? First { get; set; }

				public string? Last { get; set; }
			}
			""");

		await TestSources.AssertIdsAsync(diagnostics);
	}

	[Test]
	public async Task ExplicitNameCollidesWithDeclaredName_ReportsShift005()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
			[GenerateShape]
			public partial class Person
			{
				public string? Name { get; set; }

				[PropertyShape(Name = "Name")]
				public string? Alias { get; set; }
			}
			""");

		await TestSources.AssertIdsAsync(diagnostics, "SHIFT005");
		await Assert.That(diagnostics[0].GetMessage()).Contains("'Name'");
		await Assert.That(diagnostics[0].Descriptor.HelpLinkUri).IsEqualTo("https://aarnott.github.io/ShapeShift/analyzers/SHIFT005.html");
	}

	[Test]
	public async Task TwoExplicitNamesCollide_ReportsShift005()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
			[GenerateShape]
			public partial class Person
			{
				[PropertyShape(Name = "id")]
				public int First { get; set; }

				[PropertyShape(Name = "id")]
				public int Second { get; set; }
			}
			""");

		await TestSources.AssertIdsAsync(diagnostics, "SHIFT005");
	}

	[Test]
	public async Task FieldAndPropertyCollide_ReportsShift005()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
			[GenerateShape]
			public partial class Person
			{
				[PropertyShape(Name = "value")]
				public int Field;

				[PropertyShape(Name = "value")]
				public int Property { get; set; }
			}
			""");

		await TestSources.AssertIdsAsync(diagnostics, "SHIFT005");
	}

	[Test]
	public async Task IgnoredMember_DoesNotCollide()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
			[GenerateShape]
			public partial class Person
			{
				public string? Name { get; set; }

				[PropertyShape(Name = "Name", Ignore = true)]
				public string? Alias { get; set; }
			}
			""");

		await TestSources.AssertIdsAsync(diagnostics);
	}

	[Test]
	public async Task ExtensionDataMember_DoesNotCollide()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
			[GenerateShape]
			public partial class Person
			{
				[PropertyShape(Name = "Extra")]
				public string? Named { get; set; }

				[ShapeShiftExtensionData]
				[PropertyShape(Name = "Extra")]
				public Dictionary<string, ShapeShiftValue> Extra { get; } = new();
			}
			""");

		await TestSources.AssertIdsAsync(diagnostics);
	}

	[Test]
	public async Task CaseOnlyDifference_ReportsShift006()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
			[GenerateShape]
			public partial class Person
			{
				public int Id { get; set; }

				public int ID { get; set; }
			}
			""");

		await TestSources.AssertIdsAsync(diagnostics, "SHIFT006");
		await Assert.That(diagnostics[0].Severity).IsEqualTo(DiagnosticSeverity.Info);
		await Assert.That(diagnostics[0].GetMessage()).Contains("naming policy");
	}

	[Test]
	public async Task CaseOnlyDifferenceWithExplicitName_ReportsNothing()
	{
		// An explicit name is written verbatim, so no naming policy can create a collision.
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
			[GenerateShape]
			public partial class Person
			{
				public int Id { get; set; }

				[PropertyShape(Name = "ID")]
				public int Identifier { get; set; }
			}
			""");

		await TestSources.AssertIdsAsync(diagnostics);
	}

	[Test]
	public async Task NonPublicAndStaticMembers_AreIgnored()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
			[GenerateShape]
			public partial class Person
			{
				public int Id { get; set; }

				internal int ID { get; set; }

				public static int id { get; set; }
			}
			""");

		await TestSources.AssertIdsAsync(diagnostics);
	}

	[Test]
	public async Task WriteOnlyProperty_IsIgnored()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
			[GenerateShape]
			public partial class Person
			{
				public int Id { get; set; }

				public int ID { set { } }
			}
			""");

		await TestSources.AssertIdsAsync(diagnostics);
	}

	[Test]
	public async Task TypeWithoutGenerateShape_IsIgnored()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
			public partial class Person
			{
				public string? Name { get; set; }

				[PropertyShape(Name = "Name")]
				public string? Alias { get; set; }
			}
			""");

		await TestSources.AssertIdsAsync(diagnostics);
	}

	private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string body)
		=> AnalyzerHarness.GetDiagnosticsAsync(new WireNameAnalyzer(), TestSources.Source(body));
}
