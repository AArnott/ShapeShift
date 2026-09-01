// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ShapeShift.MsgPack;

namespace MsgPackPositionalContracts;

internal static partial class MsgPackPositionalContractsSample
{
    internal static void Run()
    {
        #region PositionalContract
        var serializer = new MsgPackSerializer();

        // A map contract (the default) writes property names, so it tolerates members being added,
        // removed, and renamed. A positional contract writes only values, located by the permanent
        // positions [MsgPackKey] assigns, which is dramatically more compact for small records.
        byte[] asMap = serializer.Serialize(new MapMeasurement("t1", 21.5, 1013.2));
        byte[] asArray = serializer.Serialize(new Measurement("t1", 21.5, 1013.2));

        Measurement? roundTripped = serializer.Deserialize<Measurement>(asArray);
        #endregion

        #region PositionalVersioning
        // A reader accepts a shorter array (members at the missing positions keep their defaults)
        // and a longer one (surplus positions are skipped), so appending a member at a new position
        // is compatible in both directions. Position 1 was retired and is never reused: writers emit
        // a null placeholder there so every later position stays where it belongs.
        MeasurementV2? upgraded = serializer.Deserialize<MeasurementV2>(asArray);
        byte[] newer = serializer.Serialize(new MeasurementV2("t1", 21.5, 1013.2, "roof"));
        Measurement? downgraded = serializer.Deserialize<Measurement>(newer);
        #endregion

        Console.WriteLine($"{asMap.Length} bytes as a map, {asArray.Length} bytes as an array");
        Console.WriteLine($"{roundTripped} / {upgraded} / {downgraded}");
    }

    [GenerateShape]
    internal partial record MapMeasurement(string SensorId, double Celsius, double Hectopascals);

    [GenerateShape]
    [MsgPackArrayContract]
    internal partial record Measurement(
        [property: MsgPackKey(0)] string SensorId,
        [property: MsgPackKey(1)] double Celsius,
        [property: MsgPackKey(2)] double Hectopascals);

    [GenerateShape]
    [MsgPackArrayContract]
    internal partial record MeasurementV2(
        [property: MsgPackKey(0)] string SensorId,
        [property: MsgPackKey(1)] double Celsius,
        [property: MsgPackKey(2)] double Hectopascals,
        [property: MsgPackKey(3)] string? Location);
}
