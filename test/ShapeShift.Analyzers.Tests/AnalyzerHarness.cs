// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

namespace ShapeShift.Analyzers.Tests;

/// <summary>
/// A minimal Roslyn test harness that drives the ShapeShift analyzers and code fixes through their
/// public Roslyn entry points, so the tests never need access to analyzer internals.
/// </summary>
/// <remarks>
/// The harness compiles the supplied source against the assemblies loaded into this test process,
/// which include ShapeShift and PolyType. PolyType's source generator is deliberately not run:
/// tests that need a shape-bearing type implement <c>PolyType.IShapeable&lt;T&gt;</c> by hand,
/// which is exactly the contract the analyzers examine.
/// </remarks>
internal static class AnalyzerHarness
{
	private static readonly Lazy<ImmutableArray<MetadataReference>> ReferenceAssemblies = new(CreateReferences);

	/// <summary>
	/// Runs an analyzer over a source file and returns the diagnostics it reported.
	/// </summary>
	/// <param name="analyzer">The analyzer to run.</param>
	/// <param name="source">The C# source to analyze.</param>
	/// <param name="expectedCompilerErrorIds">Compiler error IDs the source is expected to produce, if any.</param>
	/// <returns>The diagnostics reported by <paramref name="analyzer"/>, ordered by source position.</returns>
	internal static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
		DiagnosticAnalyzer analyzer,
		string source,
		params string[] expectedCompilerErrorIds)
	{
		(_, Document document) = CreateDocument(source);
		Compilation compilation = await GetCompilationAsync(document);
		AssertNoUnexpectedCompilerErrors(compilation, expectedCompilerErrorIds);

		CompilationWithAnalyzers withAnalyzers = compilation.WithAnalyzers([analyzer]);

		ImmutableArray<Diagnostic> diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync(CancellationToken.None);
		return [.. diagnostics.OrderBy(d => d.Location.SourceSpan.Start).ThenBy(d => d.Id, StringComparer.Ordinal)];
	}

	/// <summary>
	/// Runs an analyzer and applies the first code fix offered for the first diagnostic.
	/// </summary>
	/// <param name="analyzer">The analyzer that produces the diagnostic.</param>
	/// <param name="codeFix">The code fix provider under test.</param>
	/// <param name="source">The C# source to analyze and fix.</param>
	/// <param name="expectedCompilerErrorIds">Compiler error IDs the source is expected to produce, if any.</param>
	/// <returns>The source text after the fix, or <see langword="null" /> when no fix was offered.</returns>
	internal static async Task<string?> ApplyFixAsync(
		DiagnosticAnalyzer analyzer,
		CodeFixProvider codeFix,
		string source,
		params string[] expectedCompilerErrorIds)
	{
		(AdhocWorkspace workspace, Document document) = CreateDocument(source);
		using (workspace)
		{
			Compilation compilation = await GetCompilationAsync(document);
			AssertNoUnexpectedCompilerErrors(compilation, expectedCompilerErrorIds);

			CompilationWithAnalyzers withAnalyzers = compilation.WithAnalyzers([analyzer]);
			ImmutableArray<Diagnostic> diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync(CancellationToken.None);
			Diagnostic? diagnostic = diagnostics.FirstOrDefault(d => codeFix.FixableDiagnosticIds.Contains(d.Id));
			if (diagnostic is null)
			{
				return null;
			}

			List<CodeAction> actions = [];
			CodeFixContext context = new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None);
			await codeFix.RegisterCodeFixesAsync(context);
			if (actions.Count == 0)
			{
				return null;
			}

			ImmutableArray<CodeActionOperation> operations = await actions[0].GetOperationsAsync(CancellationToken.None);
			Solution changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
			Document changedDocument = changed.GetDocument(document.Id)!;
			Document formatted = await Formatter.FormatAsync(changedDocument);
			SourceText text = await formatted.GetTextAsync();
			return text.ToString();
		}
	}

	private static (AdhocWorkspace Workspace, Document Document) CreateDocument(string source)
	{
		AdhocWorkspace workspace = new();
		ProjectId projectId = ProjectId.CreateNewId();
		ProjectInfo projectInfo = ProjectInfo
			.Create(projectId, VersionStamp.Default, "TestProject", "TestProject", LanguageNames.CSharp)
			.WithMetadataReferences(ReferenceAssemblies.Value)
			.WithCompilationOptions(new CSharpCompilationOptions(
				OutputKind.DynamicallyLinkedLibrary,
				nullableContextOptions: NullableContextOptions.Enable))
			.WithParseOptions(new CSharpParseOptions(LanguageVersion.Latest));

		Project project = workspace.AddProject(projectInfo);
		return (workspace, workspace.AddDocument(project.Id, "Test.cs", SourceText.From(source)));
	}

	private static async Task<Compilation> GetCompilationAsync(Document document)
		=> await document.Project.GetCompilationAsync() ?? throw new InvalidOperationException("No compilation.");

	private static void AssertNoUnexpectedCompilerErrors(Compilation compilation, string[] expectedCompilerErrorIds)
	{
		string[] unexpected = [.. compilation.GetDiagnostics()
			.Where(d => d.Severity == DiagnosticSeverity.Error && !expectedCompilerErrorIds.Contains(d.Id))
			.Select(d => d.ToString())];

		if (unexpected.Length > 0)
		{
			throw new InvalidOperationException($"The test source failed to compile:{Environment.NewLine}{string.Join(Environment.NewLine, unexpected)}");
		}
	}

	private static ImmutableArray<MetadataReference> CreateReferences()
	{
		ImmutableArray<MetadataReference>.Builder builder = ImmutableArray.CreateBuilder<MetadataReference>();
		HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
		if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trusted)
		{
			foreach (string path in trusted.Split(Path.PathSeparator))
			{
				if (path.Length > 0 && seen.Add(Path.GetFileNameWithoutExtension(path)) && File.Exists(path))
				{
					builder.Add(MetadataReference.CreateFromFile(path));
				}
			}
		}

		return builder.ToImmutable();
	}
}
