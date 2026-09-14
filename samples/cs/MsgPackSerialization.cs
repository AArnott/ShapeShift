// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ShapeShift.MsgPack;

namespace MsgPackSerialization;

internal static partial class MsgPackSerializationSample
{
    internal static void Run()
    {
        #region MsgPackSerialization
        var serializer = new MsgPackSerializer();
        var person = new Person("Ada", [1, 2, 3]);

        byte[] messagePack = serializer.Serialize(person);
        Person? copy = serializer.Deserialize<Person>(messagePack);
        #endregion

        Console.WriteLine($"{messagePack.Length} bytes");
        Console.WriteLine(copy);
    }

    [GenerateShape]
    internal partial record Person(string Name, List<int> Values);
}
