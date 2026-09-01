// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using ShapeShift.Conformance;

namespace ShapeShift.Yaml.Tests;

/// <summary>
/// Runs the shared <see cref="ConformanceSuite"/> against <see cref="YamlEncoder"/> and <see cref="YamlDecoder"/>.
/// </summary>
public class YamlConformanceTests
{
	/// <summary>
	/// Gets every conformance case that applies to YAML.
	/// </summary>
	/// <returns>The cases, each wrapped in a factory as TUnit data sources require.</returns>
	public static IEnumerable<Func<ConformanceTestCase>> Cases()
		=> ConformanceSuite.CreateTestCases(new YamlAdapter())
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
	/// Asserts that every applicable case passes, and reports what YAML opted out of.
	/// </summary>
	/// <returns>A task tracking the assertion.</returns>
	[Test]
	public async Task WholeSuiteIsConformant()
	{
		ConformanceReport report = ConformanceSuite.Run(new YamlAdapter());
		foreach (ConformanceResult result in report.Results.Where(r => r.Outcome != ConformanceOutcome.Passed))
		{
			Console.WriteLine(result.ToString());
		}

		report.ThrowIfNotConformant();
		await Assert.That(report.PassedCount).IsGreaterThan(40);
	}

	/// <summary>
	/// Describes YAML to the conformance kit.
	/// </summary>
	private sealed class YamlAdapter : FormatConformanceAdapter<YamlEncoder, YamlDecoder>
	{
		/// <inheritdoc/>
		public override string FormatName => "Yaml";

		/// <inheritdoc/>
		public override FormatConformanceOptions Options { get; } = new()
		{
			// YAML has no binary family of its own; this encoder writes no binary at all.
			SupportsBinary = false,

			// Structure is carried by indentation, so a container with no children has nothing to
			// indent and cannot be told apart from an absent value.
			SupportsEmptyMaps = false,
			SupportsEmptyVectors = false,

			// This encoder writes a document as a block mapping or a scalar.
			SupportsRootVectors = false,

			// A block sequence's shape is inferred from its first item.
			SupportsHeterogeneousVectors = false,
			SupportsNestedVectors = false,

			// An empty line carries no value, so an empty string cannot survive as itself.
			SupportsEmptyStrings = false,

			// .inf and .nan are YAML spellings this encoder does not produce.
			SupportsNonFiniteFloats = false,

			// A newline inside a scalar needs a block or quoted form this encoder normalizes.
			SupportsControlCharactersInStrings = false,

			// Indentation is not length-prefixed, so a container's size is known only after reading it.
			ReportsContainerCounts = false,

			// Truncating a line-oriented text document usually yields another valid document.
			DetectsTruncatedInput = false,

			// A structural problem shows up while the decoder is inferring where containers begin, which
			// is before the converter layer can attribute it to a member.
			ReportsErrorPaths = false,

			// Reference preservation needs a format-specific back-reference token, which this encoder lacks.
			SupportsReferencePreservation = false,

			// A dynamic value is reconstructed from token types, and YAML's untyped scalars cannot
			// carry the distinctions a faithful dynamic round trip needs.
			SupportsDynamicValues = false,

			MaxTestedNestingDepth = 12,
		};

		/// <inheritdoc/>
		public override TokenType GetExpectedTokenType(ConformanceValueKind kind) => kind switch
		{
			// YAML scalars are untyped text.
			ConformanceValueKind.Boolean => TokenType.String,
			_ => base.GetExpectedTokenType(kind),
		};

		/// <inheritdoc/>
		public override ShapeShiftSerializer<YamlEncoder, YamlDecoder> CreateSerializer() => new YamlSerializer();

		/// <inheritdoc/>
		public override byte[] Encode(EncodeAction<YamlEncoder> action)
		{
			ArgumentNullException.ThrowIfNull(action);
			StringWriter writer = new();
			YamlEncoder encoder = new(writer);
			action(ref encoder);
			return Encoding.UTF8.GetBytes(writer.ToString());
		}

		/// <inheritdoc/>
		public override TResult Decode<TResult>(ReadOnlyMemory<byte> payload, DecodeFunc<YamlDecoder, TResult> func)
		{
			ArgumentNullException.ThrowIfNull(func);
			StringReader reader = new(Encoding.UTF8.GetString(payload.Span));
			YamlDecoder decoder = new(reader);
			return func(ref decoder);
		}
	}
}
