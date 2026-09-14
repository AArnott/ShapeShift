// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace ShapeShift.Benchmarks;

/// <summary>
/// Provides source-generated System.Text.Json metadata for the benchmark payload.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(PerformancePayload))]
internal partial class PerformanceJsonSerializerContext : JsonSerializerContext;
