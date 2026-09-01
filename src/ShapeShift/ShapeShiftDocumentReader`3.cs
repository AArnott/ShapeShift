// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift;

/// <summary>
/// Enables incremental, low-allocation enumeration of a sequence of whole top-level values sharing one decoder,
/// such as newline-delimited JSON (NDJSON) or a buffer containing several concatenated MessagePack values.
/// </summary>
/// <typeparam name="T">The type of each top-level value.</typeparam>
/// <typeparam name="TEncoder">The type of encoder used by the serializer that created this reader.</typeparam>
/// <typeparam name="TDecoder">The type of decoder that supplies the serialized data.</typeparam>
/// <remarks>
/// <para>
/// Create an instance with <see cref="ShapeShiftSerializer{TEncoder, TDecoder}.CreateDocumentReader{T}(ITypeShape{T}, CancellationToken)"/>
/// or a format-specific convenience overload (e.g. <c>JsonSerializer.CreateDocumentReader</c>).
/// </para>
/// <para>
/// Unlike <see cref="ShapeShiftSequenceReader{T, TEncoder, TDecoder}"/>, this reader does not expect (or consume) any
/// enclosing vector brackets: it simply reads whole values, one after another, until the decoder reports
/// <see cref="TokenType.EndDocument"/>. As with <see cref="ShapeShiftSequenceReader{T, TEncoder, TDecoder}"/>, this type
/// does not store the <typeparamref name="TDecoder"/> itself; the same decoder value must be passed by
/// <see langword="ref" /> to every call to <see cref="MoveNext(ref TDecoder)"/>.
/// </para>
/// <para>
/// Always call <see cref="Dispose"/> (or use a <see langword="using" /> statement) when finished with a reader,
/// so that pooled resources it may hold can be released.
/// </para>
/// </remarks>
public struct ShapeShiftDocumentReader<T, TEncoder, TDecoder> : IDisposable
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	private readonly ShapeShiftConverter<T, TEncoder, TDecoder> converter;
	private SerializationContext<TEncoder, TDecoder> context;
	private bool completed;

	/// <summary>
	/// Initializes a new instance of the <see cref="ShapeShiftDocumentReader{T, TEncoder, TDecoder}"/> struct.
	/// </summary>
	/// <param name="converter">The converter to use to read each top-level value.</param>
	/// <param name="context">The serialization context for this operation.</param>
	internal ShapeShiftDocumentReader(ShapeShiftConverter<T, TEncoder, TDecoder> converter, SerializationContext<TEncoder, TDecoder> context)
	{
		this.converter = converter;
		this.context = context;
	}

	/// <summary>
	/// Gets the value most recently read by <see cref="MoveNext(ref TDecoder)"/>.
	/// </summary>
	/// <remarks>
	/// This property's value is undefined before the first call to <see cref="MoveNext(ref TDecoder)"/>,
	/// and after any call to it that returns <see langword="false" />.
	/// </remarks>
	public T? Current { readonly get; private set; }

	/// <summary>
	/// Advances to the next top-level value, reading it into <see cref="Current"/>.
	/// </summary>
	/// <param name="decoder">
	/// The decoder that supplies the data. On every call, the same value (by <see langword="ref" />) that was supplied
	/// to the previous call must be given, so this reader observes the decoder's position as it was left after
	/// the previous value was read.
	/// </param>
	/// <returns>
	/// <see langword="true" /> if another value was read into <see cref="Current"/>;
	/// <see langword="false" /> if the decoder has reached the end of its input (<see cref="TokenType.EndDocument"/>).
	/// </returns>
	/// <exception cref="InvalidOperationException">Thrown if this method is called again after it has already returned <see langword="false" /> once.</exception>
	public bool MoveNext(ref TDecoder decoder)
	{
		Verify.Operation(!this.completed, "This reader has already reached the end of the input.");

		if (decoder.NextTokenType == TokenType.EndDocument)
		{
			this.completed = true;
			this.Current = default;
			return false;
		}

		this.context.CancellationToken.ThrowIfCancellationRequested();
		this.Current = this.converter.Read(ref decoder, this.context);
		return true;
	}

	/// <summary>
	/// Releases any pooled resources held by this reader.
	/// </summary>
	public void Dispose() => this.context.End();
}
