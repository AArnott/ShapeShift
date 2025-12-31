// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;
using PolyType.Utilities;

namespace ShapeShift.Converters;

/// <summary>
/// A factory for delayed converters (those that support recursive types).
/// </summary>
internal sealed class DelayedConverterFactory<TEncoder, TDecoder> : IDelayedValueFactory
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public DelayedValue Create<T>(ITypeShape<T> typeShape)
		=> new DelayedValue<ConverterResult<TEncoder, TDecoder>>(self => ConverterResult.Ok(new DelayedConverter<T>(self)));

	/// <summary>
	/// A converter that defers to another converter that is not yet available.
	/// </summary>
	/// <typeparam name="T">The convertible data type.</typeparam>
	/// <param name="self">A box containing the not-yet-done converter.</param>
	internal class DelayedConverter<T>(DelayedValue<ConverterResult<TEncoder, TDecoder>> self) : ShapeShiftConverter<T, TEncoder, TDecoder>
	{
		/// <inheritdoc/>
		public override T? Read(ref TDecoder reader, SerializationContext<TEncoder, TDecoder> context)
			=> ((ShapeShiftConverter<T, TEncoder, TDecoder>)self.Result.ValueOrThrow).Read(ref reader, context);

		/// <inheritdoc/>
		public override void Write(ref TEncoder writer, in T? value, SerializationContext<TEncoder, TDecoder> context)
			=> ((ShapeShiftConverter<T, TEncoder, TDecoder>)self.Result.ValueOrThrow).Write(ref writer, value, context);
	}
}
