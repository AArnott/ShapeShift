// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ShapeShift.Conformance;

namespace ShapeShift.Protobuf.Tests;

/// <summary>
/// Runs the shared <see cref="ConformanceSuite"/> against <see cref="ProtobufEncoder"/> and <see cref="ProtobufDecoder"/>.
/// </summary>
public class ProtobufConformanceTests
{
	/// <summary>
	/// Gets every conformance case that applies to protobuf.
	/// </summary>
	/// <returns>The cases, each wrapped in a factory as TUnit data sources require.</returns>
	public static IEnumerable<Func<ConformanceTestCase>> Cases()
		=> ConformanceSuite.CreateTestCases(new ProtobufAdapter())
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
	/// Asserts that every applicable case passes, and reports what protobuf opted out of.
	/// </summary>
	/// <returns>A task tracking the assertion.</returns>
	[Test]
	public async Task WholeSuiteIsConformant()
	{
		ConformanceReport report = ConformanceSuite.Run(new ProtobufAdapter());
		foreach (ConformanceResult result in report.Results.Where(r => r.Outcome != ConformanceOutcome.Passed))
		{
			Console.WriteLine(result.ToString());
		}

		report.ThrowIfNotConformant();
		await Assert.That(report.PassedCount).IsGreaterThan(50);
	}

	/// <summary>
	/// Describes protobuf to the conformance kit.
	/// </summary>
	private sealed class ProtobufAdapter : FormatConformanceAdapter<ProtobufEncoder, ProtobufDecoder>
	{
		/// <inheritdoc/>
		public override string FormatName => "Protobuf";

		/// <inheritdoc/>
		public override FormatConformanceOptions Options { get; } = new()
		{
			ReportsContainerCounts = false,
			SupportsReferencePreservation = false,
		};

		/// <inheritdoc/>
		public override ShapeShiftSerializer<ProtobufEncoder, ProtobufDecoder> CreateSerializer() => new ProtobufSerializer();

		/// <inheritdoc/>
		public override byte[] Encode(EncodeAction<ProtobufEncoder> action)
		{
			ArgumentNullException.ThrowIfNull(action);
			MemoryStream stream = new();
			ProtobufEncoder encoder = new(stream);
			action(ref encoder);
			return stream.ToArray();
		}

		/// <inheritdoc/>
		public override TResult Decode<TResult>(ReadOnlyMemory<byte> payload, DecodeFunc<ProtobufDecoder, TResult> func)
		{
			ArgumentNullException.ThrowIfNull(func);
			ProtobufDecoder decoder = new(payload.Span);
			return func(ref decoder);
		}
	}
}
