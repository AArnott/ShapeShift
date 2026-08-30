// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text.Json;
using ShapeShift.Conformance;

namespace ShapeShift.Json.Tests;

/// <summary>
/// Runs the shared <see cref="ConformanceSuite"/> against <see cref="JsonEncoder"/> and <see cref="JsonDecoder"/>.
/// </summary>
public class JsonConformanceTests
{
	/// <summary>
	/// Gets every conformance case that applies to JSON.
	/// </summary>
	/// <returns>The cases, each wrapped in a factory as TUnit data sources require.</returns>
	public static IEnumerable<Func<ConformanceTestCase>> Cases()
		=> ConformanceSuite.CreateTestCases(new JsonAdapter())
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
	/// Asserts that every applicable case passes, and reports what JSON opted out of.
	/// </summary>
	/// <returns>A task tracking the assertion.</returns>
	[Test]
	public async Task WholeSuiteIsConformant()
	{
		ConformanceReport report = ConformanceSuite.Run(new JsonAdapter());
		foreach (ConformanceResult result in report.Results.Where(r => r.Outcome != ConformanceOutcome.Passed))
		{
			Console.WriteLine(result.ToString());
		}

		report.ThrowIfNotConformant();
		await Assert.That(report.PassedCount).IsGreaterThan(50);
	}

	/// <summary>
	/// Describes JSON to the conformance kit.
	/// </summary>
	private sealed class JsonAdapter : FormatConformanceAdapter<JsonEncoder, JsonDecoder>
	{
		/// <inheritdoc/>
		public override string FormatName => "Json";

		/// <inheritdoc/>
		public override FormatConformanceOptions Options { get; } = new()
		{
			// JSON has no dedicated binary family: byte arrays travel as base64 strings, so a binary value
			// is indistinguishable from a string at the token level, but it still round-trips.
			SupportsBinary = true,

			// A JSON number cannot be NaN or infinite. ShapeShift.Json can opt into the conventional
			// "NaN"/"Infinity" strings, but that is off by default and is not part of the format.
			SupportsNonFiniteFloats = false,

			// Reference preservation needs a format-specific back-reference token, which JSON has not defined.
			SupportsReferencePreservation = false,

			// Utf8JsonReader reports no element count until a container has been fully read.
			ReportsContainerCounts = false,
		};

		/// <inheritdoc/>
		public override TokenType GetExpectedTokenType(ConformanceValueKind kind) => kind switch
		{
			// Byte arrays are base64 strings in JSON.
			ConformanceValueKind.Binary => TokenType.String,
			_ => base.GetExpectedTokenType(kind),
		};

		/// <inheritdoc/>
		public override ShapeShiftSerializer<JsonEncoder, JsonDecoder> CreateSerializer() => new JsonSerializer();

		/// <inheritdoc/>
		public override IValueBoundaryScanner CreateValueBoundaryScanner() => new JsonValueBoundaryScanner();

		/// <inheritdoc/>
		public override byte[] Encode(EncodeAction<JsonEncoder> action)
		{
			ArgumentNullException.ThrowIfNull(action);
			ArrayBufferWriter<byte> buffer = new();
			using Utf8JsonWriter writer = new(buffer);
			JsonEncoder encoder = new(writer);
			action(ref encoder);
			writer.Flush();
			return buffer.WrittenSpan.ToArray();
		}

		/// <inheritdoc/>
		public override TResult Decode<TResult>(ReadOnlyMemory<byte> payload, DecodeFunc<JsonDecoder, TResult> func)
		{
			ArgumentNullException.ThrowIfNull(func);
			JsonDecoder decoder = new(payload.Span);
			return func(ref decoder);
		}
	}
}
