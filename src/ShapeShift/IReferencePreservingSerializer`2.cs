// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift;

public interface IReferencePreservingSerializer<TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc cref="SerializerConfiguration{TEncoder, TDecoder}.PreserveReferences"/>
	ReferencePreservationMode PreserveReferences { get; }

	void WriteObjectReference(ref TEncoder writer, int referenceId, SerializationContext<TEncoder, TDecoder> context);

	bool TryReadObjectReference(ref TDecoder reader, out int referenceId, SerializationContext<TEncoder, TDecoder> context);
}
