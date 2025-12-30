// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using ShapeShift.Tests;

namespace ShapeShift.Yaml.Tests;

public partial class YamlSerializerTests : TestBase
{
    private string? lastSerializedYaml;

    [Test]
    public async Task SimpleString()
    {
        string original = "Hello, World!";
        await this.AssertRoundtripAsync<string, Witness>(original);

        // In the case of a simple string, the yaml representation is equal to the string itself.
        await Assert.That(this.lastSerializedYaml).IsEqualTo(original);
    }

    [Test]
    public async Task SimpleInt32()
    {
        int original = 42;
        await this.AssertRoundtripAsync<int, Witness>(original);

        // In the case of an integer, the yaml representation is equal the a simple ToString on the integer.
        await Assert.That(this.lastSerializedYaml?.Trim()).IsEqualTo(original.ToString(CultureInfo.InvariantCulture));
    }

    protected async ValueTask<T?> AssertRoundtripAsync<T, TProvider>(T? value)
        where TProvider : IShapeable<T>
    {
        YamlSerializer serializer = new();

        this.lastSerializedYaml = serializer.Serialize<T, TProvider>(value);

        Console.WriteLine("Serialized form:");
        Console.WriteLine(this.lastSerializedYaml);
        Console.WriteLine();

        T? deserialized = serializer.Deserialize<T, TProvider>(this.lastSerializedYaml);

        await Assert.That(deserialized).IsEqualTo(value);
        return deserialized;
    }

    [GenerateShapeFor<string>]
    [GenerateShapeFor<int>]
    private partial class Witness;
}
