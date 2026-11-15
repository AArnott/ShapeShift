// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Yaml;

/// <summary>
/// The kind of a composed YAML node.
/// </summary>
internal enum YamlNodeKind
{
	/// <summary>A scalar, including the empty scalar that YAML treats as null.</summary>
	Scalar,

	/// <summary>A sequence.</summary>
	Sequence,

	/// <summary>A mapping.</summary>
	Mapping,
}

/// <summary>
/// The style a scalar was written in. Schema resolution treats plain scalars differently from quoted and block scalars.
/// </summary>
internal enum YamlScalarStyle
{
	/// <summary>An unquoted scalar.</summary>
	Plain,

	/// <summary>A single-quoted scalar.</summary>
	SingleQuoted,

	/// <summary>A double-quoted scalar.</summary>
	DoubleQuoted,

	/// <summary>A literal block scalar (<c>|</c>).</summary>
	Literal,

	/// <summary>A folded block scalar (<c>&gt;</c>).</summary>
	Folded,
}

/// <summary>
/// One mapping entry.
/// </summary>
/// <param name="Key">The key node.</param>
/// <param name="Value">The value node.</param>
internal readonly record struct YamlPair(YamlNode Key, YamlNode Value);

/// <summary>
/// One composed YAML node. Aliases point at the anchored node rather than copying it.
/// </summary>
internal sealed class YamlNode
{
	/// <summary>Gets or sets the node kind. Ignored when <see cref="Alias"/> is set.</summary>
	public YamlNodeKind Kind { get; set; }

	/// <summary>Gets or sets the scalar style. Meaningful for <see cref="YamlNodeKind.Scalar"/>.</summary>
	public YamlScalarStyle Style { get; set; }

	/// <summary>Gets or sets the scalar content. Empty for collections.</summary>
	public string Scalar { get; set; } = string.Empty;

	/// <summary>Gets or sets the resolved tag URI, or <c>!</c> for the non-specific tag, or <see langword="null"/> when the node is untagged.</summary>
	public string? Tag { get; set; }

	/// <summary>Gets or sets the anchor name, when this node declared one.</summary>
	public string? Anchor { get; set; }

	/// <summary>Gets or sets the node an alias refers to.</summary>
	public YamlNode? Alias { get; set; }

	/// <summary>Gets or sets sequence items.</summary>
	public List<YamlNode>? Items { get; set; }

	/// <summary>Gets or sets mapping entries in source order.</summary>
	public List<YamlPair>? Pairs { get; set; }

	/// <summary>Gets or sets how many times the decoder has read this node. The first read is not an expansion.</summary>
	internal int Reads { get; set; }

	/// <summary>Creates an empty plain scalar, which the core schema resolves as null.</summary>
	/// <returns>The scalar.</returns>
	public static YamlNode NullScalar() => new() { Kind = YamlNodeKind.Scalar, Style = YamlScalarStyle.Plain, Scalar = string.Empty };

	/// <summary>Creates a plain scalar.</summary>
	/// <param name="text">The scalar content.</param>
	/// <returns>The scalar.</returns>
	public static YamlNode Plain(string text) => new() { Kind = YamlNodeKind.Scalar, Style = YamlScalarStyle.Plain, Scalar = text };

	/// <summary>Creates a single-quoted scalar.</summary>
	/// <param name="text">The unescaped content.</param>
	/// <returns>The scalar.</returns>
	public static YamlNode Single(string text) => new() { Kind = YamlNodeKind.Scalar, Style = YamlScalarStyle.SingleQuoted, Scalar = text };

	/// <summary>Creates a double-quoted scalar.</summary>
	/// <param name="text">The unescaped content.</param>
	/// <returns>The scalar.</returns>
	public static YamlNode Double(string text) => new() { Kind = YamlNodeKind.Scalar, Style = YamlScalarStyle.DoubleQuoted, Scalar = text };

	/// <summary>Creates a block scalar.</summary>
	/// <param name="text">The folded or literal content.</param>
	/// <param name="folded"><see langword="true"/> for <c>&gt;</c>; <see langword="false"/> for <c>|</c>.</param>
	/// <returns>The scalar.</returns>
	public static YamlNode Block(string text, bool folded) => new()
	{
		Kind = YamlNodeKind.Scalar,
		Style = folded ? YamlScalarStyle.Folded : YamlScalarStyle.Literal,
		Scalar = text,
	};
}
