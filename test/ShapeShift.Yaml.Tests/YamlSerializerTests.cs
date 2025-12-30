// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ShapeShift.Tests;

namespace ShapeShift.Yaml.Tests;

public partial class YamlSerializerTests : TestBase
{
    [Test]
    public async Task SimpleString()
    {
        YamlSerializer serializer = new();
        string original = "Hello, World!";
        string yaml = serializer.Serialize<string, Witness>(original);
        string? deserialized = serializer.Deserialize<string, Witness>(yaml);

        await Assert.That(deserialized).IsEqualTo(original);

        // In the case of a simple string, the yaml representation is equal to the string itself.
        await Assert.That(yaml).IsEqualTo(original);
    }

    [GenerateShapeFor<string>]
    private partial class Witness;
}
