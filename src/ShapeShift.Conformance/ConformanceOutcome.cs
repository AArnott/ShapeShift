// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Conformance;

/// <summary>
/// How a conformance case finished.
/// </summary>
public enum ConformanceOutcome
{
	/// <summary>The case met every expectation.</summary>
	Passed,

	/// <summary>The case does not apply to the format because of a declared limitation.</summary>
	Skipped,

	/// <summary>The case did not meet an expectation.</summary>
	Failed,
}
