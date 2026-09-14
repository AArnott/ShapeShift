// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Conformance;

/// <summary>
/// Contributes a group of related cases to a conformance run.
/// </summary>
/// <typeparam name="TEncoder">The format's encoder type.</typeparam>
/// <typeparam name="TDecoder">The format's decoder type.</typeparam>
/// <remarks>
/// Implement this to package a reusable group of format-specific cases, then pass it to
/// <see cref="ConformanceSuite.CreateTestCases"/>. For a handful of one-off cases, override
/// <see cref="FormatConformanceAdapter{TEncoder, TDecoder}.AddFormatSpecificTests"/> instead.
/// </remarks>
public interface IConformanceSuite<TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <summary>
	/// Gets the category the suite's cases belong to.
	/// </summary>
	ConformanceCategory Category { get; }

	/// <summary>
	/// Adds this suite's cases to a run.
	/// </summary>
	/// <param name="collector">The collector to add to. Its <see cref="ConformanceTestCollector{TEncoder, TDecoder}.CurrentCategory"/> is already set to <see cref="Category"/>.</param>
	void AddTests(ConformanceTestCollector<TEncoder, TDecoder> collector);
}
