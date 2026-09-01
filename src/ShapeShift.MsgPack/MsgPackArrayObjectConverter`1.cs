// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // File may only contain a single type

using ShapeShift.Schema;

namespace ShapeShift.MsgPack;

/// <summary>
/// Serializes an object as a MessagePack array whose elements are identified by the positions that
/// <see cref="MsgPackKeyAttribute"/> assigns.
/// </summary>
/// <typeparam name="T">The type being converted.</typeparam>
internal abstract class MsgPackArrayObjectConverter<T> : ShapeShiftConverter<T, MsgPackEncoder, MsgPackDecoder>
{
	/// <summary>
	/// Guards against a self-referencing positional contract recursing forever while describing itself.
	/// </summary>
	private bool describing;

	/// <summary>
	/// Gets the writers for each position, indexed by position. A <see langword="null" /> entry is a position that
	/// no readable member occupies, and is written as a <c>nil</c> placeholder.
	/// </summary>
	internal required ImmutableArray<MsgPackArrayWriteSlot<T>?> WriteSlots { get; init; }

	/// <summary>
	/// Gets the description of each declared position, indexed by position.
	/// </summary>
	internal required ImmutableArray<MsgPackArraySlotDescription?> Descriptions { get; init; }

	/// <inheritdoc/>
	public override void Write(ref MsgPackEncoder encoder, in T? value, SerializationContext<MsgPackEncoder, MsgPackDecoder> context)
	{
		if (value is null)
		{
			encoder.WriteNull();
			return;
		}

		var callbacks = value as IShapeShiftSerializationCallbacks;
		callbacks?.OnBeforeSerialize();

		context.DepthStep();

		// Only the tail of the array may be elided: a shorter array unambiguously says "nothing was written from
		// here on", whereas an interior element has no way to distinguish "absent" from "null". Interior members
		// are therefore always written at their real values even when default-value omission is enabled.
		int count = this.WriteSlots.Length;
		while (count > 0 && (this.WriteSlots[count - 1] is null || (this.WriteSlots[count - 1] is { ShouldWrite: { } shouldWrite } && !shouldWrite(value))))
		{
			count--;
		}

		encoder.WriteArrayHeader(count);
		for (int i = 0; i < count; i++)
		{
			if (this.WriteSlots[i] is not { } slot)
			{
				// A retired or write-only position: a placeholder keeps every later position where it belongs.
				encoder.WriteNull();
				continue;
			}

			try
			{
				slot.Write(ref encoder, in value, context);
			}
			catch (ShapeShiftSerializationException ex) when (ex.AddEnclosingPathElement(i))
			{
				throw;
			}
			catch (Exception ex) when (MsgPackErrors.IsAugmentable(ex))
			{
				throw new ShapeShiftSerializationException($"Failed to serialize position {i} ('{slot.Name}') of {typeof(T).FullName}.", ex, new ShapeShiftPath(i));
			}
		}

		encoder.WriteEndVector();
		callbacks?.OnAfterSerialize();
	}

	/// <inheritdoc/>
	public override DataContract? GetContract(ContractContext<MsgPackEncoder, MsgPackDecoder> context)
	{
		ArgumentNullException.ThrowIfNull(context);
		if (this.describing)
		{
			return new UndocumentedContract(typeof(T), "A positional MessagePack contract that refers to itself cannot be expanded into a finite schema.");
		}

		this.describing = true;
		try
		{
			List<PropertyContract> properties = new(this.Descriptions.Length);
			foreach (MsgPackArraySlotDescription? description in this.Descriptions)
			{
				if (description is null)
				{
					continue;
				}

				properties.Add(new PropertyContract(description.Name, context.GetContract(description.Type))
				{
					DeclaredName = description.Name,
					Position = description.Position,
					IsRequired = description.IsRequired,
					IsNullable = description.IsNullable,
					IsReadable = description.IsReadable,
					IsWritable = description.IsWritable,
					IsAlwaysWritten = description.IsAlwaysWritten,
				});
			}

			return new ObjectContract(typeof(T), properties) { Encoding = ObjectEncoding.Positional };
		}
		finally
		{
			this.describing = false;
		}
	}

	/// <summary>
	/// Reads the array header, rejecting anything that is not a MessagePack array.
	/// </summary>
	/// <param name="decoder">The decoder.</param>
	/// <returns>The number of elements the array declares.</returns>
	private protected static int ReadArrayHeader(ref MsgPackDecoder decoder)
		=> decoder.ReadStartVector() ?? throw new DecoderException($"A positional MessagePack contract requires an array of a known length.");

	/// <summary>
	/// Reads element <paramref name="position"/>, dispatching it to the member that occupies that position or
	/// skipping it when no member does.
	/// </summary>
	/// <typeparam name="TState">The object or argument state being populated.</typeparam>
	/// <param name="decoder">The decoder, positioned at the element to read.</param>
	/// <param name="state">The destination that receives the member's value.</param>
	/// <param name="readSlots">The readers for each position, indexed by position.</param>
	/// <param name="position">The 0-based position of the element being read.</param>
	/// <param name="context">The serialization context.</param>
	private protected void ReadElement<TState>(ref MsgPackDecoder decoder, ref TState state, ImmutableArray<MsgPackArrayReadSlot<TState>?> readSlots, int position, SerializationContext<MsgPackEncoder, MsgPackDecoder> context)
	{
		if (position >= readSlots.Length || readSlots[position] is not { } slot)
		{
			// A position this contract does not know: either a retired one, or one a newer writer appended.
			decoder.Skip();
			return;
		}

		try
		{
			slot.Read(ref decoder, ref state, context);
		}
		catch (ShapeShiftSerializationException ex) when (ex.AddEnclosingPathElement(position))
		{
			throw;
		}
		catch (Exception ex) when (MsgPackErrors.IsAugmentable(ex))
		{
			throw new ShapeShiftSerializationException($"Failed to deserialize position {position} ('{slot.Name}') of {typeof(T).FullName}.", ex, new ShapeShiftPath(position));
		}
	}
}

/// <summary>
/// A positional converter for a type that ShapeShift constructs with a parameterless constructor and then populates.
/// </summary>
/// <typeparam name="T">The type being converted.</typeparam>
/// <param name="constructor">Creates an empty instance.</param>
internal sealed class MsgPackArrayObjectConverterWithDefaultCtor<T>(Func<T> constructor) : MsgPackArrayObjectConverter<T>
{
	/// <summary>
	/// Gets the readers for each position, indexed by position.
	/// </summary>
	internal required ImmutableArray<MsgPackArrayReadSlot<T>?> ReadSlots { get; init; }

	/// <inheritdoc/>
	public override T? Read(ref MsgPackDecoder decoder, SerializationContext<MsgPackEncoder, MsgPackDecoder> context)
	{
		if (decoder.TryReadNull())
		{
			return default;
		}

		context.DepthStep();
		T value = constructor();
		var callbacks = value as IShapeShiftSerializationCallbacks;
		callbacks?.OnBeforeDeserialize();

		if (!typeof(T).IsValueType)
		{
			context.ReportObjectConstructed(value);
		}

		int count = ReadArrayHeader(ref decoder);
		for (int i = 0; i < count; i++)
		{
			this.ReadElement(ref decoder, ref value, this.ReadSlots, i, context);
		}

		decoder.ReadEndVector();
		callbacks?.OnAfterDeserialize();
		return value;
	}
}

/// <summary>
/// A positional converter for a type whose values are produced by a parameterized constructor.
/// </summary>
/// <typeparam name="T">The type being converted.</typeparam>
/// <typeparam name="TArgumentState">The state that accumulates constructor arguments.</typeparam>
/// <param name="argumentStateConstructor">Creates the argument state.</param>
/// <param name="constructor">Creates the value from the accumulated arguments.</param>
internal sealed class MsgPackArrayObjectConverterWithCtor<T, TArgumentState>(Func<TArgumentState> argumentStateConstructor, Constructor<TArgumentState, T> constructor) : MsgPackArrayObjectConverter<T>
	where TArgumentState : IArgumentState
{
	/// <summary>
	/// Gets the readers for each position, indexed by position.
	/// </summary>
	internal required ImmutableArray<MsgPackArrayReadSlot<TArgumentState>?> ReadSlots { get; init; }

	/// <summary>
	/// Gets the constructor parameters, used to name whichever required ones a payload failed to supply.
	/// </summary>
	internal required IReadOnlyList<IParameterShape> Parameters { get; init; }

	/// <summary>
	/// Gets the policy that decides whether a payload may omit a required member.
	/// </summary>
	internal required DeserializeDefaultValuesPolicy DefaultValuesPolicy { get; init; }

	/// <inheritdoc/>
	public override T? Read(ref MsgPackDecoder decoder, SerializationContext<MsgPackEncoder, MsgPackDecoder> context)
	{
		if (decoder.TryReadNull())
		{
			return default;
		}

		context.DepthStep();
		TArgumentState argumentState = argumentStateConstructor();

		int count = ReadArrayHeader(ref decoder);
		for (int i = 0; i < count; i++)
		{
			this.ReadElement(ref decoder, ref argumentState, this.ReadSlots, i, context);
		}

		decoder.ReadEndVector();

		if ((this.DefaultValuesPolicy & DeserializeDefaultValuesPolicy.AllowMissingValuesForRequiredProperties) == 0 && !argumentState.AreRequiredArgumentsSet)
		{
			List<string> missing = [];
			foreach (IParameterShape parameter in this.Parameters)
			{
				if (parameter.IsRequired && !argumentState.IsArgumentSet(parameter.Position))
				{
					missing.Add(parameter.Name);
				}
			}

			throw new ShapeShiftSerializationException($"Missing required properties: {string.Join(", ", missing)}.");
		}

		T value = constructor(ref argumentState);
		(value as IShapeShiftSerializationCallbacks)?.OnAfterDeserialize();
		return value;
	}
}
