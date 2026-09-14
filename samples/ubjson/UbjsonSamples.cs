// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipelines;
using ShapeShift.Conformance;

namespace Ubjson;

/// <summary>
/// Demonstrates using the third-party UBJSON format package this project defines.
/// </summary>
public static class UbjsonSamples
{
    #region Roundtrip
    /// <summary>
    /// Serializes and deserializes an ordinary shape-generated type through the new format.
    /// </summary>
    /// <returns>The value that survived the round trip.</returns>
    public static Measurement Roundtrip()
    {
        UbjsonSerializer serializer = new();

        Measurement original = new("cabin-pressure", 101.325m, [1, 2, 3], new byte[] { 0xDE, 0xAD });
        byte[] payload = serializer.Serialize(original);

        return serializer.Deserialize<Measurement>(payload)!;
    }
    #endregion

    #region Configuration
    /// <summary>
    /// Shows that a third-party format inherits the whole shared feature set, configured the same way.
    /// </summary>
    /// <returns>The payload written with camel-cased property names.</returns>
    public static byte[] SerializeWithSharedPolicies()
    {
        UbjsonSerializer serializer = new()
        {
            PropertyNamingPolicy = ShapeShiftNamingPolicy.CamelCase,
            SerializeDefaultValues = SerializeDefaultValuesPolicy.Required,
        };

        return serializer.Serialize(new Measurement("altitude", 10_668m, [], null));
    }
    #endregion

    #region Streaming
    /// <summary>
    /// Reads a stream of concatenated top-level UBJSON values without buffering more than one at a time.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The names of the values read.</returns>
    public static async Task<IReadOnlyList<string>> ReadAllAsync(CancellationToken cancellationToken)
    {
        UbjsonSerializer serializer = new();
        byte[] stream =
        [
            .. serializer.Serialize(new Measurement("first", 1m, [], null)),
            .. serializer.Serialize(new Measurement("second", 2m, [], null)),
        ];

        List<string> names = [];
        PipeReader reader = PipeReader.Create(new ReadOnlySequence<byte>(stream));
        await foreach (Measurement? measurement in serializer.DeserializeAllAsync<Measurement>(reader, cancellationToken: cancellationToken))
        {
            names.Add(measurement!.Name);
        }

        return names;
    }
    #endregion

    #region Conformance
    /// <summary>
    /// Runs the shared conformance kit against the new format.
    /// </summary>
    /// <returns>The report, whose <see cref="ConformanceReport.IsConformant"/> is the pass/fail signal.</returns>
    public static ConformanceReport RunConformanceSuite()
        => ConformanceSuite.Run(new UbjsonConformanceAdapter());
    #endregion
}

#region Model
/// <summary>
/// A model type that exercises strings, exact decimals, collections, and binary data.
/// </summary>
/// <param name="Name">The name of the measurement.</param>
/// <param name="Value">The measured value.</param>
/// <param name="Samples">The raw samples the value was derived from.</param>
/// <param name="Signature">An optional signature over the samples.</param>
[GenerateShape]
public partial record Measurement(string Name, decimal Value, int[] Samples, byte[]? Signature);
#endregion
