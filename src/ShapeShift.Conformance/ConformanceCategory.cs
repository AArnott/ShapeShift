// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Conformance;

/// <summary>
/// Groups the conformance test cases so that a consumer can report on, or select, a subset of them.
/// </summary>
[Flags]
public enum ConformanceCategory
{
	/// <summary>No category.</summary>
	None = 0,

	/// <summary>The <see cref="IDecoder.NextTokenType"/> a decoder reports for each kind of value.</summary>
	Tokens = 0x1,

	/// <summary>The <see cref="IDecoder.TryReadNull"/> consume-on-true contract and null handling generally.</summary>
	Null = 0x2,

	/// <summary>Container start/end token pairing and the decoder state each transition leaves behind.</summary>
	State = 0x4,

	/// <summary><see cref="IDecoder.Skip"/> over every shape of value.</summary>
	Skip = 0x8,

	/// <summary><see cref="ShapeShiftPath"/> traversal via the <c>TrySeek</c> decoder extension.</summary>
	Path = 0x10,

	/// <summary>Round-tripping every primitive width and scalar type.</summary>
	Primitives = 0x20,

	/// <summary>Round-tripping binary values.</summary>
	Binary = 0x40,

	/// <summary>Dynamic (<see cref="ShapeShiftValue"/>) and representation-preserving number reads.</summary>
	Dynamic = 0x80,

	/// <summary>Behavior when the input is malformed or truncated.</summary>
	Malformed = 0x100,

	/// <summary>Enforcement of the security limits carried on <see cref="SerializationContext{TEncoder, TDecoder}"/>.</summary>
	Limits = 0x200,

	/// <summary>Interaction with custom converters, reference preservation, and serializer policies.</summary>
	Converters = 0x400,

	/// <summary>The <see cref="IValueBoundaryScanner"/> that backs asynchronous adapters.</summary>
	Scanner = 0x800,

	/// <summary>Every category.</summary>
	All = Tokens | Null | State | Skip | Path | Primitives | Binary | Dynamic | Malformed | Limits | Converters | Scanner,
}
