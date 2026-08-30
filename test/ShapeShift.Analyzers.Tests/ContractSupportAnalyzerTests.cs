// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace ShapeShift.Analyzers.Tests;

/// <summary>
/// Verifies SHIFT008 reported by <see cref="ContractSupportAnalyzer"/>.
/// </summary>
public class ContractSupportAnalyzerTests
{
	[Test]
	public async Task ValidExtensionData_ReportsNothing()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
			public class Extensible
			{
				[ShapeShiftExtensionData]
				public Dictionary<string, ShapeShiftValue> Extra { get; } = new();
			}
			""");

		await TestSources.AssertIdsAsync(diagnostics);
	}

	[Test]
	public async Task MultipleExtensionDataMembers_ReportsShift008()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
			public class Extensible
			{
				[ShapeShiftExtensionData]
				public Dictionary<string, ShapeShiftValue> First { get; } = new();

				[ShapeShiftExtensionData]
				public Dictionary<string, ShapeShiftValue> Second { get; } = new();
			}
			""");

		await TestSources.AssertIdsAsync(diagnostics, "SHIFT008");
		await Assert.That(diagnostics[0].GetMessage()).Contains("more than one extension-data member");
		await Assert.That(diagnostics[0].Descriptor.HelpLinkUri).IsEqualTo("https://aarnott.github.io/ShapeShift/docs/analyzers/SHIFT008.html");
	}

	[Test]
	public async Task WrongExtensionDataType_ReportsShift008()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
			public class Extensible
			{
				[ShapeShiftExtensionData]
				public Dictionary<string, string> Extra { get; } = new();
			}
			""");

		await TestSources.AssertIdsAsync(diagnostics, "SHIFT008");
		await Assert.That(diagnostics[0].GetMessage()).Contains("Dictionary<string, ShapeShiftValue>");
	}

	[Test]
	public async Task ExtensionDataWithoutGetter_ReportsShift008()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
			public class Extensible
			{
				private Dictionary<string, ShapeShiftValue>? backing;

				[ShapeShiftExtensionData]
				public Dictionary<string, ShapeShiftValue> Extra { set { this.backing = value; } }
			}
			""");

		await TestSources.AssertIdsAsync(diagnostics, "SHIFT008");
		await Assert.That(diagnostics[0].GetMessage()).Contains("must have a getter");
	}

	[Test]
	public async Task ExtensionDataWithoutParameterlessConstructor_ReportsShift008()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
			public class Extensible
			{
				public Extensible(int required) { }

				[ShapeShiftExtensionData]
				public Dictionary<string, ShapeShiftValue> Extra { get; } = new();
			}
			""");

		await TestSources.AssertIdsAsync(diagnostics, "SHIFT008");
		await Assert.That(diagnostics[0].GetMessage()).Contains("parameterless deserialization constructor");
	}

	[Test]
	public async Task ExtensionDataOnStruct_ReportsNothing()
	{
		// Structs always have a parameterless constructor available to the deserializer.
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
			public struct Extensible
			{
				public Extensible(int required) { this.Extra = new(); }

				[ShapeShiftExtensionData]
				public Dictionary<string, ShapeShiftValue> Extra { get; set; }
			}
			""");

		await TestSources.AssertIdsAsync(diagnostics);
	}

	[Test]
	public async Task ExtensionDataField_IsSupported()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
			public class Extensible
			{
				[ShapeShiftExtensionData]
				public Dictionary<string, ShapeShiftValue> Extra = new();
			}
			""");

		await TestSources.AssertIdsAsync(diagnostics);
	}

	[Test]
	public async Task TypeWithoutExtensionData_ReportsNothing()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
			public class Person
			{
				public Person(int required) { }

				public string? Name { get; set; }
			}
			""");

		await TestSources.AssertIdsAsync(diagnostics);
	}

	private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string body)
		=> AnalyzerHarness.GetDiagnosticsAsync(new ContractSupportAnalyzer(), TestSources.Source(body));
}
