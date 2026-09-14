// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ShapeShift.MsgPack;

namespace MsgPackReferencePreservation;

internal static partial class MsgPackReferencePreservationSample
{
    internal static void Run()
    {
        #region ReferencePreservation
        // Off (the default) writes a shared object once per reference, so the graph is duplicated
        // on the wire and identity is lost on the way back.
        var plain = new MsgPackSerializer();

        // RejectCycles writes each object once and refers to it afterwards, using the reserved
        // MessagePack reference extension. AllowCycles additionally reconstructs graphs that refer
        // back to themselves, at the cost of accepting cyclic graphs from untrusted senders.
        var preserving = plain with { PreserveReferences = ReferencePreservationMode.RejectCycles };

        var shared = new Author("Ada");
        var library = new Library([new Book("Notes", shared), new Book("Letters", shared)]);

        Library? copy = preserving.Deserialize<Library>(preserving.Serialize(library));
        bool identityPreserved = ReferenceEquals(copy!.Books[0].Author, copy.Books[1].Author);
        #endregion

        #region ReferenceCycles
        var cyclic = plain with { PreserveReferences = ReferencePreservationMode.AllowCycles };
        var manager = new Employee("Grace");
        var report = new Employee("Katherine") { Manager = manager };
        manager.Reports.Add(report);

        Employee? roundTripped = cyclic.Deserialize<Employee>(cyclic.Serialize(manager));
        bool cycleRebuilt = ReferenceEquals(roundTripped, roundTripped!.Reports[0].Manager);
        #endregion

        Console.WriteLine($"identity preserved: {identityPreserved}, cycle rebuilt: {cycleRebuilt}");
        Console.WriteLine($"{preserving.Serialize(library).Length} bytes vs {plain.Serialize(library).Length} without preservation");
    }

    [GenerateShape]
    internal partial record Author(string Name);

    [GenerateShape]
    internal partial record Book(string Title, Author Author);

    [GenerateShape]
    internal partial record Library(List<Book> Books);

    [GenerateShape]
    internal partial class Employee(string name)
    {
        public string Name { get; set; } = name;

        public Employee? Manager { get; set; }

        public List<Employee> Reports { get; } = [];
    }
}
