// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.MsgPack;

/// <summary>
/// Declares that a type is serialized as a MessagePack array whose elements are identified by position
/// (see <see cref="MsgPackKeyAttribute"/>) instead of as a map keyed by property name.
/// </summary>
/// <remarks>
/// <para>
/// Map contracts are the ShapeShift default because they are the most version tolerant. Positional contracts
/// trade that tolerance for compactness: property names never appear on the wire, so a small object can shrink
/// dramatically. Apply this attribute only when both ends of a payload are versioned together, or when the
/// versioning rules below are followed strictly.
/// </para>
/// <para><strong>Versioning rules.</strong></para>
/// <list type="number">
/// <item>Every serializable member must declare a <see cref="MsgPackKeyAttribute"/>. There is no implicit ordering.</item>
/// <item>A key, once shipped, belongs to that member forever. Retire keys; never reuse or reorder them.</item>
/// <item>New members take keys above every key already in use.</item>
/// <item>
/// A retired key becomes a hole. Holes are written as a MessagePack <c>nil</c> placeholder whenever a later
/// position is still written, and readers skip whatever they find at a position they no longer recognize.
/// </item>
/// <item>
/// A reader accepts an array that is shorter than its own contract (members at the missing positions keep their
/// default values, subject to required-member validation) and an array that is longer (the surplus elements are
/// skipped). That is what makes appending a member backward and forward compatible.
/// </item>
/// </list>
/// <para><strong>Omitted values.</strong> A MessagePack array has no way to say that an interior element is
/// absent as opposed to null, so a positional contract declines
/// <see cref="SerializeDefaultValuesPolicy"/> omission for interior positions: those members are always written,
/// at their real values, even when that value is the default. Omission is honored only for the tail of the array,
/// where a shorter array is an unambiguous statement that the remaining positions were not written. Required
/// members are never elided, because a reader could not reconstruct the object without them.
/// </para>
/// <para><strong>Unsupported combinations.</strong> A positional contract cannot carry an extension-data member
/// (<see cref="ShapeShiftExtensionDataAttribute"/>): unknown positions have no names to retain them under.
/// Applying both raises a <see cref="ShapeShiftSerializationException"/> when the converter is built.
/// </para>
/// </remarks>
/// <example>
/// <code source="../../samples/cs/MsgPackPositionalContracts.cs" region="PositionalContract" lang="C#" />
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class MsgPackArrayContractAttribute : Attribute
{
}
