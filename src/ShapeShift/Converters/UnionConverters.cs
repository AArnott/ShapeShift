// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

namespace ShapeShift.Converters;

/// <summary>
/// Converts a union case under its declared union type.
/// </summary>
/// <typeparam name="TUnionCase">The derived case type.</typeparam>
/// <typeparam name="TUnion">The declared union type.</typeparam>
/// <typeparam name="TEncoder">The encoder type.</typeparam>
/// <typeparam name="TDecoder">The decoder type.</typeparam>
internal sealed class UnionCaseConverter<TUnionCase, TUnion, TEncoder, TDecoder>(
	ShapeShiftConverter<TUnionCase, TEncoder, TDecoder> inner,
	IMarshaler<TUnionCase, TUnion> marshaler) : ShapeShiftConverter<TUnion, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public override TUnion? Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context)
		=> marshaler.Marshal(inner.Read(ref decoder, context));

	/// <inheritdoc/>
	public override void Write(ref TEncoder encoder, in TUnion? value, SerializationContext<TEncoder, TDecoder> context)
		=> inner.Write(ref encoder, marshaler.Unmarshal(value), context);
}

/// <summary>
/// Describes one union case and its wire discriminator.
/// </summary>
/// <typeparam name="TUnion">The declared union type.</typeparam>
/// <typeparam name="TEncoder">The encoder type.</typeparam>
/// <typeparam name="TDecoder">The decoder type.</typeparam>
internal sealed record UnionCase<TUnion, TEncoder, TDecoder>(
	string Name,
	int Tag,
	bool UseTag,
	ShapeShiftConverter<TUnion, TEncoder, TDecoder> Converter)
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct;

/// <summary>
/// Converts polymorphic values as a two-element discriminator/value vector.
/// </summary>
/// <typeparam name="TUnion">The declared union type.</typeparam>
/// <typeparam name="TEncoder">The encoder type.</typeparam>
/// <typeparam name="TDecoder">The decoder type.</typeparam>
internal sealed class UnionConverter<TUnion, TEncoder, TDecoder>(
	ShapeShiftConverter<TUnion, TEncoder, TDecoder> baseConverter,
	Getter<TUnion, int> getUnionCaseIndex,
	IReadOnlyList<UnionCase<TUnion, TEncoder, TDecoder>> cases) : ShapeShiftConverter<TUnion, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public override TUnion? Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context)
	{
		if (decoder.TryReadNull())
		{
			return default;
		}

		int? count = decoder.ReadStartVector();
		if (count is not null and not 2)
		{
			throw new ShapeShiftSerializationException("Expected a two-element union value.");
		}

		ShapeShiftConverter<TUnion, TEncoder, TDecoder> converter;
		if (decoder.TryReadNull())
		{
			converter = baseConverter;
		}
		else if (decoder.NextTokenType == TokenType.Number)
		{
			int tag = checked((int)decoder.ReadInt64());
			converter = this.FindCase(tag).Converter;
		}
		else
		{
			string name = decoder.ReadString();
			converter = this.FindCase(name).Converter;
		}

		TUnion? value;
		try
		{
			value = converter.Read(ref decoder, context);
		}
		catch (ShapeShiftSerializationException ex) when (ex.AddEnclosingPathElement(1))
		{
			throw;
		}
		catch (Exception ex) when (SerializationErrors.IsAugmentable(ex))
		{
			throw SerializationErrors.Wrap(ex, 1, typeof(TUnion), serializing: false);
		}

		decoder.ReadEndVector();
		return value;
	}

	/// <inheritdoc/>
	public override void Write(ref TEncoder encoder, in TUnion? value, SerializationContext<TEncoder, TDecoder> context)
	{
		if (value is null)
		{
			encoder.WriteNull();
			return;
		}

		encoder.WriteStartVector(2);
		int index = getUnionCaseIndex(ref Unsafe.AsRef(in value));
		ShapeShiftConverter<TUnion, TEncoder, TDecoder> converter;
		if (index < 0)
		{
			encoder.WriteNull();
			converter = baseConverter;
		}
		else
		{
			if ((uint)index >= (uint)cases.Count)
			{
				throw new ShapeShiftSerializationException($"Union case index {index} is invalid for {typeof(TUnion).FullName}.");
			}

			UnionCase<TUnion, TEncoder, TDecoder> unionCase = cases[index];
			if (unionCase.UseTag)
			{
				encoder.WriteValue(unionCase.Tag);
			}
			else
			{
				encoder.WriteValue(unionCase.Name);
			}

			converter = unionCase.Converter;
		}

		try
		{
			converter.Write(ref encoder, value, context);
		}
		catch (ShapeShiftSerializationException ex) when (ex.AddEnclosingPathElement(1))
		{
			throw;
		}
		catch (Exception ex) when (SerializationErrors.IsAugmentable(ex))
		{
			throw SerializationErrors.Wrap(ex, 1, typeof(TUnion), serializing: true);
		}

		encoder.WriteEndVector();
	}

	private UnionCase<TUnion, TEncoder, TDecoder> FindCase(int tag)
	{
		foreach (UnionCase<TUnion, TEncoder, TDecoder> unionCase in cases)
		{
			if (unionCase.UseTag && unionCase.Tag == tag)
			{
				return unionCase;
			}
		}

		throw new ShapeShiftSerializationException($"Unrecognized union tag {tag} for {typeof(TUnion).FullName}.");
	}

	private UnionCase<TUnion, TEncoder, TDecoder> FindCase(string name)
	{
		foreach (UnionCase<TUnion, TEncoder, TDecoder> unionCase in cases)
		{
			if (!unionCase.UseTag && string.Equals(unionCase.Name, name, StringComparison.Ordinal))
			{
				return unionCase;
			}
		}

		throw new ShapeShiftSerializationException($"Unrecognized union case '{name}' for {typeof(TUnion).FullName}.");
	}
}
