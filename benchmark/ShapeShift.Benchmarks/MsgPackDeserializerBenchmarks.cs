// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Nerdbank.MessagePack;
using ShapeShiftMsgPackSerializer = ShapeShift.MsgPack.MsgPackSerializer;

namespace ShapeShift.Benchmarks;

/// <summary>
/// Compares MessagePack deserialization on deterministic, source-generated object graphs.
/// </summary>
[MemoryDiagnoser]
public class MsgPackDeserializerBenchmarks
{
	private readonly ShapeShiftMsgPackSerializer shapeShiftSerializer = new();
	private readonly MessagePackSerializer nerdbankSerializer = new();
	private byte[] shapeShiftPayload = null!;
	private byte[] nerdbankPayload = null!;

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
		this.shapeShiftPayload = this.shapeShiftSerializer.Serialize(payload);
		this.nerdbankPayload = this.nerdbankSerializer.Serialize(payload);
		PerformancePayload.AssertEquivalent(payload, this.shapeShiftSerializer.Deserialize<PerformancePayload>(this.shapeShiftPayload));
		PerformancePayload.AssertEquivalent(payload, this.nerdbankSerializer.Deserialize<PerformancePayload>(this.nerdbankPayload));
	}

	/// <summary>
	/// Deserializes MessagePack written by ShapeShift.
	/// </summary>
	/// <returns>The deserialized value.</returns>
	[Benchmark(Baseline = true)]
	public PerformancePayload? ShapeShiftDeserialize() => this.shapeShiftSerializer.Deserialize<PerformancePayload>(this.shapeShiftPayload);

	/// <summary>
	/// Deserializes MessagePack written by Nerdbank.MessagePack.
	/// </summary>
	/// <returns>The deserialized value.</returns>
	[Benchmark]
	public PerformancePayload? NerdbankMessagePackDeserialize() => this.nerdbankSerializer.Deserialize<PerformancePayload>(this.nerdbankPayload);
}
