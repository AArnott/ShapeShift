// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using ShapeShift.Conformance;

namespace ShapeShift.Toml.Tests;

/// <summary>
/// Runs the shared <see cref="ConformanceSuite"/> against <see cref="TomlEncoder"/> and <see cref="TomlDecoder"/>.
/// </summary>
public class TomlConformanceTests
{
	/// <summary>
	/// Gets every conformance case that applies to TOML.
	/// </summary>
	/// <returns>The cases, each wrapped in a factory as TUnit data sources require.</returns>
	public static IEnumerable<Func<ConformanceTestCase>> Cases()
		=> ConformanceSuite.CreateTestCases(new TomlAdapter())
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
	/// Asserts that every applicable case passes, and reports what TOML opted out of.
	/// </summary>
	/// <returns>A task tracking the assertion.</returns>
	[Test]
	public async Task WholeSuiteIsConformant()
	{
		ConformanceReport report = ConformanceSuite.Run(new TomlAdapter());
		foreach (ConformanceResult result in report.Results.Where(r => r.Outcome != ConformanceOutcome.Passed))
		{
			Console.WriteLine(result.ToString());
		}

		report.ThrowIfNotConformant();
		await Assert.That(report.PassedCount).IsGreaterThan(40);
	}

	/// <summary>
	/// Describes TOML to the conformance kit.
	/// </summary>
	private sealed class TomlAdapter : FormatConformanceAdapter<TomlEncoder, TomlDecoder>
	{
		/// <inheritdoc/>
		public override string FormatName => "Toml";

		/// <inheritdoc/>
		public override FormatConformanceOptions Options { get; } = new()
		{
			// TOML has no binary family.
			SupportsBinary = false,

			// TOML requires a table at the root, so bare scalars and vectors need wrapping.
			SupportsRootScalars = false,
			SupportsRootVectors = false,

			// TOML arrays can hold mixed types when they contain tables, but not primitive types.
			SupportsHeterogeneousVectors = false,

			// Empty tables and arrays are fully supported in TOML.
			SupportsEmptyMaps = true,
			SupportsEmptyVectors = true,

			// TOML supports nested arrays.
			SupportsNestedVectors = true,

			// Empty strings are supported via quoted strings.
			SupportsEmptyStrings = true,

			// TOML distinguishes strings from numbers and booleans by quoting.
			PreservesAmbiguousStrings = true,

			// TOML supports unsigned integers, Int128, UInt128, BigInteger, decimal, and Half.
			SupportsUnsignedIntegers = true,
			SupportsInt128 = true,
			SupportsBigInteger = true,
			SupportsDecimal = true,
			SupportsHalf = true,

			// TOML supports NaN and Infinity.
			SupportsNonFiniteFloats = true,

			// TOML supports date/time and duration types.
			SupportsDateTime = true,
			SupportsTimeSpan = true,

			// TOML does not declare container sizes.
			ReportsContainerCounts = false,

			// TOML supports skip and path seek.
			SupportsSkip = true,
			SupportsPathSeek = true,

			// TOML reports EndDocument after the top-level value.
			ReportsEndDocument = true,

			// Truncating TOML can sometimes yield valid output, so we don't guarantee detection.
			DetectsTruncatedInput = false,

			// TOML rejects type mismatches (e.g., reading a string as a number).
			RejectsTypeMismatches = true,

			// Error paths are reported by the converter layer.
			ReportsErrorPaths = true,

			// TOML has no native reference preservation mechanism.
			SupportsReferencePreservation = false,

			// TOML can represent dynamic values through its typed scalars.
			SupportsDynamicValues = true,

			// TOML supports surrogate pairs via UTF-8 encoding.
			SupportsSurrogatePairs = true,

			// TOML supports control characters in strings via escaping.
			SupportsControlCharactersInStrings = true,

			// Reasonable nesting depth for TOML.
			MaxTestedNestingDepth = 24,
		};

		/// <inheritdoc/>
		public override ShapeShiftSerializer<TomlEncoder, TomlDecoder> CreateSerializer() => new TomlSerializer();

		/// <inheritdoc/>
		public override byte[] Encode(EncodeAction<TomlEncoder> action)
		{
			ArgumentNullException.ThrowIfNull(action);
			StringWriter writer = new();
			TomlEncoder encoder = new(writer);
			action(ref encoder);
			return Encoding.UTF8.GetBytes(writer.ToString());
		}

		/// <inheritdoc/>
		public override TResult Decode<TResult>(ReadOnlyMemory<byte> payload, DecodeFunc<TomlDecoder, TResult> func)
		{
			ArgumentNullException.ThrowIfNull(func);
			StringReader reader = new(Encoding.UTF8.GetString(payload.Span));
			TomlDecoder decoder = new(reader);
			return func(ref decoder);
		}
	}
}
