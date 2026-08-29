// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;

#pragma warning disable SA1402 // The value hierarchy is kept together as one cohesive public model.

namespace ShapeShift;

/// <summary>
/// Represents a value in the format-neutral ShapeShift data model.
/// </summary>
[GenerateShape]
public abstract partial record ShapeShiftValue
{
	/// <summary>
	/// Gets the singleton null value.
	/// </summary>
	public static ShapeShiftValue Null { get; } = new ShapeShiftNull();

	/// <summary>
	/// Creates a Boolean value.
	/// </summary>
	/// <param name="value">The represented value.</param>
	/// <returns>The dynamic value.</returns>
	public static implicit operator ShapeShiftValue(bool value) => new ShapeShiftBoolean(value);

	/// <summary>
	/// Creates a signed integer value.
	/// </summary>
	/// <param name="value">The represented value.</param>
	/// <returns>The dynamic value.</returns>
	public static implicit operator ShapeShiftValue(long value) => new ShapeShiftInteger(value);

	/// <summary>
	/// Creates an unsigned integer value.
	/// </summary>
	/// <param name="value">The represented value.</param>
	/// <returns>The dynamic value.</returns>
	public static implicit operator ShapeShiftValue(ulong value) => new ShapeShiftUnsignedInteger(value);

	/// <summary>
	/// Creates a floating-point value.
	/// </summary>
	/// <param name="value">The represented value.</param>
	/// <returns>The dynamic value.</returns>
	public static implicit operator ShapeShiftValue(double value) => new ShapeShiftFloat(value);

	/// <summary>
	/// Creates a decimal value.
	/// </summary>
	/// <param name="value">The represented value.</param>
	/// <returns>The dynamic value.</returns>
	public static implicit operator ShapeShiftValue(decimal value) => new ShapeShiftDecimal(value);

	/// <summary>
	/// Creates a string value.
	/// </summary>
	/// <param name="value">The represented value.</param>
	/// <returns>The dynamic value.</returns>
	public static implicit operator ShapeShiftValue(string value) => new ShapeShiftString(value);

	/// <summary>
	/// Creates a binary value.
	/// </summary>
	/// <param name="value">The represented bytes.</param>
	/// <returns>The dynamic value.</returns>
	public static implicit operator ShapeShiftValue(byte[] value) => new ShapeShiftBinary(value);
}

/// <summary>
/// Represents a null value.
/// </summary>
public sealed record ShapeShiftNull : ShapeShiftValue;

/// <summary>
/// Represents a Boolean value.
/// </summary>
/// <param name="Value">The represented value.</param>
public sealed record ShapeShiftBoolean(bool Value) : ShapeShiftValue;

/// <summary>
/// Represents a signed integer value.
/// </summary>
/// <param name="Value">The represented value.</param>
public sealed record ShapeShiftInteger(long Value) : ShapeShiftNumber;

/// <summary>
/// Represents an unsigned integer value.
/// </summary>
/// <param name="Value">The represented value.</param>
public sealed record ShapeShiftUnsignedInteger(ulong Value) : ShapeShiftNumber;

/// <summary>
/// Represents an arbitrary-precision integer value.
/// </summary>
/// <param name="Value">The represented value.</param>
public sealed record ShapeShiftBigInteger(BigInteger Value) : ShapeShiftNumber;

/// <summary>
/// Represents a floating-point value.
/// </summary>
/// <param name="Value">The represented value.</param>
public sealed record ShapeShiftFloat(double Value) : ShapeShiftNumber;

/// <summary>
/// Represents a decimal value.
/// </summary>
/// <param name="Value">The represented value.</param>
public sealed record ShapeShiftDecimal(decimal Value) : ShapeShiftNumber;

/// <summary>
/// Represents any dynamic numeric value.
/// </summary>
public abstract record ShapeShiftNumber : ShapeShiftValue;

/// <summary>
/// Represents a string value.
/// </summary>
/// <param name="Value">The represented value.</param>
public sealed record ShapeShiftString(string Value) : ShapeShiftValue;

/// <summary>
/// Represents a binary value.
/// </summary>
public sealed record ShapeShiftBinary : ShapeShiftValue
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ShapeShiftBinary"/> class.
	/// </summary>
	/// <param name="value">The bytes to copy into the immutable value.</param>
	public ShapeShiftBinary(ReadOnlySpan<byte> value)
	{
		this.Value = value.ToArray();
	}

	/// <summary>
	/// Gets the represented bytes.
	/// </summary>
	public ReadOnlyMemory<byte> Value { get; }
}

/// <summary>
/// Represents a sequence of dynamic values.
/// </summary>
/// <param name="Items">The sequence items.</param>
public sealed record ShapeShiftArray(IReadOnlyList<ShapeShiftValue> Items) : ShapeShiftValue;

/// <summary>
/// Represents a map whose keys are strings.
/// </summary>
/// <param name="Properties">The map properties.</param>
public sealed record ShapeShiftMap(IReadOnlyDictionary<string, ShapeShiftValue> Properties) : ShapeShiftValue;
