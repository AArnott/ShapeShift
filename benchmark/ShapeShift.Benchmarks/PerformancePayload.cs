// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;
using PolyType;

namespace ShapeShift.Benchmarks;

/// <summary>
/// A deterministic, representative order payload shared by all serializer benchmarks.
/// </summary>
[GenerateShape]
public sealed partial record PerformancePayload(string CustomerId, string CustomerName, List<PerformanceOrder> Orders, Dictionary<string, string> Metadata)
{
	/// <summary>
	/// Creates a deterministic payload with the requested number of orders.
	/// </summary>
	/// <param name="orderCount">The number of orders to add.</param>
	/// <returns>A populated benchmark payload.</returns>
	public static PerformancePayload Create(int orderCount)
	{
		List<PerformanceOrder> orders = new(orderCount);
		for (int i = 0; i < orderCount; i++)
		{
			orders.Add(new($"order-{i:D5}", (i * 17) + 3, i % 2 == 0, $"Product {i % 23}", i % 7));
		}

		return new(
			"customer-0042",
			"Ada Lovelace",
			orders,
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["region"] = "west",
				["channel"] = "online",
				["campaign"] = "autumn",
			});
	}

	/// <summary>
	/// Asserts equivalence without including benchmark validation in the measured operation.
	/// </summary>
	/// <param name="expected">The expected payload.</param>
	/// <param name="actual">The payload to validate.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="expected"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">Thrown when the payloads differ.</exception>
	public static void AssertEquivalent(PerformancePayload expected, PerformancePayload? actual)
	{
		ArgumentNullException.ThrowIfNull(expected);
		if (actual is null
			|| expected.CustomerId != actual.CustomerId
			|| expected.CustomerName != actual.CustomerName
			|| expected.Orders.Count != actual.Orders.Count
			|| expected.Metadata.Count != actual.Metadata.Count)
		{
			throw new InvalidOperationException("The deserialized payload did not match the source payload.");
		}

		for (int i = 0; i < expected.Orders.Count; i++)
		{
			if (expected.Orders[i] != actual.Orders[i])
			{
				throw new InvalidOperationException("A deserialized order did not match the source order.");
			}
		}

		foreach ((string key, string value) in expected.Metadata)
		{
			if (!actual.Metadata.TryGetValue(key, out string? actualValue) || value != actualValue)
			{
				throw new InvalidOperationException("Deserialized metadata did not match the source metadata.");
			}
		}
	}
}
