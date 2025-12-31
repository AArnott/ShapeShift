// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift;

public abstract class ShapeShiftConverter<TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <summary>
	/// Gets the data type that this converter can serialize and deserialize.
	/// </summary>
	internal abstract Type DataType { get; }

	public abstract void WriteObject(ref TEncoder encoder, object? value, SerializationContext<TEncoder, TDecoder> context);

	public abstract object? ReadObject(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context);
}
