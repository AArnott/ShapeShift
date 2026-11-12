// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace ShapeShift.Analyzers.Tests;

/// <summary>
/// Verifies that analyzer compilations use the restored Microsoft.NETCore.App reference pack.
/// </summary>
public class ReferenceAssemblyTests
{
	[Test]
	public async Task CompilesAgainstReferenceAssemblies()
	{
		string directory = AnalyzerHarness.ReferenceAssemblyDirectory;
		string runtimeAssembly = Path.Combine(directory, "System.Runtime.dll");
		string implementationAssembly = Path.Combine(directory, "System.Private.CoreLib.dll");

		await Assert.That(directory.Contains("Microsoft.NETCore.App.Ref", StringComparison.OrdinalIgnoreCase)).IsTrue();
		await Assert.That(File.Exists(runtimeAssembly)).IsTrue();
		await Assert.That(File.Exists(implementationAssembly)).IsFalse();

		string? referencedRuntime = AnalyzerHarness.MetadataReferences
			.OfType<PortableExecutableReference>()
			.Select(reference => reference.FilePath)
			.SingleOrDefault(path => string.Equals(Path.GetFileName(path), "System.Runtime.dll", StringComparison.OrdinalIgnoreCase));
		await Assert.That(referencedRuntime).IsEqualTo(runtimeAssembly);
	}
}
