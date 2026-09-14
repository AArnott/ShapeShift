// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using ShapeShiftJsonSerializer = ShapeShift.Json.JsonSerializer;
using SystemTextJsonSerializer = System.Text.Json.JsonSerializer;

namespace ShapeShift.Benchmarks;

/// <summary>
/// Compares JSON serialization on deterministic, source-generated object graphs.
/// </summary>
[MemoryDiagnoser]
public class JsonSerializerBenchmarks
{
	private readonly ShapeShiftJsonSerializer shapeShiftSerializer = new();
	private PerformancePayload payload = null!;

	/// <summary>
	/// Gets or sets the number of orders in the object graph.
	/// </summary>
	[Params(1, 100, 1_000)]
	public int OrderCount { get; set; }

	/// <summary>
	/// Creates deterministic benchmark inputs and confirms both implementations produce equivalent values.
	/// </summary>
	[GlobalSetup]
	public void Setup()
	{
		this.payload = PerformancePayload.Create(this.OrderCount);
		byte[] shapeShiftPayload = this.shapeShiftSerializer.SerializeToUtf8Bytes(this.payload);
		byte[] systemTextJsonPayload = SystemTextJsonSerializer.SerializeToUtf8Bytes(this.payload, PerformanceJsonSerializerContext.Default.PerformancePayload);
		PerformancePayload.AssertEquivalent(this.payload, this.shapeShiftSerializer.Deserialize<PerformancePayload>(shapeShiftPayload));
		PerformancePayload.AssertEquivalent(this.payload, SystemTextJsonSerializer.Deserialize(systemTextJsonPayload, PerformanceJsonSerializerContext.Default.PerformancePayload));
	}

	/// <summary>
	/// Serializes using ShapeShift's UTF-8 JSON API.
	/// </summary>
	/// <returns>The JSON document.</returns>
	[Benchmark(Baseline = true)]
	public byte[] ShapeShiftSerialize() => this.shapeShiftSerializer.SerializeToUtf8Bytes(this.payload);

	/// <summary>
	/// Serializes using System.Text.Json's source-generated metadata.
	/// </summary>
	/// <returns>The JSON document.</returns>
	[Benchmark]
	public byte[] SystemTextJsonSerialize() => SystemTextJsonSerializer.SerializeToUtf8Bytes(this.payload, PerformanceJsonSerializerContext.Default.PerformancePayload);
}
