// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
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
		[StringSyntax("c#-test")] string source,
		params string[] expectedCompilerErrorIds)
		=> await GetDiagnosticsAsync(analyzer, source, "Test.cs", expectedCompilerErrorIds);

	/// <summary>
	/// Runs an analyzer over a generated source file and returns the diagnostics it reported.
	/// </summary>
	/// <param name="analyzer">The analyzer to run.</param>
	/// <param name="source">The generated C# source to analyze.</param>
	/// <param name="expectedCompilerErrorIds">Compiler error IDs the source is expected to produce, if any.</param>
	/// <returns>The diagnostics reported by <paramref name="analyzer"/>.</returns>
	internal static async Task<ImmutableArray<Diagnostic>> GetGeneratedCodeDiagnosticsAsync(
		DiagnosticAnalyzer analyzer,
		[StringSyntax("c#-test")] string source,
		params string[] expectedCompilerErrorIds)
		=> await GetDiagnosticsAsync(analyzer, source, "Test.g.cs", expectedCompilerErrorIds);

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
		[StringSyntax("c#-test")] string source,
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

	/// <summary>
	/// Applies a code fix provider's document-scoped Fix All operation.
	/// </summary>
	/// <param name="analyzer">The analyzer that produces the diagnostics.</param>
	/// <param name="codeFix">The code fix provider under test.</param>
	/// <param name="source">The C# source to analyze and fix.</param>
	/// <param name="expectedCompilerErrorIds">Compiler error IDs the source is expected to produce, if any.</param>
	/// <returns>The source text after Fix All, or <see langword="null" /> when no fix was offered.</returns>
	internal static async Task<string?> ApplyFixAllAsync(
		DiagnosticAnalyzer analyzer,
		CodeFixProvider codeFix,
		[StringSyntax("c#-test")] string source,
		params string[] expectedCompilerErrorIds)
	{
		(AdhocWorkspace workspace, Document document) = CreateDocument(source);
		using (workspace)
		{
			Compilation compilation = await GetCompilationAsync(document);
			AssertNoUnexpectedCompilerErrors(compilation, expectedCompilerErrorIds);

			AnalyzerDiagnosticProvider diagnosticProvider = new(analyzer);
			Diagnostic[] diagnostics = [.. await diagnosticProvider.GetDocumentDiagnosticsAsync(document, CancellationToken.None)];
			if (diagnostics.FirstOrDefault(d => codeFix.FixableDiagnosticIds.Contains(d.Id)) is not { } diagnostic)
			{
				return null;
			}

			List<CodeAction> actions = [];
			CodeFixContext codeFixContext = new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None);
			await codeFix.RegisterCodeFixesAsync(codeFixContext);
			if (actions is not [{ EquivalenceKey: { } equivalenceKey }, ..])
			{
				return null;
			}

			FixAllProvider fixAllProvider = codeFix.GetFixAllProvider() ?? throw new InvalidOperationException("The provider does not support Fix All.");
			FixAllContext fixAllContext = new(
				document,
				codeFix,
				FixAllScope.Document,
				equivalenceKey,
				codeFix.FixableDiagnosticIds,
				diagnosticProvider,
				CancellationToken.None);
			CodeAction fixAllAction = await fixAllProvider.GetFixAsync(fixAllContext)
				?? throw new InvalidOperationException("The Fix All provider returned no action.");
			ImmutableArray<CodeActionOperation> operations = await fixAllAction.GetOperationsAsync(CancellationToken.None);
			Solution changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
			Document changedDocument = changed.GetDocument(document.Id)!;
			Document formatted = await Formatter.FormatAsync(changedDocument);
			return (await formatted.GetTextAsync()).ToString();
		}
	}

	private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
		DiagnosticAnalyzer analyzer,
		[StringSyntax("c#-test")] string source,
		string fileName,
		string[] expectedCompilerErrorIds)
	{
		(source, TextSpan? expectedSpan) = RemoveMarkup(source);
		(_, Document document) = CreateDocument(source, fileName);
		Compilation compilation = await GetCompilationAsync(document);
		AssertNoUnexpectedCompilerErrors(compilation, expectedCompilerErrorIds);

		CompilationWithAnalyzers withAnalyzers = compilation.WithAnalyzers([analyzer]);

		ImmutableArray<Diagnostic> diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync(CancellationToken.None);
		diagnostics = [.. diagnostics.OrderBy(d => d.Location.SourceSpan.Start).ThenBy(d => d.Id, StringComparer.Ordinal)];
		if (expectedSpan is { } span &&
			(diagnostics.Length != 1 || diagnostics[0].Location.SourceSpan != span))
		{
			throw new InvalidOperationException(
				$"Expected one diagnostic at {span}, but found: {string.Join(", ", diagnostics.Select(d => $"{d.Id}@{d.Location.SourceSpan}"))}");
		}

		return diagnostics;
	}

	private static (AdhocWorkspace Workspace, Document Document) CreateDocument(
		[StringSyntax("c#-test")] string source,
		string fileName = "Test.cs")
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
		return (workspace, workspace.AddDocument(project.Id, fileName, SourceText.From(source)));
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
		string runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory();
		DirectoryInfo? dotnetRoot = Directory.GetParent(runtimeDirectory)?.Parent?.Parent?.Parent;
		string referenceDirectory = Path.Combine(
			dotnetRoot?.FullName ?? throw new InvalidOperationException("Could not locate the dotnet installation."),
			"packs",
			"Microsoft.NETCore.App.Ref",
			Path.GetFileName(Path.TrimEndingDirectorySeparator(runtimeDirectory)),
			"ref",
			$"net{Environment.Version.Major}.0");
		if (!Directory.Exists(referenceDirectory))
		{
			throw new DirectoryNotFoundException($"Could not locate reference assemblies at '{referenceDirectory}'.");
		}

		foreach (string path in Directory.EnumerateFiles(referenceDirectory, "*.dll"))
		{
			AddReference(path);
		}

		AddReference(typeof(ShapeShiftSerializer<,>).Assembly.Location);
		AddReference(typeof(ShapeShift.Json.JsonSerializer).Assembly.Location);
		AddReference(typeof(PolyType.IShapeable<>).Assembly.Location);
		return builder.ToImmutable();

		void AddReference(string path)
		{
			if (seen.Add(Path.GetFileNameWithoutExtension(path)))
			{
				builder.Add(MetadataReference.CreateFromFile(path));
			}
		}
	}

	private static (string Source, TextSpan? ExpectedSpan) RemoveMarkup([StringSyntax("c#-test")] string source)
	{
		int start = source.IndexOf("[|", StringComparison.Ordinal);
		if (start < 0)
		{
			return (source, null);
		}

		int end = source.IndexOf("|]", start + 2, StringComparison.Ordinal);
		if (end < 0 || source.IndexOf("[|", start + 2, StringComparison.Ordinal) >= 0)
		{
			throw new ArgumentException("Source must contain exactly one complete [|...|] diagnostic span.", nameof(source));
		}

		string unmarked = source.Remove(end, 2).Remove(start, 2);
		return (unmarked, new TextSpan(start, end - start - 2));
	}

	private sealed class AnalyzerDiagnosticProvider(DiagnosticAnalyzer analyzer) : FixAllContext.DiagnosticProvider
	{
		/// <inheritdoc/>
		public override async Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(
			Document document,
			CancellationToken cancellationToken)
		{
			SyntaxTree tree = await document.GetSyntaxTreeAsync(cancellationToken)
				?? throw new InvalidOperationException("The document has no syntax tree.");
			return (await this.GetDiagnosticsAsync(document.Project, cancellationToken))
				.Where(diagnostic => diagnostic.Location.SourceTree == tree);
		}

		/// <inheritdoc/>
		public override async Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(
			Project project,
			CancellationToken cancellationToken)
			=> (await this.GetDiagnosticsAsync(project, cancellationToken)).Where(diagnostic => diagnostic.Location == Location.None);

		/// <inheritdoc/>
		public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(
			Project project,
			CancellationToken cancellationToken)
			=> this.GetDiagnosticsAsync(project, cancellationToken);

		private async Task<IEnumerable<Diagnostic>> GetDiagnosticsAsync(Project project, CancellationToken cancellationToken)
		{
			Compilation compilation = await project.GetCompilationAsync(cancellationToken)
				?? throw new InvalidOperationException("The project has no compilation.");
			return await compilation.WithAnalyzers([analyzer]).GetAnalyzerDiagnosticsAsync(cancellationToken);
		}
	}
}
