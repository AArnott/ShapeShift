// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using PolyType;
using ShapeShift.Taml;
using ShapeShift.Yaml;

namespace ShapeShift.Benchmarks;

/// <summary>
/// Measures the cost and allocation impact of string interning during deserialization.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public partial class StringInterning
{
	private static readonly TamlSerializer TamlNonInterning = new() { InternStrings = false };
	private static readonly TamlSerializer TamlInterning = new() { InternStrings = true };
	private static readonly YamlSerializer YamlNonInterning = new() { InternStrings = false };
	private static readonly YamlSerializer YamlInterning = new() { InternStrings = true };
	private static readonly StringProperties Duplicated = new()
	{
		First = "Duplicate string value",
		Second = "Duplicate string value",
		Third = "Duplicate string value",
		Fourth = "Duplicate string value",
	};

	private static readonly StringProperties Unique = new()
	{
		First = "First unique string value",
		Second = "Second unique string value",
		Third = "Third unique string value",
		Fourth = "Fourth unique string value",
	};

	private static readonly StringProperties EscapedDuplicated = new()
	{
		First = "First line\nSecond line",
		Second = "First line\nSecond line",
		Third = "First line\nSecond line",
		Fourth = "First line\nSecond line",
	};

	private static readonly string DuplicatedTaml = TamlNonInterning.Serialize(Duplicated);
	private static readonly string UniqueTaml = TamlNonInterning.Serialize(Unique);
	private static readonly string EscapedDuplicatedTaml = TamlNonInterning.Serialize(EscapedDuplicated);
	private static readonly string DuplicatedYaml = YamlNonInterning.Serialize(Duplicated);
	private static readonly string UniqueYaml = YamlNonInterning.Serialize(Unique);
	private static readonly string EscapedDuplicatedYaml = YamlNonInterning.Serialize(EscapedDuplicated);

	/// <summary>Deserializes duplicated TAML strings without interning.</summary>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("TAML", "Duplicated")]
	public void DeserializeDuplicatedTamlWithoutInterning() => TamlNonInterning.Deserialize<StringProperties>(DuplicatedTaml);

	/// <summary>Deserializes duplicated TAML strings with interning.</summary>
	[Benchmark]
	[BenchmarkCategory("TAML", "Duplicated")]
	public void DeserializeDuplicatedTamlWithInterning() => TamlInterning.Deserialize<StringProperties>(DuplicatedTaml);

	/// <summary>Deserializes unique TAML strings without interning.</summary>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("TAML", "Unique")]
	public void DeserializeUniqueTamlWithoutInterning() => TamlNonInterning.Deserialize<StringProperties>(UniqueTaml);

	/// <summary>Deserializes unique TAML strings with interning.</summary>
	[Benchmark]
	[BenchmarkCategory("TAML", "Unique")]
	public void DeserializeUniqueTamlWithInterning() => TamlInterning.Deserialize<StringProperties>(UniqueTaml);

	/// <summary>Deserializes duplicated escaped TAML strings without interning.</summary>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("TAML", "Escaped")]
	public void DeserializeEscapedDuplicatedTamlWithoutInterning() => TamlNonInterning.Deserialize<StringProperties>(EscapedDuplicatedTaml);

	/// <summary>Deserializes duplicated escaped TAML strings with interning.</summary>
	[Benchmark]
	[BenchmarkCategory("TAML", "Escaped")]
	public void DeserializeEscapedDuplicatedTamlWithInterning() => TamlInterning.Deserialize<StringProperties>(EscapedDuplicatedTaml);

	/// <summary>Deserializes duplicated YAML strings without interning.</summary>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("YAML", "Duplicated")]
	public void DeserializeDuplicatedYamlWithoutInterning() => YamlNonInterning.Deserialize<StringProperties>(DuplicatedYaml);

	/// <summary>Deserializes duplicated YAML strings with interning.</summary>
	[Benchmark]
	[BenchmarkCategory("YAML", "Duplicated")]
	public void DeserializeDuplicatedYamlWithInterning() => YamlInterning.Deserialize<StringProperties>(DuplicatedYaml);

	/// <summary>Deserializes duplicated escaped YAML strings without interning.</summary>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("YAML", "Escaped")]
	public void DeserializeEscapedDuplicatedYamlWithoutInterning() => YamlNonInterning.Deserialize<StringProperties>(EscapedDuplicatedYaml);

	/// <summary>Deserializes duplicated escaped YAML strings with interning.</summary>
	[Benchmark]
	[BenchmarkCategory("YAML", "Escaped")]
	public void DeserializeEscapedDuplicatedYamlWithInterning() => YamlInterning.Deserialize<StringProperties>(EscapedDuplicatedYaml);

	/// <summary>Gets a benchmark payload with four string properties.</summary>
	[GenerateShape]
	public partial record StringProperties
	{
		/// <summary>Gets the first value.</summary>
		public string First { get; init; } = string.Empty;

		/// <summary>Gets the second value.</summary>
		public string Second { get; init; } = string.Empty;

		/// <summary>Gets the third value.</summary>
		public string Third { get; init; } = string.Empty;

		/// <summary>Gets the fourth value.</summary>
		public string Fourth { get; init; } = string.Empty;
	}
}
