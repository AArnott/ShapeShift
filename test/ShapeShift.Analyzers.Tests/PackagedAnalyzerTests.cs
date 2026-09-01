// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition.Hosting;
using System.IO.Compression;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ShapeShift.Analyzers.Tests;

/// <summary>
/// Verifies analyzer and code-fix discovery from the package that consumers install.
/// </summary>
public class PackagedAnalyzerTests
{
	[Test]
	public async Task ShapeShiftPackage_ContainsLoadableAnalyzersAndCodeFixes()
	{
		string packagePath = FindPackage();
		string extractionPath = Path.Combine(Path.GetTempPath(), "ShapeShift.AnalyzerPackageTests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(extractionPath);

		try
		{
			ZipFile.ExtractToDirectory(packagePath, extractionPath);
			string analyzerPath = Path.Combine(extractionPath, "analyzers", "dotnet", "cs", "ShapeShift.Analyzers.dll");
			string codeFixPath = Path.Combine(extractionPath, "analyzers", "dotnet", "cs", "ShapeShift.Analyzers.CodeFixes.dll");
			await Assert.That(File.Exists(analyzerPath)).IsTrue();
			await Assert.That(File.Exists(codeFixPath)).IsTrue();

			PackageAnalyzerAssemblyLoader loader = new();
			loader.AddDependencyLocation(analyzerPath);
			loader.AddDependencyLocation(codeFixPath);
			List<string> loadFailures = [];
			AnalyzerFileReference reference = new(analyzerPath, loader);
			reference.AnalyzerLoadFailed += (_, args) => loadFailures.Add(args.Message);
			ImmutableArray<DiagnosticAnalyzer> analyzers = reference.GetAnalyzers(LanguageNames.CSharp);

			await Assert.That(loadFailures).IsEmpty();
			await Assert.That(analyzers.Length).IsEqualTo(5);

			DiagnosticAnalyzer wireNameAnalyzer = analyzers.Single(analyzer => analyzer.GetType().Name == nameof(WireNameAnalyzer));
			ImmutableArray<Diagnostic> diagnostics = await AnalyzerHarness.GetDiagnosticsAsync(
				wireNameAnalyzer,
				TestSources.Source("""
					[GenerateShape]
					public partial class Person
					{
						public int Id { get; set; }

						public int ID { get; set; }
					}
					"""));
			await TestSources.AssertIdsAsync(diagnostics, "SHIFT006");

			Assembly codeFixAssembly = loader.LoadFromPath(codeFixPath);
			using CompositionHost container = new ContainerConfiguration()
				.WithAssembly(codeFixAssembly)
				.CreateContainer();
			CodeFixProvider[] codeFixes = [.. container.GetExports<CodeFixProvider>()];
			await Assert.That(codeFixes.Select(fix => fix.GetType().Name).OrderBy(name => name).SequenceEqual(
				[
					nameof(AddGenerateShapeCodeFixProvider),
					nameof(MakeConverterConstructorPublicCodeFixProvider),
				])).IsTrue();
		}
		finally
		{
			Directory.Delete(extractionPath, recursive: true);
		}
	}

	private static string FindPackage()
	{
		string? directory = Path.GetDirectoryName(typeof(PackagedAnalyzerTests).Assembly.Location);
		while (directory is not null)
		{
			string packageDirectory = Path.Combine(directory, "obj", "test", "ShapeShift.Analyzers.Tests", "package-under-test");
			if (Directory.Exists(packageDirectory))
			{
				string[] packages = [.. Directory.EnumerateFiles(packageDirectory, "ShapeShift.*.nupkg")
					.Where(path => !path.EndsWith(".symbols.nupkg", StringComparison.OrdinalIgnoreCase))
					.OrderByDescending(File.GetLastWriteTimeUtc)];
				if (packages is [string package, ..])
				{
					return package;
				}
			}

			directory = Path.GetDirectoryName(directory);
		}

		throw new FileNotFoundException("Could not locate the ShapeShift package produced for analyzer tests.");
	}

	private sealed class PackageAnalyzerAssemblyLoader : IAnalyzerAssemblyLoader
	{
		/// <inheritdoc/>
		public void AddDependencyLocation(string fullPath)
		{
		}

		/// <inheritdoc/>
		public Assembly LoadFromPath(string fullPath) => System.Reflection.Assembly.LoadFrom(fullPath);
	}
}
