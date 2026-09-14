// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace ShapeShift.Conformance;

/// <summary>
/// The outcome of running the whole conformance suite against one format.
/// </summary>
public sealed class ConformanceReport
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ConformanceReport"/> class.
	/// </summary>
	/// <param name="formatName">The name of the format that was tested.</param>
	/// <param name="results">The result of every case, in the order they ran.</param>
	public ConformanceReport(string formatName, IReadOnlyList<ConformanceResult> results)
	{
		this.FormatName = Requires.NotNull(formatName);
		this.Results = Requires.NotNull(results);
	}

	/// <summary>
	/// Gets the name of the format that was tested.
	/// </summary>
	public string FormatName { get; }

	/// <summary>
	/// Gets the result of every case, in the order they ran.
	/// </summary>
	public IReadOnlyList<ConformanceResult> Results { get; }

	/// <summary>
	/// Gets the number of cases that passed.
	/// </summary>
	public int PassedCount => this.Results.Count(r => r.Outcome == ConformanceOutcome.Passed);

	/// <summary>
	/// Gets the number of cases that were skipped because of a declared format limitation.
	/// </summary>
	public int SkippedCount => this.Results.Count(r => r.Outcome == ConformanceOutcome.Skipped);

	/// <summary>
	/// Gets the number of cases that failed.
	/// </summary>
	public int FailedCount => this.Results.Count(r => r.Outcome == ConformanceOutcome.Failed);

	/// <summary>
	/// Gets a value indicating whether every applicable case passed.
	/// </summary>
	public bool IsConformant => this.FailedCount == 0;

	/// <summary>
	/// Throws when any case failed.
	/// </summary>
	/// <exception cref="ConformanceAssertionException">Thrown when <see cref="IsConformant"/> is <see langword="false" />.</exception>
	public void ThrowIfNotConformant()
	{
		if (this.IsConformant)
		{
			return;
		}

		StringBuilder builder = new();
		builder.Append(CultureInfo.InvariantCulture, $"{this.FailedCount} of {this.Results.Count} {this.FormatName} conformance cases failed:");
		foreach (ConformanceResult result in this.Results.Where(r => r.Outcome == ConformanceOutcome.Failed))
		{
			builder.AppendLine().Append("  ").Append(result.ToString());
		}

		throw new ConformanceAssertionException(builder.ToString());
	}

	/// <inheritdoc/>
	public override string ToString()
		=> $"{this.FormatName}: {this.PassedCount} passed, {this.FailedCount} failed, {this.SkippedCount} skipped.";
}
