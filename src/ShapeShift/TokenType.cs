// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift;

/// <summary>
/// The kinds of token an <see cref="IDecoder"/> reports, which is the vocabulary every
/// format-neutral converter dispatches on.
/// </summary>
/// <remarks>
/// The set is deliberately small: it names only the distinctions a converter must be able to make
/// without knowing which format it is reading. A format that cannot make one of them (for example a
/// text format with no binary family) maps its values onto whichever member it can honestly report,
/// and declares the limitation to its conformance adapter.
/// </remarks>
public enum TokenType
{
	/// <summary>The beginning of a map: a sequence of property-name/value pairs.</summary>
	StartMap,

	/// <summary>The end of the innermost open map.</summary>
	EndMap,

	/// <summary>The beginning of a vector: an ordered sequence of values.</summary>
	StartVector,

	/// <summary>The end of the innermost open vector.</summary>
	EndVector,

	/// <summary>The name of the map entry whose value comes next.</summary>
	PropertyName,

	/// <summary>An explicit null.</summary>
	Null,

	/// <summary>Text.</summary>
	String,

	/// <summary>A number of any width, integral or otherwise.</summary>
	Number,

	/// <summary>A Boolean.</summary>
	Boolean,

	/// <summary>Binary data, for formats that carry bytes natively.</summary>
	Binary,

	/// <summary>
	/// No further token: the input has been fully consumed.
	/// </summary>
	/// <remarks>
	/// A decoder reports this rather than throwing, so a caller may always ask what comes next --
	/// which is exactly what a loop reading a stream of concatenated top-level values needs to do.
	/// </remarks>
	EndDocument,
}
