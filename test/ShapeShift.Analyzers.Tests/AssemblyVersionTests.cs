// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Analyzers.Tests;

/// <summary>
/// Verifies that projects with their own version.json get its revision-level assembly version,
/// rather than the root version.json's x.y.0.0 (e.g. when a root GitVersionBaseDirectory overrides it).
/// </summary>
public class AssemblyVersionTests
{
	[Test]
	public async Task ShapeShiftAnalyzers() => await Assert.That(typeof(global::ShapeShift.Analyzers.Diagnostics).Assembly.GetName().Version!.Revision).IsNotEqualTo(0);

	[Test]
	public async Task ShapeShiftAnalyzersCodeFixes() => await Assert.That(typeof(global::ShapeShift.Analyzers.AddGenerateShapeCodeFixProvider).Assembly.GetName().Version!.Revision).IsNotEqualTo(0);
}
