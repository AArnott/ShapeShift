// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ShapeShift.Equality;

namespace StructuralEqualitySample
{
    internal static class Samples
    {
        #region Basics
        internal static void CompareTwoGraphs()
        {
            IEqualityComparer<Order> comparer = StructuralEqualityComparer.Create<Order>();

            Order left = new("A-1", [new("widget", 2), new("gizmo", 1)], new Dictionary<string, string> { ["gift"] = "yes" });
            Order right = new("A-1", [new("widget", 2), new("gizmo", 1)], new Dictionary<string, string> { ["gift"] = "yes" });

            // True, even though no two objects in the graphs are the same instance,
            // and even though Order does not override Equals.
            Console.WriteLine(comparer.Equals(left, right));
            Console.WriteLine(comparer.GetHashCode(left) == comparer.GetHashCode(right));
        }
        #endregion

        #region HashSet
        internal static void UseAsCollectionComparer()
        {
            HashSet<Order> orders = new(StructuralEqualityComparer.Create<Order>());

            orders.Add(new("A-1", [new("widget", 2)], null));
            orders.Add(new("A-1", [new("widget", 2)], null));

            // 1: the second order is a duplicate by value.
            Console.WriteLine(orders.Count);
        }
        #endregion

        #region Cycles
        internal static void CompareCyclicGraphs()
        {
            IEqualityComparer<Employee> comparer = StructuralEqualityComparer.Create<Employee>();

            Employee soleFounder = new("Ada");
            soleFounder.Manager = soleFounder;

            Employee alice = new("Ada");
            Employee bob = new("Ada");
            alice.Manager = bob;
            bob.Manager = alice;

            // True: both graphs unfold to the same infinite sequence of "Ada"s.
            // Equality describes the value a graph denotes, not the shape of the graph.
            Console.WriteLine(comparer.Equals(soleFounder, alice));
        }
        #endregion

        #region CustomComparer
        internal static void OverrideAMemberComparer()
        {
            IEqualityComparer<Order> caseInsensitive = StructuralEqualityComparerProvider.Default
                .WithComparer(StringComparer.OrdinalIgnoreCase)
                .GetComparer<Order>();

            Order left = new("a-1", [], null);
            Order right = new("A-1", [], null);

            Console.WriteLine(caseInsensitive.Equals(left, right)); // True
            Console.WriteLine(StructuralEqualityComparer.Create<Order>().Equals(left, right)); // False
        }
        #endregion

        #region CollisionResistant
        internal static void HashUntrustedInput()
        {
            // Randomly keyed per process: use it for hash based collections whose keys
            // come from an untrusted source. Never persist these hash codes.
            IEqualityComparer<Order> comparer = StructuralEqualityComparer.CreateCollisionResistant<Order>();

            Dictionary<Order, int> counts = new(comparer);
            counts[new("A-1", [], null)] = 1;

            Console.WriteLine(counts.ContainsKey(new("A-1", [], null))); // True
        }
        #endregion
    }

    #region Model
    [GenerateShape]
    internal partial record Order(string Id, IReadOnlyList<LineItem> Items, IReadOnlyDictionary<string, string>? Metadata);

    internal record LineItem(string Sku, int Quantity);

    [GenerateShape]
    internal partial class Employee(string name)
    {
        public string Name => name;

        public Employee? Manager { get; set; }
    }
    #endregion
}
