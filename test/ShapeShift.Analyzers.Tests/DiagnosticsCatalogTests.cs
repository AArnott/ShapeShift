// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ShapeShift.Analyzers.Tests;

/// <summary>
/// Verifies the stability and completeness of the ShapeShift diagnostic catalog.
/// </summary>
public class DiagnosticsCatalogTests
{
	[Test]
	public async Task Catalog_HasStableIdsAndHelpLinks()
	{
		foreach (DiagnosticDescriptor descriptor in Diagnostics.GetAll())
		{
			await Assert.That(descriptor.Id).StartsWith("SHIFT");
			await Assert.That(descriptor.HelpLinkUri).IsEqualTo($"{Diagnostics.HelpLinkBase}{descriptor.Id}.html");
			await Assert.That(descriptor.Title.ToString()).IsNotEmpty();
			await Assert.That(descriptor.Description.ToString()).IsNotEmpty();
			await Assert.That(descriptor.Category).IsNotEmpty();
		}
	}

	[Test]
	public async Task Catalog_IdsAreUniqueAndContiguous()
	{
		string[] ids = [.. Diagnostics.GetAll().Select(d => d.Id).OrderBy(id => id, StringComparer.Ordinal)];

		await Assert.That(ids.Distinct().Count()).IsEqualTo(ids.Length);
		await Assert.That(string.Join(",", ids)).IsEqualTo("SHIFT001,SHIFT002,SHIFT003,SHIFT004,SHIFT005,SHIFT006,SHIFT007,SHIFT008");
	}

	[Test]
	public async Task EveryDescriptorIsSupportedByAnAnalyzer()
	{
		HashSet<string> supported = new(StringComparer.Ordinal);
		foreach (DiagnosticAnalyzer analyzer in GetAnalyzers())
		{
			foreach (DiagnosticDescriptor descriptor in analyzer.SupportedDiagnostics)
			{
				supported.Add(descriptor.Id);
			}
		}

		foreach (DiagnosticDescriptor descriptor in Diagnostics.GetAll())
		{
			await Assert.That(supported.Contains(descriptor.Id)).IsTrue();
		}
	}

	[Test]
	public async Task EveryAnalyzerIsConcurrencySafeAndDeclaresGeneratedCodePolicy()
	{
		foreach (DiagnosticAnalyzer analyzer in GetAnalyzers())
		{
			RecordingAnalysisContext context = new();
			analyzer.Initialize(context);

			await Assert.That(context.ConcurrentExecutionEnabled).IsTrue();
			await Assert.That(context.GeneratedCodeFlags).IsEqualTo(GeneratedCodeAnalysisFlags.None);
		}
	}

	[Test]
	public async Task EveryAnalyzerRejectsNullContext()
	{
		foreach (DiagnosticAnalyzer analyzer in GetAnalyzers())
		{
			void Act() => analyzer.Initialize(null!);
			await Assert.That(Act).Throws<ArgumentNullException>();
		}
	}

	[Test]
	public async Task ReleaseTrackingFileListsEveryDescriptor()
	{
		string releases = ReadUnshippedReleases();
		foreach (DiagnosticDescriptor descriptor in Diagnostics.GetAll())
		{
			await Assert.That(releases).Contains($"{descriptor.Id} | {descriptor.Category} | {descriptor.DefaultSeverity} |");
		}
	}

	private static IEnumerable<DiagnosticAnalyzer> GetAnalyzers()
	{
		yield return new ConverterAttributeAnalyzer();
		yield return new TypeShapeRequirementAnalyzer();
		yield return new WireNameAnalyzer();
		yield return new ReflectionActivationAnalyzer();
		yield return new ContractSupportAnalyzer();
	}

	private static string ReadUnshippedReleases()
	{
		string? directory = Path.GetDirectoryName(typeof(DiagnosticsCatalogTests).Assembly.Location);
		while (directory is not null)
		{
			string candidate = Path.Combine(directory, "src", "ShapeShift.Analyzers", "AnalyzerReleases.Unshipped.md");
			if (File.Exists(candidate))
			{
				return File.ReadAllText(candidate);
			}

			directory = Path.GetDirectoryName(directory);
		}

		throw new FileNotFoundException("Could not locate AnalyzerReleases.Unshipped.md.");
	}

	private sealed class RecordingAnalysisContext : AnalysisContext
	{
		internal bool ConcurrentExecutionEnabled { get; private set; }

		internal GeneratedCodeAnalysisFlags? GeneratedCodeFlags { get; private set; }

		public override void EnableConcurrentExecution() => this.ConcurrentExecutionEnabled = true;

		public override void ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags analysisMode) => this.GeneratedCodeFlags = analysisMode;

		public override void RegisterCompilationStartAction(Action<CompilationStartAnalysisContext> action)
		{
		}

		public override void RegisterCompilationAction(Action<CompilationAnalysisContext> action)
		{
		}

		public override void RegisterSemanticModelAction(Action<SemanticModelAnalysisContext> action)
		{
		}

		public override void RegisterSymbolAction(Action<SymbolAnalysisContext> action, ImmutableArray<SymbolKind> symbolKinds)
		{
		}

		public override void RegisterSyntaxNodeAction<TLanguageKindEnum>(Action<SyntaxNodeAnalysisContext> action, ImmutableArray<TLanguageKindEnum> syntaxKinds)
		{
		}

		public override void RegisterSyntaxTreeAction(Action<SyntaxTreeAnalysisContext> action)
		{
		}

		public override void RegisterCodeBlockStartAction<TLanguageKindEnum>(Action<CodeBlockStartAnalysisContext<TLanguageKindEnum>> action)
		{
		}

		public override void RegisterCodeBlockAction(Action<CodeBlockAnalysisContext> action)
		{
		}

		public override void RegisterOperationAction(Action<OperationAnalysisContext> action, ImmutableArray<OperationKind> operationKinds)
		{
		}

		public override void RegisterOperationBlockAction(Action<OperationBlockAnalysisContext> action)
		{
		}

		public override void RegisterOperationBlockStartAction(Action<OperationBlockStartAnalysisContext> action)
		{
		}
	}
}
