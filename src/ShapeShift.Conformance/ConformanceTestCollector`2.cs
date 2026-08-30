// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Conformance;

/// <summary>
/// Accumulates the cases that make up a format's conformance run.
/// </summary>
/// <typeparam name="TEncoder">The format's encoder type.</typeparam>
/// <typeparam name="TDecoder">The format's decoder type.</typeparam>
/// <remarks>
/// The built-in suites and any format-specific cases added from
/// <see cref="FormatConformanceAdapter{TEncoder, TDecoder}.AddFormatSpecificTests"/> share this collector,
/// so custom cases are reported and filtered exactly like the built-in ones.
/// </remarks>
public sealed class ConformanceTestCollector<TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	private readonly List<ConformanceTestCase> cases = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="ConformanceTestCollector{TEncoder, TDecoder}"/> class.
	/// </summary>
	/// <param name="adapter">The adapter under test.</param>
	public ConformanceTestCollector(FormatConformanceAdapter<TEncoder, TDecoder> adapter)
	{
		this.Adapter = Requires.NotNull(adapter);
	}

	/// <summary>
	/// Gets the adapter under test.
	/// </summary>
	public FormatConformanceAdapter<TEncoder, TDecoder> Adapter { get; }

	/// <summary>
	/// Gets the format's declared capabilities.
	/// </summary>
	public FormatConformanceOptions Options => this.Adapter.Options;

	/// <summary>
	/// Gets the cases collected so far.
	/// </summary>
	public IReadOnlyList<ConformanceTestCase> Cases => this.cases;

	/// <summary>
	/// Gets or sets the category assigned to cases added without an explicit one.
	/// </summary>
	/// <remarks>Each built-in suite sets this once before adding its cases.</remarks>
	public ConformanceCategory CurrentCategory { get; set; } = ConformanceCategory.None;

	/// <summary>
	/// Adds a case that always applies.
	/// </summary>
	/// <param name="name">The case name, unique within its category.</param>
	/// <param name="body">The case body. It receives the adapter under test and throws to signal failure.</param>
	public void Add(string name, Action<FormatConformanceAdapter<TEncoder, TDecoder>> body)
		=> this.Add(name, null, body);

	/// <summary>
	/// Adds a case that may not apply to this format.
	/// </summary>
	/// <param name="name">The case name, unique within its category.</param>
	/// <param name="skipReason">The reason the case does not apply, or <see langword="null" /> when it does.</param>
	/// <param name="body">The case body. It receives the adapter under test and throws to signal failure.</param>
	public void Add(string name, string? skipReason, Action<FormatConformanceAdapter<TEncoder, TDecoder>> body)
	{
		Requires.NotNull(name);
		Requires.NotNull(body);
		FormatConformanceAdapter<TEncoder, TDecoder> adapter = this.Adapter;
		this.cases.Add(new ConformanceTestCase(
			$"{adapter.FormatName}/{this.CurrentCategory}/{name}",
			this.CurrentCategory,
			skipReason,
			() => body(adapter)));
	}

	/// <summary>
	/// Adds a case that applies only when the format declares a capability.
	/// </summary>
	/// <param name="name">The case name, unique within its category.</param>
	/// <param name="condition">Whether the format supports what the case exercises.</param>
	/// <param name="skipReason">The reason to report when <paramref name="condition"/> is <see langword="false" />.</param>
	/// <param name="body">The case body. It receives the adapter under test and throws to signal failure.</param>
	public void AddIf(string name, bool condition, string skipReason, Action<FormatConformanceAdapter<TEncoder, TDecoder>> body)
		=> this.Add(name, condition ? null : skipReason, body);
}
