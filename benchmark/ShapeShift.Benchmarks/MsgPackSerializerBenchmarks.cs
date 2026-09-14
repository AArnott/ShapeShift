// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Nerdbank.MessagePack;
using ShapeShiftMsgPackSerializer = ShapeShift.MsgPack.MsgPackSerializer;

namespace ShapeShift.Benchmarks;

/// <summary>
/// Compares MessagePack serialization on deterministic, source-generated object graphs.
/// </summary>
[MemoryDiagnoser]
public class MsgPackSerializerBenchmarks
{
	private readonly ShapeShiftMsgPackSerializer shapeShiftSerializer = new();
	private readonly MessagePackSerializer nerdbankSerializer = new();
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
		byte[] shapeShiftPayload = this.shapeShiftSerializer.Serialize(this.payload);
		byte[] nerdbankPayload = this.nerdbankSerializer.Serialize(this.payload);
		PerformancePayload.AssertEquivalent(this.payload, this.shapeShiftSerializer.Deserialize<PerformancePayload>(shapeShiftPayload));
		PerformancePayload.AssertEquivalent(this.payload, this.nerdbankSerializer.Deserialize<PerformancePayload>(nerdbankPayload));
	}

	/// <summary>
	/// Serializes using ShapeShift's MessagePack API.
	/// </summary>
	/// <returns>The MessagePack document.</returns>
	[Benchmark(Baseline = true)]
	public byte[] ShapeShiftSerialize() => this.shapeShiftSerializer.Serialize(this.payload);

	/// <summary>
	/// Serializes using Nerdbank.MessagePack.
	/// </summary>
	/// <returns>The MessagePack document.</returns>
	[Benchmark]
	public byte[] NerdbankMessagePackSerialize() => this.nerdbankSerializer.Serialize(this.payload);
}
