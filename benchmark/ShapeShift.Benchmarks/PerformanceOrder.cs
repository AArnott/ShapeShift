// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using PolyType;

namespace ShapeShift.Benchmarks;

/// <summary>
/// One order in <see cref="PerformancePayload"/>.
/// </summary>
[GenerateShape]
public sealed partial record PerformanceOrder(string Id, int Quantity, bool IsPriority, string ProductName, int DiscountCode);
