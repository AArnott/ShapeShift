// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Yaml;

/// <summary>
/// Options that control how a <see cref="YamlDecoder"/> reads a YAML 1.2 stream.
/// </summary>
public sealed record YamlLoadOptions
{
	/// <summary>
	/// Gets a value indicating whether a mapping key that appears more than once is an error.
	/// </summary>
	/// <value>
	/// <see langword="true" /> to reject duplicate keys. The default is <see langword="false" />.
	/// YAML 1.2 says mapping keys must be unique, and the official test suite still accepts
	/// documents that repeat a key. Set this to <see langword="true" /> to enforce uniqueness.
	/// When it is <see langword="false" />, every entry is kept in source order.
	/// </value>
	public bool RejectDuplicateKeys { get; init; } = false;

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
