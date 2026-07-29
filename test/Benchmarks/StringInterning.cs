// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using PolyType;
using ShapeShift.Taml;
using ShapeShift.Yaml;

namespace ShapeShift.Benchmarks;

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

	private static readonly string DuplicatedTaml = TamlNonInterning.Serialize(Duplicated);
	private static readonly string UniqueTaml = TamlNonInterning.Serialize(Unique);
	private static readonly string DuplicatedYaml = YamlNonInterning.Serialize(Duplicated);
	private static readonly string UniqueYaml = YamlNonInterning.Serialize(Unique);

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("TAML", "Duplicated")]
	public void DeserializeDuplicatedTamlWithoutInterning() => TamlNonInterning.Deserialize<StringProperties>(DuplicatedTaml);

	[Benchmark]
	[BenchmarkCategory("TAML", "Duplicated")]
	public void DeserializeDuplicatedTamlWithInterning() => TamlInterning.Deserialize<StringProperties>(DuplicatedTaml);

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("TAML", "Unique")]
	public void DeserializeUniqueTamlWithoutInterning() => TamlNonInterning.Deserialize<StringProperties>(UniqueTaml);

	[Benchmark]
	[BenchmarkCategory("TAML", "Unique")]
	public void DeserializeUniqueTamlWithInterning() => TamlInterning.Deserialize<StringProperties>(UniqueTaml);

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("YAML", "Duplicated")]
	public void DeserializeDuplicatedYamlWithoutInterning() => YamlNonInterning.Deserialize<StringProperties>(DuplicatedYaml);

	[Benchmark]
	[BenchmarkCategory("YAML", "Duplicated")]
	public void DeserializeDuplicatedYamlWithInterning() => YamlInterning.Deserialize<StringProperties>(DuplicatedYaml);

	[GenerateShape]
	public partial record StringProperties
	{
		public string First { get; init; } = string.Empty;

		public string Second { get; init; } = string.Empty;

		public string Third { get; init; } = string.Empty;

		public string Fourth { get; init; } = string.Empty;
	}
}
