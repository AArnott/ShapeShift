// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Formats.Cbor;
using ShapeShift.Conformance;

namespace ShapeShift.Cbor.Tests;

/// <summary>
/// Runs the shared <see cref="ConformanceSuite"/> against <see cref="CborEncoder"/> and <see cref="CborDecoder"/>.
/// </summary>
public class CborConformanceTests
{
	/// <summary>
	/// Gets every conformance case that applies to CBOR.
	/// </summary>
	/// <returns>The cases, each wrapped in a factory as TUnit data sources require.</returns>
	public static IEnumerable<Func<ConformanceTestCase>> Cases()
		=> ConformanceSuite.CreateTestCases(new CborAdapter())
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
	/// Asserts that every applicable conformance case passes.
	/// </summary>
	/// <returns>A task tracking the assertion.</returns>
	[Test]
	public async Task WholeSuiteIsConformant()
	{
		ConformanceReport report = ConformanceSuite.Run(new CborAdapter());
		foreach (ConformanceResult result in report.Results.Where(r => r.Outcome != ConformanceOutcome.Passed))
		{
			Console.WriteLine(result.ToString());
		}

		report.ThrowIfNotConformant();
		await Assert.That(report.PassedCount).IsGreaterThan(50);
	}

	private sealed class CborAdapter : FormatConformanceAdapter<CborEncoder, CborDecoder>
	{
		/// <inheritdoc/>
		public override string FormatName => "Cbor";

		/// <inheritdoc/>
		public override FormatConformanceOptions Options { get; } = new()
		{
			ReportsContainerCounts = true,
			SupportsReferencePreservation = false,
		};

		/// <inheritdoc/>
		public override TokenType GetExpectedTokenType(ConformanceValueKind kind) => kind switch
		{
			ConformanceValueKind.DateTime => TokenType.Number,
			ConformanceValueKind.TimeSpan => TokenType.Number,
			_ => base.GetExpectedTokenType(kind),
		};

		/// <inheritdoc/>
		public override ShapeShiftSerializer<CborEncoder, CborDecoder> CreateSerializer() => new CborSerializer();

		/// <inheritdoc/>
		public override byte[] Encode(EncodeAction<CborEncoder> action)
		{
			ArgumentNullException.ThrowIfNull(action);
			CborWriter writer = new();
			CborEncoder encoder = new(writer);
			action(ref encoder);
			return writer.Encode();
		}

		/// <inheritdoc/>
		public override TResult Decode<TResult>(ReadOnlyMemory<byte> payload, DecodeFunc<CborDecoder, TResult> func)
		{
			ArgumentNullException.ThrowIfNull(func);
			CborDecoder decoder = new(payload);
			return func(ref decoder);
		}
	}
}
