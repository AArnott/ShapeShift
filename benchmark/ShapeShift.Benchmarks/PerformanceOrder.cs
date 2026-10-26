// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using PolyType;

namespace ShapeShift.Benchmarks;

/// <summary>
/// One order in <see cref="PerformancePayload"/>.
/// </summary>
/// <param name="Id">The order identifier.</param>
/// <param name="Quantity">The number of items ordered.</param>
/// <param name="IsPriority">Whether the order has priority handling.</param>
/// <param name="ProductName">The name of the ordered product.</param>
/// <param name="DiscountCode">The numeric discount code.</param>
[GenerateShape]
public sealed partial record PerformanceOrder(string Id, int Quantity, bool IsPriority, string ProductName, int DiscountCode);
