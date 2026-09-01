// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ShapeShift.Json;

namespace UnknownDataRetention;

internal static partial class UnknownDataRetentionSample
{
    internal static void Run()
    {
        #region UnknownDataRetention
        var serializer = new JsonSerializer();
        const string Payload = """{"Name":"Ada","future":{"enabled":true}}""";

        ExtensiblePerson? person = serializer.Deserialize<ExtensiblePerson>(Payload);
        string forwardedPayload = serializer.Serialize(person);
        #endregion

        Console.WriteLine(forwardedPayload);
    }

    [GenerateShape]
    internal partial class ExtensiblePerson
    {
        public string? Name { get; set; }

        [ShapeShiftExtensionData]
        public Dictionary<string, ShapeShiftValue> ExtensionData { get; } = new(StringComparer.Ordinal);
    }
}
