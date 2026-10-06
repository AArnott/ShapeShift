// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Analyzers.Tests;

/// <summary>
/// Verifies that projects with their own version.json get the assembly version it specifies
/// rather than the repo root version.json's version (e.g. when a root GitVersionBaseDirectory overrides it).
/// </summary>
/// <remarks>
/// The expected versions are computed at build time by the AddNestedVersionJsonAssemblyVersionExpectations target in test/Directory.Build.targets.
/// </remarks>
public class AssemblyVersionTests
{
	[Test]
	public async Task ShapeShiftAnalyzers() => await AssertNestedVersionJsonAssemblyVersionAsync(typeof(global::ShapeShift.Analyzers.Diagnostics).Assembly);

	[Test]
	public async Task ShapeShiftAnalyzersCodeFixes() => await AssertNestedVersionJsonAssemblyVersionAsync(typeof(global::ShapeShift.Analyzers.AddGenerateShapeCodeFixProvider).Assembly);

	private static async Task AssertNestedVersionJsonAssemblyVersionAsync(System.Reflection.Assembly assembly)
	{
		System.Reflection.AssemblyName assemblyName = assembly.GetName();
		string? expected = GetExpectedAssemblyVersion(assemblyName.Name!);
		await Assert.That(expected).IsNotNull();
		await Assert.That(assemblyName.Version).IsEqualTo(Version.Parse(expected!));
	}

	private static string? GetExpectedAssemblyVersion(string assemblyName)
	{
		return typeof(AssemblyVersionTests).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), inherit: false)
			.Cast<System.Reflection.AssemblyMetadataAttribute>()
			.SingleOrDefault(a => a.Key == $"ExpectedAssemblyVersion:{assemblyName}")?.Value;
	}
}
