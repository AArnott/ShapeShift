// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Conformance;

/// <summary>
/// One named, runnable conformance expectation, already bound to the format that will be tested.
/// </summary>
/// <remarks>
/// This type is deliberately not generic so that a test framework can carry it through a data source
/// without naming the format's <see langword="ref" /> struct encoder and decoder types.
/// </remarks>
public sealed class ConformanceTestCase
{
	private readonly Action body;

	/// <summary>
	/// Initializes a new instance of the <see cref="ConformanceTestCase"/> class.
	/// </summary>
	/// <param name="name">The fully qualified case name, which is also its display name.</param>
	/// <param name="category">The category the case belongs to.</param>
	/// <param name="skipReason">The reason the case does not apply to this format, or <see langword="null" /> when it does.</param>
	/// <param name="body">The case itself, which throws to signal failure.</param>
	public ConformanceTestCase(string name, ConformanceCategory category, string? skipReason, Action body)
	{
		this.Name = Requires.NotNull(name);
		this.Category = category;
		this.SkipReason = skipReason;
		this.body = Requires.NotNull(body);
	}

	/// <summary>
	/// Gets the fully qualified case name, in the form <c>Format/Category/Case</c>.
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// Gets the category the case belongs to.
	/// </summary>
	public ConformanceCategory Category { get; }

	/// <summary>
	/// Gets the reason the case does not apply to this format, or <see langword="null" /> when it does.
	/// </summary>
	public string? SkipReason { get; }

	/// <summary>
	/// Gets a value indicating whether the case is inapplicable to this format.
	/// </summary>
	public bool IsSkipped => this.SkipReason is not null;

	/// <summary>
	/// Runs the case, throwing on failure.
	/// </summary>
	/// <exception cref="ConformanceSkippedException">Thrown when <see cref="IsSkipped"/> is <see langword="true" />.</exception>
	/// <exception cref="ConformanceAssertionException">Thrown when an expectation is not met.</exception>
	public void Run()
	{
		if (this.SkipReason is not null)
		{
			throw new ConformanceSkippedException(this.SkipReason);
		}

		this.body();
	}

	/// <summary>
	/// Runs the case and captures its outcome instead of throwing.
	/// </summary>
	/// <returns>The result of the case.</returns>
	public ConformanceResult Execute()
	{
		if (this.SkipReason is not null)
		{
			return new(this.Name, this.Category, ConformanceOutcome.Skipped, this.SkipReason, null);
		}

		try
		{
			this.body();
			return new(this.Name, this.Category, ConformanceOutcome.Passed, null, null);
		}
		catch (ConformanceSkippedException ex)
		{
			return new(this.Name, this.Category, ConformanceOutcome.Skipped, ex.Message, null);
		}
		catch (Exception ex)
		{
			return new(this.Name, this.Category, ConformanceOutcome.Failed, ex.Message, ex);
		}
	}

	/// <inheritdoc/>
	public override string ToString() => this.Name;
}
