// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Schema;

/// <summary>
/// Describes an enum type.
/// </summary>
/// <remarks>
/// Deserialization always accepts <em>either</em> the name of a member (case-insensitively)
/// or its underlying numeric value, regardless of <see cref="IsSerializedByName"/>.
/// </remarks>
/// <param name="dataType"><inheritdoc cref="DataContract(Type)" path="/param[@name='dataType']"/></param>
/// <param name="underlyingType">The contract for the enum's underlying integral type.</param>
/// <param name="members">The declared members of the enum.</param>
public sealed class EnumContract(Type dataType, DataContract underlyingType, IEnumerable<EnumMemberContract> members)
	: DataContract(dataType)
{
	/// <summary>
	/// Gets the contract for the enum's underlying integral type.
	/// </summary>
	public DataContract UnderlyingType { get; } = Requires.NotNull(underlyingType, nameof(underlyingType));

	/// <summary>
	/// Gets the declared members of the enum.
	/// </summary>
	public ImmutableArray<EnumMemberContract> Members { get; } = [.. Requires.NotNull(members, nameof(members))];

	/// <summary>
	/// Gets a value indicating whether values are written using their member name when a name is available.
	/// </summary>
	/// <value>The default value is <see langword="true" />.</value>
	/// <remarks>
	/// When <see langword="false" />, or when the value has no matching declared member
	/// (as is common for flags enums), the underlying numeric value is written instead.
	/// </remarks>
	public bool IsSerializedByName { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether the enum is decorated with <see cref="FlagsAttribute"/>.
	/// </summary>
	public bool IsFlags { get; init; }

	/// <inheritdoc/>
	public override DataContractKind Kind => DataContractKind.Enum;

	/// <inheritdoc/>
	public override IEnumerable<DataContract> ReferencedContracts => [this.UnderlyingType];
}
