// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.MsgPack;

/// <summary>
/// Assigns the stable position a member occupies in a type that declares
/// <see cref="MsgPackArrayContractAttribute"/>.
/// </summary>
/// <param name="index">
/// The 0-based position of this member within the MessagePack array. It must be unique within the declaring type
/// and no greater than <see cref="MaxIndex"/>.
/// </param>
/// <remarks>
/// <para>
/// The attribute may be applied to a property or field, or to a constructor parameter (which is the natural place
/// for it on a positional record). When both a parameter and its matching property carry one, they must agree.
/// </para>
/// <para>
/// This attribute has no effect on a type that does not declare <see cref="MsgPackArrayContractAttribute"/>,
/// and no effect on any format other than MessagePack.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class MsgPackKeyAttribute(int index) : Attribute
{
	/// <summary>
	/// The largest position a positional contract may assign.
	/// </summary>
	/// <remarks>
	/// Positions are array indexes, and every position below the highest one in use costs at least one byte on the
	/// wire even when nothing occupies it. This bound keeps a typo from turning a small object into an enormous array.
	/// </remarks>
	public const int MaxIndex = 1023;

	/// <summary>
	/// Gets the 0-based position of this member within the MessagePack array.
	/// </summary>
	public int Index { get; } = index;
}
