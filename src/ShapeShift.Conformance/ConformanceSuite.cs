// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ShapeShift.Conformance.Suites;

namespace ShapeShift.Conformance;

/// <summary>
/// Builds and runs the ShapeShift format conformance suite.
/// </summary>
/// <remarks>
/// <para>
/// The suite is a list of independent, named cases. A consumer either feeds
/// <see cref="CreateTestCases"/> into its test framework's data source so each case becomes a
/// first-class test, or calls <see cref="Run"/> to get a single report -- useful from a console
/// app or a smoke test.
/// </para>
/// <para>
/// The kit deliberately has no test framework dependency. A case signals failure by throwing,
/// which every framework already understands.
/// </para>
/// </remarks>
public static class ConformanceSuite
{
	/// <summary>
	/// Builds every conformance case for a format, including the ones its declared limitations skip.
	/// </summary>
	/// <typeparam name="TEncoder">The format's encoder type.</typeparam>
	/// <typeparam name="TDecoder">The format's decoder type.</typeparam>
	/// <param name="adapter">The adapter describing the format under test.</param>
	/// <param name="categories">The categories to include. Defaults to all of them.</param>
	/// <param name="additionalSuites">Extra suites to append after the built-in ones and the adapter's own cases.</param>
	/// <returns>The cases, in a stable order.</returns>
	public static IReadOnlyList<ConformanceTestCase> CreateTestCases<TEncoder, TDecoder>(
		FormatConformanceAdapter<TEncoder, TDecoder> adapter,
		ConformanceCategory categories = ConformanceCategory.All,
		IEnumerable<IConformanceSuite<TEncoder, TDecoder>>? additionalSuites = null)
		where TEncoder : IEncoder, allows ref struct
		where TDecoder : IDecoder, allows ref struct
	{
		Requires.NotNull(adapter);

		ConformanceTestCollector<TEncoder, TDecoder> collector = new(adapter);
		foreach (IConformanceSuite<TEncoder, TDecoder> suite in BuiltInSuites<TEncoder, TDecoder>().Concat(additionalSuites ?? []))
		{
			if ((categories & suite.Category) != suite.Category)
			{
				continue;
			}

			collector.CurrentCategory = suite.Category;
			suite.AddTests(collector);
		}

		collector.CurrentCategory = ConformanceCategory.None;
		adapter.AddFormatSpecificTests(collector);

		return [.. collector.Cases];
	}

	/// <summary>
	/// Runs every conformance case for a format and reports the outcome of each.
	/// </summary>
	/// <typeparam name="TEncoder">The format's encoder type.</typeparam>
	/// <typeparam name="TDecoder">The format's decoder type.</typeparam>
	/// <param name="adapter">The adapter describing the format under test.</param>
	/// <param name="categories">The categories to include. Defaults to all of them.</param>
	/// <param name="additionalSuites">Extra suites to append after the built-in ones and the adapter's own cases.</param>
	/// <returns>The report. Call <see cref="ConformanceReport.ThrowIfNotConformant"/> to turn it into a pass/fail.</returns>
	public static ConformanceReport Run<TEncoder, TDecoder>(
		FormatConformanceAdapter<TEncoder, TDecoder> adapter,
		ConformanceCategory categories = ConformanceCategory.All,
		IEnumerable<IConformanceSuite<TEncoder, TDecoder>>? additionalSuites = null)
		where TEncoder : IEncoder, allows ref struct
		where TDecoder : IDecoder, allows ref struct
	{
		Requires.NotNull(adapter);
		IReadOnlyList<ConformanceTestCase> cases = CreateTestCases(adapter, categories, additionalSuites);
		return new ConformanceReport(adapter.FormatName, [.. cases.Select(c => c.Execute())]);
	}

	private static IEnumerable<IConformanceSuite<TEncoder, TDecoder>> BuiltInSuites<TEncoder, TDecoder>()
		where TEncoder : IEncoder, allows ref struct
		where TDecoder : IDecoder, allows ref struct
		=>
		[
			new TokenSuite<TEncoder, TDecoder>(),
			new NullSuite<TEncoder, TDecoder>(),
			new StateSuite<TEncoder, TDecoder>(),
			new SkipSuite<TEncoder, TDecoder>(),
			new PathSuite<TEncoder, TDecoder>(),
			new PrimitiveSuite<TEncoder, TDecoder>(),
			new BinarySuite<TEncoder, TDecoder>(),
			new DynamicSuite<TEncoder, TDecoder>(),
			new MalformedSuite<TEncoder, TDecoder>(),
			new LimitsSuite<TEncoder, TDecoder>(),
			new ConverterSuite<TEncoder, TDecoder>(),
			new ScannerSuite<TEncoder, TDecoder>(),
		];
}
