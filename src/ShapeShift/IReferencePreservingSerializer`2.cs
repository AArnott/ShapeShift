// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift;

/// <summary>
/// Implemented by a format's serializer when the format can represent a back-reference to an object
/// it has already written.
/// </summary>
/// <typeparam name="TEncoder">The format's encoder type.</typeparam>
/// <typeparam name="TDecoder">The format's decoder type.</typeparam>
/// <remarks>
/// <para>
/// There is no format-neutral way to say "this value is a reference to the object written earlier"
/// without colliding with data that happens to look the same, so each format that supports
/// <see cref="SerializerConfiguration{TEncoder, TDecoder}.PreserveReferences"/> defines its own
/// unambiguous token -- a reserved extension type, for example -- and implements this interface to
/// read and write it.
/// </para>
/// <para>
/// A serializer that does not implement this interface rejects any attempt to enable reference
/// preservation, rather than silently writing a graph as a tree.
/// </para>
/// </remarks>
public interface IReferencePreservingSerializer<TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc cref="SerializerConfiguration{TEncoder, TDecoder}.PreserveReferences"/>
	ReferencePreservationMode PreserveReferences { get; }

	/// <summary>
	/// Writes a back-reference to an object that has already been written.
	/// </summary>
	/// <param name="writer">The encoder to write to.</param>
	/// <param name="referenceId">The non-negative identifier of the previously written object.</param>
	/// <param name="context">The serialization context.</param>
	void WriteObjectReference(ref TEncoder writer, int referenceId, SerializationContext<TEncoder, TDecoder> context);

	/// <summary>
	/// Reads a back-reference if the next token is one.
	/// </summary>
	/// <param name="reader">The decoder to read from.</param>
	/// <param name="referenceId">Receives the identifier when the next token is a back-reference.</param>
	/// <param name="context">The serialization context.</param>
	/// <returns>
	/// <see langword="true" /> when a back-reference was consumed; <see langword="false" /> when the
	/// next token is ordinary data, which must be left unconsumed.
	/// </returns>
	bool TryReadObjectReference(ref TDecoder reader, out int referenceId, SerializationContext<TEncoder, TDecoder> context);
}
