// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ShapeShift;
using ShapeShift.Conformance;

namespace Ubjson.Tests;

/// <summary>
/// Runs the shared <see cref="ConformanceSuite"/> against the UBJSON format-authoring sample, so that
/// the sample a third-party author copies from is held to the same contract every shipping format is.
/// </summary>
public class UbjsonConformanceTests
{
	/// <summary>
	/// Gets every conformance case that applies to UBJSON.
	/// </summary>
	/// <returns>The cases, each wrapped in a factory as TUnit data sources require.</returns>
	public static IEnumerable<Func<ConformanceTestCase>> Cases()
		=> ConformanceSuite.CreateTestCases(new UbjsonConformanceAdapter())
			.Where(c => !c.IsSkipped)
			.Select(c => (Func<ConformanceTestCase>)(() => c));

	/// <summary>
	/// Runs one conformance case.
	/// </summary>
	/// <param name="testCase">The case to run.</param>
	[Test]
	[MethodDataSource(nameof(Cases))]
	public void Conformance(ConformanceTestCase testCase)
	{
		ArgumentNullException.ThrowIfNull(testCase);
		testCase.Run();
	}

	/// <summary>
	/// Asserts that every applicable case passes, and reports what UBJSON opted out of.
	/// </summary>
	/// <returns>A task tracking the assertion.</returns>
	[Test]
	public async Task WholeSuiteIsConformant()
	{
		ConformanceReport report = ConformanceSuite.Run(new UbjsonConformanceAdapter());
		foreach (ConformanceResult result in report.Results.Where(r => r.Outcome != ConformanceOutcome.Passed))
		{
			Console.WriteLine(result.ToString());
		}

		report.ThrowIfNotConformant();
		await Assert.That(report.PassedCount).IsGreaterThan(100);
	}
}
