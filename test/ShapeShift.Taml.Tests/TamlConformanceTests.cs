// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using ShapeShift.Conformance;

namespace ShapeShift.Taml.Tests;

/// <summary>
/// Runs the shared <see cref="ConformanceSuite"/> against <see cref="TamlEncoder"/> and <see cref="TamlDecoder"/>.
/// </summary>
public class TamlConformanceTests
{
	/// <summary>
	/// Gets every conformance case that applies to TAML.
	/// </summary>
	/// <returns>The cases, each wrapped in a factory as TUnit data sources require.</returns>
	public static IEnumerable<Func<ConformanceTestCase>> Cases()
		=> ConformanceSuite.CreateTestCases(new TamlAdapter())
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
	/// Asserts that every applicable case passes, and reports what TAML opted out of.
	/// </summary>
	/// <returns>A task tracking the assertion.</returns>
	[Test]
	public async Task WholeSuiteIsConformant()
	{
		ConformanceReport report = ConformanceSuite.Run(new TamlAdapter());
		foreach (ConformanceResult result in report.Results.Where(r => r.Outcome != ConformanceOutcome.Passed))
		{
			Console.WriteLine(result.ToString());
		}

		report.ThrowIfNotConformant();
		await Assert.That(report.PassedCount).IsGreaterThan(40);
	}

	/// <summary>
	/// Describes TAML to the conformance kit.
	/// </summary>
	private sealed class TamlAdapter : FormatConformanceAdapter<TamlEncoder, TamlDecoder>
	{
		/// <inheritdoc/>
		public override string FormatName => "Taml";

		/// <inheritdoc/>
		public override FormatConformanceOptions Options { get; } = new()
		{
			// TAML is plain text with no binary family.
			SupportsBinary = false,

			// Structure is carried entirely by indentation, so a container with no children has
			// nothing to indent and cannot be told apart from a scalar or from an absent value.
			SupportsEmptyMaps = false,
			SupportsEmptyVectors = false,

			// A document is a map or a single scalar: a bare list of lines at the root is
			// indistinguishable from a multi-line scalar.
			SupportsRootVectors = false,

			// A vector's shape is inferred from its first child line, so a vector whose elements do not
			// all look alike cannot be recognized.
			SupportsHeterogeneousVectors = false,

			// A vector's items are lines at one indentation level, so an item cannot itself be a
			// bare vector without a key to hang it from.
			SupportsNestedVectors = false,

			// An empty line carries no value, so an empty string cannot survive as itself.
			SupportsEmptyStrings = false,

			// Every scalar is untyped text. A boolean is the word "true", and a string is only
			// distinguishable from a number or a null by quoting, which the encoder applies.
			SupportsNonFiniteFloats = false,

			// Text lines are separated by newlines, so a string containing one cannot be written
			// without quoting, which TAML applies -- but a lone carriage return still normalizes.
			SupportsControlCharactersInStrings = false,

			// Indentation is not length-prefixed, so the decoder learns a container's size only by
			// reading it.
			ReportsContainerCounts = false,

			// Truncating a line-oriented text document usually yields another valid document.
			DetectsTruncatedInput = false,

			// A structural problem shows up while the decoder is inferring where containers begin, which
			// is before the converter layer can attribute it to a member.
			ReportsErrorPaths = false,

			// Reference preservation needs a format-specific back-reference token, which TAML lacks.
			SupportsReferencePreservation = false,

			// A dynamic value is reconstructed from token types, and TAML's untyped scalars cannot
			// carry the distinctions a faithful dynamic round trip needs.
			SupportsDynamicValues = false,

			// Deep indentation is legal but noisy; a dozen levels exercises the decoder's frame stack.
			MaxTestedNestingDepth = 12,
		};

		/// <inheritdoc/>
		public override TokenType GetExpectedTokenType(ConformanceValueKind kind) => kind switch
		{
			// TAML scalars are untyped text: a boolean is the bare word "true" or "false", and a
			// timestamp or duration is its round-trip text form.
			ConformanceValueKind.Boolean => TokenType.String,
			_ => base.GetExpectedTokenType(kind),
		};

		/// <inheritdoc/>
		public override ShapeShiftSerializer<TamlEncoder, TamlDecoder> CreateSerializer() => new TamlSerializer();

		/// <inheritdoc/>
		public override byte[] Encode(EncodeAction<TamlEncoder> action)
		{
			ArgumentNullException.ThrowIfNull(action);
			StringWriter writer = new();
			TamlEncoder encoder = new(writer);
			action(ref encoder);
			return Encoding.UTF8.GetBytes(writer.ToString());
		}

		/// <inheritdoc/>
		public override TResult Decode<TResult>(ReadOnlyMemory<byte> payload, DecodeFunc<TamlDecoder, TResult> func)
		{
			ArgumentNullException.ThrowIfNull(func);
			StringReader reader = new(Encoding.UTF8.GetString(payload.Span));
			TamlDecoder decoder = new(reader);
			return func(ref decoder);
		}
	}
}
