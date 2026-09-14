// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift;

/// <summary>
/// Enables incremental, low-allocation enumeration of the elements of a vector (a JSON array, or a MessagePack array),
/// whether that vector is the root of a document, or is reached by first seeking into an enclosing document
/// (for example with <see cref="ShapeShiftPath"/>).
/// </summary>
/// <typeparam name="T">The type of each element in the vector.</typeparam>
/// <typeparam name="TEncoder">The type of encoder used by the serializer that created this reader.</typeparam>
/// <typeparam name="TDecoder">The type of decoder that supplies the serialized data.</typeparam>
/// <remarks>
/// <para>
/// Create an instance with <see cref="ShapeShiftSerializer{TEncoder, TDecoder}.CreateSequenceReader{T}(ITypeShape{T}, CancellationToken)"/>
/// or a format-specific convenience overload (e.g. <c>JsonSerializer.CreateSequenceReader</c>).
/// </para>
/// <para>
/// This type deliberately does <em>not</em> store the <typeparamref name="TDecoder"/> that supplies its data:
/// because decoders are typically <see langword="ref" /> structs, a type that stored one as a field could never
/// itself be anything but a <see langword="ref" /> struct, which in turn would rule out patterns like <see langword="await" />
/// between calls. Instead, the same decoder value must be passed <see langword="ref" /> to every call to <see cref="MoveNext(ref TDecoder)"/>,
/// much like an old-fashioned <c>while</c>-based iteration over an <see cref="System.Collections.IEnumerator"/>.
/// </para>
/// <para>
/// Always call <see cref="Dispose"/> (or use a <see langword="using" /> statement) when finished with a reader,
/// so that pooled resources it may hold can be released.
/// </para>
/// </remarks>
public struct ShapeShiftSequenceReader<T, TEncoder, TDecoder> : IDisposable
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	private readonly ShapeShiftConverter<T, TEncoder, TDecoder> converter;
	private SerializationContext<TEncoder, TDecoder> context;
	private State state;

	/// <summary>
	/// Initializes a new instance of the <see cref="ShapeShiftSequenceReader{T, TEncoder, TDecoder}"/> struct.
	/// </summary>
	/// <param name="converter">The converter to use to read each element.</param>
	/// <param name="context">The serialization context for this operation.</param>
	internal ShapeShiftSequenceReader(ShapeShiftConverter<T, TEncoder, TDecoder> converter, SerializationContext<TEncoder, TDecoder> context)
	{
		this.converter = converter;
		this.context = context;
	}

	private enum State
	{
		NotStarted,
		Enumerating,
		Completed,
	}

	/// <summary>
	/// Gets the element most recently read by <see cref="MoveNext(ref TDecoder)"/>.
	/// </summary>
	/// <remarks>
	/// This property's value is undefined before the first call to <see cref="MoveNext(ref TDecoder)"/>,
	/// and after any call to it that returns <see langword="false" />.
	/// </remarks>
	public T? Current { readonly get; private set; }

	/// <summary>
	/// Advances to the next element in the vector, reading it into <see cref="Current"/>.
	/// </summary>
	/// <param name="decoder">
	/// The decoder that supplies the data. On the first call, this must be positioned at the start of the vector
	/// (i.e. its <see cref="IDecoder.NextTokenType"/> must be <see cref="TokenType.StartVector"/>).
	/// On every call, the same value (by <see langword="ref" />) that was supplied to the previous call must be given,
	/// so that this reader observes the decoder's position as it was left after the last element (or the vector header) was read.
	/// </param>
	/// <returns>
	/// <see langword="true" /> if another element was read into <see cref="Current"/>;
	/// <see langword="false" /> if the vector has been fully enumerated, in which case its closing token has also been consumed.
	/// </returns>
	/// <exception cref="InvalidOperationException">Thrown if this method is called again after it has already returned <see langword="false" /> once.</exception>
	public bool MoveNext(ref TDecoder decoder)
	{
		Verify.Operation(this.state != State.Completed, "This reader has already reached the end of the vector.");

		if (this.state == State.NotStarted)
		{
			decoder.ReadStartVector();
			this.state = State.Enumerating;
		}

		if (decoder.NextTokenType == TokenType.EndVector)
		{
			decoder.ReadEndVector();
			this.state = State.Completed;
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
