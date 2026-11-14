// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Yaml;

/// <summary>
/// Options that control how a <see cref="YamlDecoder"/> reads a YAML 1.2 stream.
/// </summary>
public sealed record YamlLoadOptions
{
	/// <summary>
	/// Gets a value indicating whether a mapping may contain the same key more than once.
	/// </summary>
	/// <value>
	/// <see langword="false" /> to reject a repeated key. That is the default.
	/// YAML 1.2 requires mapping keys to be unique. Set this to <see langword="true" /> to keep
	/// every entry in source order, which is what the official YAML test suite accepts.
	/// </value>
	public bool AllowDuplicateKeys { get; init; } = false;

	/// <summary>
	/// Gets the maximum nesting depth of collections, block scalars excluded.
	/// </summary>
	/// <value>The default is 256.</value>
	public int MaxDepth { get; init; } = 256;

	/// <summary>
	/// Gets the maximum number of alias substitutions permitted in one document.
	/// </summary>
	/// <value>
	/// The default is 4096. This bounds alias expansion so a document cannot force the reader to walk
	/// an exponentially large graph.
	/// </value>
	public int MaxAliasExpansions { get; init; } = 4096;
}
