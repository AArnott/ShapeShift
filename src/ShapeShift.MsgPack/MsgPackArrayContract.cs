// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // File may only contain a single type

namespace ShapeShift.MsgPack;

/// <summary>
/// Writes one member of a positional (array) contract.
/// </summary>
/// <typeparam name="TDeclaringType">The type that declares the member.</typeparam>
/// <param name="encoder">The encoder.</param>
/// <param name="value">The object whose member is written.</param>
/// <param name="context">The serialization context.</param>
internal delegate void WriteArrayElement<TDeclaringType>(ref MsgPackEncoder encoder, in TDeclaringType value, SerializationContext<MsgPackEncoder, MsgPackDecoder> context);

/// <summary>
/// Reads one member of a positional (array) contract into either the object being built or the argument state
/// of the constructor that will build it.
/// </summary>
/// <typeparam name="TState">The object or argument state being populated.</typeparam>
/// <param name="decoder">The decoder.</param>
/// <param name="state">The object or argument state being populated.</param>
/// <param name="context">The serialization context.</param>
internal delegate void ReadArrayElement<TState>(ref MsgPackDecoder decoder, ref TState state, SerializationContext<MsgPackEncoder, MsgPackDecoder> context);

/// <summary>
/// Decides whether a member of a positional (array) contract holds anything but its default value.
/// </summary>
/// <typeparam name="TDeclaringType">The type that declares the member.</typeparam>
/// <param name="value">The object whose member is tested.</param>
/// <returns><see langword="true" /> when the member must be written.</returns>
internal delegate bool ShouldWriteArrayElement<TDeclaringType>(in TDeclaringType value);

/// <summary>
/// The write half of one position in a positional contract.
/// </summary>
/// <typeparam name="TDeclaringType">The type that declares the member.</typeparam>
/// <param name="Name">The member's declared name, used only in diagnostics and contracts.</param>
/// <param name="Write">Writes the member's value.</param>
/// <param name="ShouldWrite">
/// Reports whether the member differs from its default value, or <see langword="null" /> when the member is
/// always written.
/// </param>
internal sealed record MsgPackArrayWriteSlot<TDeclaringType>(
	string Name,
	WriteArrayElement<TDeclaringType> Write,
	ShouldWriteArrayElement<TDeclaringType>? ShouldWrite);

/// <summary>
/// The read half of one position in a positional contract.
/// </summary>
/// <typeparam name="TState">The object or argument state being populated.</typeparam>
/// <param name="Name">The member's declared name, used only in diagnostics.</param>
/// <param name="Read">Reads the member's value.</param>
internal sealed record MsgPackArrayReadSlot<TState>(string Name, ReadArrayElement<TState> Read);

/// <summary>
/// Everything a positional contract needs in order to describe one of its positions as schema.
/// </summary>
/// <param name="Position">The 0-based position within the array.</param>
/// <param name="Name">The member's declared name.</param>
/// <param name="Type">The shape of the member's type.</param>
internal sealed record MsgPackArraySlotDescription(int Position, string Name, ITypeShape Type)
{
	/// <inheritdoc cref="Schema.PropertyContract.IsRequired"/>
	internal bool IsRequired { get; init; }

	/// <inheritdoc cref="Schema.PropertyContract.IsNullable"/>
	internal bool IsNullable { get; init; } = true;

	/// <inheritdoc cref="Schema.PropertyContract.IsReadable"/>
	internal bool IsReadable { get; init; } = true;

	/// <inheritdoc cref="Schema.PropertyContract.IsWritable"/>
	internal bool IsWritable { get; init; } = true;

	/// <inheritdoc cref="Schema.PropertyContract.IsAlwaysWritten"/>
	internal bool IsAlwaysWritten { get; init; } = true;
}

/// <summary>
/// A member of a positional contract, as produced while visiting a property or constructor parameter.
/// </summary>
/// <typeparam name="TDeclaringType">The type that declares the member.</typeparam>
/// <typeparam name="TReadState">The object or argument state a read populates.</typeparam>
internal sealed class MsgPackArrayMember<TDeclaringType, TReadState>
{
	/// <summary>
	/// Gets the delegate that writes this member, or <see langword="null" /> when it cannot be read from the object.
	/// </summary>
	internal required WriteArrayElement<TDeclaringType>? Write { get; init; }

	/// <summary>
	/// Gets the delegate that reads this member, or <see langword="null" /> when it cannot be assigned.
	/// </summary>
	internal required ReadArrayElement<TReadState>? Read { get; init; }

	/// <summary>
	/// Gets the delegate that reports whether this member differs from its default value.
	/// </summary>
	internal ShouldWriteArrayElement<TDeclaringType>? ShouldWrite { get; init; }

	/// <summary>
	/// Gets a description of this member for schema purposes.
	/// </summary>
	internal required MsgPackArraySlotDescription Description { get; init; }
}
