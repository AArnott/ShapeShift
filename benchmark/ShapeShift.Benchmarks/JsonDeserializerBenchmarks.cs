// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using ShapeShiftJsonSerializer = ShapeShift.Json.JsonSerializer;
using SystemTextJsonSerializer = System.Text.Json.JsonSerializer;

namespace ShapeShift.Benchmarks;

/// <summary>
/// Compares JSON deserialization on deterministic, source-generated object graphs.
/// </summary>
[MemoryDiagnoser]
public class JsonDeserializerBenchmarks
{
	private readonly ShapeShiftJsonSerializer shapeShiftSerializer = new();
	private byte[] shapeShiftPayload = null!;
	private byte[] systemTextJsonPayload = null!;

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
		PerformancePayload payload = PerformancePayload.Create(this.OrderCount);
		this.shapeShiftPayload = this.shapeShiftSerializer.SerializeToUtf8Bytes(payload);
		this.systemTextJsonPayload = SystemTextJsonSerializer.SerializeToUtf8Bytes(payload, PerformanceJsonSerializerContext.Default.PerformancePayload);
		PerformancePayload.AssertEquivalent(payload, this.shapeShiftSerializer.Deserialize<PerformancePayload>(this.shapeShiftPayload));
		PerformancePayload.AssertEquivalent(payload, SystemTextJsonSerializer.Deserialize(this.systemTextJsonPayload, PerformanceJsonSerializerContext.Default.PerformancePayload));
	}

	/// <summary>
	/// Deserializes JSON written by ShapeShift.
	/// </summary>
	/// <returns>The deserialized value.</returns>
	[Benchmark(Baseline = true)]
	public PerformancePayload? ShapeShiftDeserialize() => this.shapeShiftSerializer.Deserialize<PerformancePayload>(this.shapeShiftPayload);

	/// <summary>
	/// Deserializes JSON written by System.Text.Json.
	/// </summary>
	/// <returns>The deserialized value.</returns>
	[Benchmark]
	public PerformancePayload? SystemTextJsonDeserialize() => SystemTextJsonSerializer.Deserialize(this.systemTextJsonPayload, PerformanceJsonSerializerContext.Default.PerformancePayload);
}
