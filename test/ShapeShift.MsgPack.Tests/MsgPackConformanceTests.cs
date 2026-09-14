// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using ShapeShift.Conformance;

namespace ShapeShift.MsgPack.Tests;

/// <summary>
/// Runs the shared <see cref="ConformanceSuite"/> against <see cref="MsgPackEncoder"/> and <see cref="MsgPackDecoder"/>.
/// </summary>
public class MsgPackConformanceTests
{
	/// <summary>
	/// Gets every conformance case that applies to MessagePack.
	/// </summary>
	/// <returns>The cases, each wrapped in a factory as TUnit data sources require.</returns>
	public static IEnumerable<Func<ConformanceTestCase>> Cases()
		=> ConformanceSuite.CreateTestCases(new MsgPackAdapter())
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
	/// Asserts that every applicable case passes, and reports what MessagePack opted out of.
	/// </summary>
	/// <returns>A task tracking the assertion.</returns>
	[Test]
	public async Task WholeSuiteIsConformant()
	{
		ConformanceReport report = ConformanceSuite.Run(new MsgPackAdapter());
		foreach (ConformanceResult result in report.Results.Where(r => r.Outcome != ConformanceOutcome.Passed))
		{
			Console.WriteLine(result.ToString());
		}

		report.ThrowIfNotConformant();
		await Assert.That(report.PassedCount).IsGreaterThan(50);
	}

	/// <summary>
	/// Describes MessagePack to the conformance kit.
	/// </summary>
	private sealed class MsgPackAdapter : FormatConformanceAdapter<MsgPackEncoder, MsgPackDecoder>
	{
		/// <inheritdoc/>
		public override string FormatName => "MsgPack";

		/// <inheritdoc/>
		public override FormatConformanceOptions Options { get; } = new()
		{
			// Every container is length-prefixed, so the decoder always knows the element count up front.
			ReportsContainerCounts = true,
		};

		/// <inheritdoc/>
		public override TokenType GetExpectedTokenType(ConformanceValueKind kind) => kind switch
		{
			// Timestamps and durations use MessagePack extension types, whose payload is opaque bytes,
			// so the decoder classifies them alongside the bin family.
			ConformanceValueKind.DateTime => TokenType.Binary,
			ConformanceValueKind.TimeSpan => TokenType.Binary,
			_ => base.GetExpectedTokenType(kind),
		};

		/// <inheritdoc/>
		public override ShapeShiftSerializer<MsgPackEncoder, MsgPackDecoder> CreateSerializer() => new MsgPackSerializer();

		/// <inheritdoc/>
		public override IValueBoundaryScanner CreateValueBoundaryScanner() => new MsgPackValueBoundaryScanner();

		/// <inheritdoc/>
		public override byte[] Encode(EncodeAction<MsgPackEncoder> action)
		{
			ArgumentNullException.ThrowIfNull(action);
			ArrayBufferWriter<byte> buffer = new();
			MsgPackEncoder encoder = new(buffer);
			action(ref encoder);
			return buffer.WrittenSpan.ToArray();
		}

		/// <inheritdoc/>
		public override TResult Decode<TResult>(ReadOnlyMemory<byte> payload, DecodeFunc<MsgPackDecoder, TResult> func)
		{
			ArgumentNullException.ThrowIfNull(func);
			MsgPackDecoder decoder = new(payload.Span);
			return func(ref decoder);
		}
	}
}
