// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Conformance;

/// <summary>
/// The outcome of running one conformance case.
/// </summary>
/// <param name="Name">The fully qualified name of the case.</param>
/// <param name="Category">The category the case belongs to.</param>
/// <param name="Outcome">How the case finished.</param>
/// <param name="Message">The failure message or skip reason, or <see langword="null" /> when the case passed.</param>
/// <param name="Exception">The exception that caused a failure, when there was one.</param>
public record struct ConformanceResult(
	string Name,
	ConformanceCategory Category,
	ConformanceOutcome Outcome,
	string? Message,
	Exception? Exception)
{
	/// <inheritdoc/>
	public override readonly string ToString()
		=> this.Message is null ? $"{this.Outcome}: {this.Name}" : $"{this.Outcome}: {this.Name} -- {this.Message}";
}
