// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace ShapeShift.Yaml;

/// <summary>
/// Composes a YAML 1.2 stream into nodes.
/// </summary>
internal static class YamlParser
{
	private enum Chomp
	{
		Clip,
		Strip,
		Keep,
	}

	/// <summary>
	/// Parses every document in <paramref name="text"/>.
	/// </summary>
	/// <param name="text">The YAML text. A leading BOM is ignored.</param>
	/// <param name="options">Limits and duplicate-key handling.</param>
	/// <returns>The documents, in order. An empty or comment-only stream yields an empty list.</returns>
	/// <exception cref="DecoderException">The text is not valid YAML 1.2.</exception>
	public static List<YamlNode> Parse(string text, YamlLoadOptions options)
	{
		ArgumentNullException.ThrowIfNull(text);
		ArgumentNullException.ThrowIfNull(options);
		return new Session(text, options).ParseStream();
	}

	private readonly record struct Mark(int Index, int Line, int LineStart);

	private readonly record struct PlainLine(string Text, bool Empty, bool Broke);

	/// <summary>
	/// Cursor and compose state for one stream.
	/// </summary>
	private sealed class Session
	{
		private const int ImplicitKeyLimit = 1024;

		private readonly string text;
		private readonly YamlLoadOptions options;
		private readonly Dictionary<string, YamlNode> anchors = new(StringComparer.Ordinal);
		private readonly Dictionary<YamlNode, HashSet<YamlNode>> seenKeys = new();
		private readonly KeyComparer keyComparer;
		private readonly HashSet<YamlNode> constructing = [];
		private readonly Dictionary<string, string> tagHandles = new(StringComparer.Ordinal);
		private readonly HashSet<string> declaredHandles = new(StringComparer.Ordinal);

		private int index;
		private int line = 1;
		private int lineStart;
		private int depth;
		private int keyAliasExpansions;
		private bool sawYamlDirective;
		private bool directivesPending;
		private bool inBlockImplicitKey;
		private bool parsingFlowKey;
		private bool allowParentColumnSequence;
		private int blockParent = -1;
		private string? pendingAnchor;
		private string? pendingTag;

		public Session(string text, YamlLoadOptions options)
		{
			this.text = text;
			this.options = options.MaxDepth > 0 && options.MaxAliasExpansions > 0
				? options
				: options with
				{
					MaxDepth = options.MaxDepth > 0 ? options.MaxDepth : 256,
					MaxAliasExpansions = options.MaxAliasExpansions > 0 ? options.MaxAliasExpansions : 4096,
				};
			this.keyComparer = new KeyComparer(this);
			this.ResetHandles();
		}

		private bool Eof => this.index >= this.text.Length;

		private char Peek => this.index < this.text.Length ? this.text[this.index] : '\0';

		private int Column => this.index - this.lineStart;

		public List<YamlNode> ParseStream()
		{
			this.SkipBom();
			List<YamlNode> documents = [];
			while (true)
			{
				this.SkipStreamSpace();
				if (this.Eof)
				{
					if (this.directivesPending)
					{
						throw this.Error("A YAML directive must be followed by a document.");
					}

					break;
				}

				if (this.Column == 0 && this.Peek == '%')
				{
					this.ParseDirective();
					this.directivesPending = true;
					continue;
				}

				if (this.Column == 0 && this.IsDocumentStart())
				{
					this.ConsumeMarker();
					this.SkipSpacesAndTabs();
					if (this.IsComment())
					{
						this.SkipComment();
					}

					bool sameLine = !this.Eof && !this.IsBreak();
					if (!sameLine && !this.Eof)
					{
						this.ConsumeBreak();
					}

					if (!sameLine && this.BoundaryAhead())
					{
						documents.Add(this.FinishScalar(YamlNode.NullScalar()));
					}
					else
					{
						documents.Add(sameLine
							? this.ParseNodeAt(-1, inFlow: false, compact: false)
							: this.ParseNode(-1, inFlow: false, compact: false));
						this.EnsureDocumentTail();
					}

					this.ResetDocumentState();
					continue;
				}

				if (this.Column == 0 && this.IsDocumentEnd())
				{
					if (this.directivesPending)
					{
						throw this.Error("A YAML directive must be followed by a document.");
					}

					this.ConsumeMarker();
					this.SkipSpacesAndTabs();
					if (this.IsComment())
					{
						this.SkipComment();
					}

					if (!this.Eof && !this.IsBreak())
					{
						throw this.Error("A document end marker must end the line.");
					}

					if (!this.Eof)
					{
						this.ConsumeBreak();
					}

					continue;
				}

				documents.Add(this.ParseNode(-1, inFlow: false, compact: false));
				this.EnsureDocumentTail();
				this.ResetDocumentState();
			}

			return documents;
		}

		private void ParseDirective()
		{
			this.Advance();
			string name = this.ReadNsWord();
			List<string> args = [];
			while (!this.Eof && !this.IsBreak() && this.Peek != '#')
			{
				if (this.Peek is not (' ' or '\t'))
				{
					break;
				}

				this.SkipSpacesAndTabs();
				if (this.Eof || this.IsBreak() || this.Peek == '#')
				{
					break;
				}

				args.Add(this.ReadNsWord());
			}

			if (this.IsComment())
			{
				this.SkipComment();
			}

			if (!this.Eof && !this.IsBreak())
			{
				throw this.Error("A directive must end the line.");
			}

			if (!this.Eof)
			{
				this.ConsumeBreak();
			}

			if (name == "YAML")
			{
				if (this.sawYamlDirective)
				{
					throw this.Error("A document cannot contain two %YAML directives.");
				}

				this.sawYamlDirective = true;
				if (args.Count < 1 || !this.TrySplitVersion(args[0], out int major, out _))
				{
					throw this.Error("The %YAML directive requires a major.minor version.");
				}

				if (major != 1)
				{
					throw this.Error($"YAML version {args[0]} is not supported.");
				}

				for (int extra = 1; extra < args.Count; extra++)
				{
					if (!this.TrySplitVersion(args[extra], out _, out _))
					{
						throw this.Error("The %YAML directive has an unexpected parameter.");
					}
				}
			}
			else if (name == "TAG")
			{
				if (args.Count != 2)
				{
					throw this.Error("The %TAG directive requires a handle and a prefix.");
				}

				this.DeclareHandle(args[0], args[1]);
			}
		}

		private void DeclareHandle(string handle, string prefix)
		{
			if (!this.IsTagHandle(handle))
			{
				throw this.Error($"'{handle}' is not a tag handle.");
			}

			if (!this.declaredHandles.Add(handle))
			{
				throw this.Error($"The tag handle '{handle}' is declared more than once.");
			}

			if (prefix.Length == 0)
			{
				throw this.Error("A tag prefix must not be empty.");
			}

			this.tagHandles[handle] = prefix;
		}

		private YamlNode ParseNode(int parentIndent, bool inFlow, bool compact)
		{
			if (inFlow)
			{
				this.SkipFlowSpace();
				return this.ParseNodeAt(parentIndent, inFlow: true, compact);
			}

			if (!this.PositionBlockContent(parentIndent))
			{
				return this.FinishScalar(YamlNode.NullScalar());
			}

			return this.ParseNodeAt(parentIndent, inFlow: false, compact);
		}

		private YamlNode ParseNodeAt(int parentIndent, bool inFlow, bool compact, bool allowSameLineSequence = false)
		{
			int savedParent = this.blockParent;
			if (!inFlow)
			{
				this.blockParent = parentIndent;
			}

			try
			{
				if (!inFlow && this.Column == 0 && this.Peek == '%')
				{
					throw this.Error("A directive cannot appear inside a document.");
				}

				int at = this.Column;
				this.ParseProperties(inFlow);
				if (!inFlow && this.pendingTag != null && this.Peek == ',')
				{
					throw this.Error("A tag cannot be followed immediately by ','.");
				}

				if (this.IsAliasStart())
				{
					if (!inFlow && this.AliasHeadsBlockMapping())
					{
						return this.ParseBlockMapping(at, keyProperties: false);
					}

					return this.ParseAlias();
				}

				if (!inFlow && this.HasPendingProperty() && (this.Eof || this.IsBreak() || this.IsComment()))
				{
					Mark save = this.Here();
					if (this.PositionBlockContent(parentIndent))
					{
						if (this.IsBlockEntry())
						{
							return this.ParseBlockSequence(this.Column);
						}

						if (this.IsExplicitKey() || this.LooksLikeBlockMapping(compact: true))
						{
							return this.ParseBlockMapping(this.Column, keyProperties: false);
						}

						return this.ParseNodeAt(parentIndent, inFlow: false, compact: false);
					}

					this.Restore(save);
					if (this.allowParentColumnSequence && parentIndent >= 0 && this.PositionAtColumn(parentIndent) && this.IsBlockEntry())
					{
						return this.ParseBlockSequence(parentIndent);
					}

					this.Restore(save);
					return this.FinishScalar(YamlNode.NullScalar());
				}

				if (this.NodeContentMissing(inFlow))
				{
					return this.FinishScalar(YamlNode.NullScalar());
				}

				if (!inFlow && this.IsBlockEntry())
				{
					if (!compact && !allowSameLineSequence && !this.AtSpaceIndent())
					{
						throw this.Error("A block sequence entry must start the line.");
					}

					return this.ParseBlockSequence(this.Column);
				}

				if (!inFlow && this.IsExplicitKey())
				{
					if (!compact && !this.AtSpaceIndent())
					{
						throw this.Error("An explicit key must start the line.");
					}

					return this.ParseBlockMapping(this.Column, keyProperties: false);
				}

				if (!inFlow && this.LooksLikeBlockMapping(compact))
				{
					return this.ParseBlockMapping(at, keyProperties: this.HasPendingProperty());
				}

				char c = this.Peek;
				if (c == '"')
				{
					return this.FinishScalar(this.ParseDoubleQuoted());
				}

				if (c == '\'')
				{
					return this.FinishScalar(this.ParseSingleQuoted());
				}

				if (c == '[')
				{
					return this.ParseFlowSequence();
				}

				if (c == '{')
				{
					return this.ParseFlowMapping();
				}

				if (!inFlow && (c == '|' || c == '>') && this.IsBlockScalarHeader())
				{
					return this.FinishScalar(this.ParseBlockScalar(parentIndent));
				}

				if (inFlow && this.IsForbiddenFlowPlainStart())
				{
					throw this.Error("A flow node cannot start with this indicator.");
				}

				return this.FinishScalar(this.ParsePlain(inFlow, inFlow ? this.blockParent : parentIndent));
			}
			finally
			{
				this.blockParent = savedParent;
			}
		}

		private bool HasPendingProperty() => this.pendingAnchor != null || this.pendingTag != null;

		private void ParseProperties(bool inFlow)
		{
			while (!this.Eof)
			{
				if (inFlow)
				{
					this.SkipFlowSpace();
				}
				else
				{
					this.SkipSpacesAndTabs();
					if (this.IsComment())
					{
						this.SkipComment();
					}
				}

				if (this.Peek == '&')
				{
					if (this.pendingAnchor != null)
					{
						throw this.Error("A node cannot have two anchors.");
					}

					this.Advance();
					string name = this.ReadAnchorName();
					if (name.Length == 0)
					{
						throw this.Error("An anchor name is missing.");
					}

					this.pendingAnchor = name;
					continue;
				}

				if (this.Peek == '!')
				{
					if (this.pendingTag != null)
					{
						throw this.Error("A node cannot have two tags.");
					}

					this.pendingTag = this.ReadTag();
					continue;
				}

				return;
			}
		}

		private string ReadTag()
		{
			this.Advance();
			if (this.Peek == '<')
			{
				this.Advance();
				StringBuilder verbatim = new();
				while (!this.Eof && this.Peek != '>')
				{
					if (this.IsBreak() || this.Peek is ' ' or '\t')
					{
						throw this.Error("A verbatim tag cannot contain white space.");
					}

					verbatim.Append(this.Peek);
					this.Advance();
				}

				if (this.Peek != '>')
				{
					throw this.Error("A verbatim tag is missing '>'.");
				}

				this.Advance();
				if (verbatim.Length == 0)
				{
					throw this.Error("A verbatim tag must not be empty.");
				}

				return verbatim.ToString();
			}

			if (this.IsTagBreak(this.Peek))
			{
				return "!";
			}

			if (this.Peek == '!')
			{
				this.Advance();
				string suffix = this.ReadTagSuffix();
				return this.ResolveHandle("!!", suffix);
			}

			string word = this.ReadTagSuffix(allowBang: true);
			int bang = word.IndexOf('!', StringComparison.Ordinal);
			if (bang >= 0)
			{
				string handle = "!" + word[..(bang + 1)];
				string suffix = word[(bang + 1)..];
				return this.ResolveHandle(handle, suffix);
			}

			return this.ResolveHandle("!", word);
		}

		private string ResolveHandle(string handle, string suffix)
		{
			if (!this.tagHandles.TryGetValue(handle, out string? prefix))
			{
				throw this.Error($"The tag handle '{handle}' is not declared.");
			}

			return prefix + suffix;
		}

		private YamlNode ParseAlias()
		{
			if (this.pendingAnchor != null || this.pendingTag != null)
			{
				throw this.Error("An alias cannot carry an anchor or a tag.");
			}

			this.Advance();
			string name = this.ReadAnchorName();
			if (name.Length == 0)
			{
				throw this.Error("An alias name is missing.");
			}

			if (!this.anchors.TryGetValue(name, out YamlNode? target))
			{
				throw this.Error($"The alias '*{name}' has no matching anchor.");
			}

			if (this.constructing.Contains(target))
			{
				throw this.Error($"The alias '*{name}' is recursive.");
			}

			return new YamlNode { Alias = target };
		}

		private YamlNode ParseBlockSequence(int dashColumn)
		{
			if (dashColumn > 0 && this.LineStartsWithDocumentMarker())
			{
				throw this.Error("A block collection cannot start on a document marker line.");
			}

			this.EnterDepth();
			YamlNode node = new() { Kind = YamlNodeKind.Sequence, Items = [] };
			this.AttachIdentity(node);
			try
			{
				bool first = true;
				while (!this.Eof)
				{
					if (!first)
					{
						if (!this.PositionAtColumn(dashColumn) || !this.IsBlockEntry())
						{
							break;
						}
					}

					first = false;
					if (this.IndicatorHasTab())
					{
						throw this.Error("A tab cannot indent a block sequence entry.");
					}

					this.Advance();
					this.RejectTabBeforeBlockIndicator();
					bool savedAllow = this.allowParentColumnSequence;
					this.allowParentColumnSequence = false;
					try
					{
						node.Items!.Add(this.ParseBlockValue(dashColumn, compact: true));
					}
					finally
					{
						this.allowParentColumnSequence = savedAllow;
					}
				}
			}
			finally
			{
				this.constructing.Remove(node);
				this.LeaveDepth();
			}

			return node;
		}

		private YamlNode ParseBlockMapping(int keyColumn, bool keyProperties)
		{
			string? keyAnchor = null;
			string? keyTag = null;
			if (keyProperties)
			{
				keyAnchor = this.pendingAnchor;
				keyTag = this.pendingTag;
				this.pendingAnchor = null;
				this.pendingTag = null;
			}

			if (keyColumn > 0 && this.LineStartsWithDocumentMarker())
			{
				throw this.Error("A block collection cannot start on a document marker line.");
			}

			this.EnterDepth();
			YamlNode node = new() { Kind = YamlNodeKind.Mapping, Pairs = [] };
			this.AttachIdentity(node);
			try
			{
				bool first = true;
				while (!this.Eof)
				{
					if (first && keyProperties)
					{
						this.pendingAnchor = keyAnchor;
						this.pendingTag = keyTag;
						keyProperties = false;
					}

					if (!first)
					{
						if (!this.PositionAtColumn(keyColumn))
						{
							break;
						}

						if (this.IsDocumentBoundary() || !(this.IsExplicitKey() || this.LooksLikeBlockMapping(compact: true)))
						{
							break;
						}
					}

					first = false;
					YamlNode key;
					YamlNode value;
					if (this.IsExplicitKey())
					{
						if (this.IndicatorHasTab())
						{
							throw this.Error("A tab cannot indent an explicit mapping key.");
						}

						this.Advance();
						if (this.LineHasSeparationTab())
						{
							throw this.Error("A tab cannot separate an explicit key.");
						}

						key = this.ParseMappingValue(keyColumn, compact: true, allowSameLineSequence: false);
						if (!this.FindExplicitColon(keyColumn))
						{
							value = YamlNode.NullScalar();
						}
						else
						{
							value = this.ParseMappingValue(keyColumn, compact: true, allowSameLineSequence: true);
						}
					}
					else
					{
						key = this.ParseImplicitKey();
						if (!this.ConsumeBlockColon())
						{
							throw this.Error("A mapping entry requires ':'.");
						}

						value = this.ParseMappingValue(keyColumn, compact: false, allowSameLineSequence: false);
					}

					this.AddPair(node, key, value);
				}
			}
			finally
			{
				this.seenKeys.Remove(node);
				this.constructing.Remove(node);
				this.LeaveDepth();
			}

			return node;
		}

		private YamlNode ParseMappingValue(int keyColumn, bool compact, bool allowSameLineSequence)
		{
			bool saved = this.allowParentColumnSequence;
			this.allowParentColumnSequence = true;
			try
			{
				return this.ParseBlockValue(keyColumn, compact, allowSameLineSequence);
			}
			finally
			{
				this.allowParentColumnSequence = saved;
			}
		}

		private bool AliasHeadsBlockMapping()
		{
			Mark save = this.Here();
			this.Advance();
			_ = this.ReadAnchorName();
			this.SkipSpacesAndTabs();
			bool heads = this.Peek == ':' && this.ColonEndsPlain(inFlow: false);
			this.Restore(save);
			return heads;
		}

		private YamlNode ParseImplicitKey()
		{
			Mark start = this.Here();
			this.inBlockImplicitKey = true;
			try
			{
				this.ParseProperties(inFlow: false);
				if (this.IsAliasStart())
				{
					return this.ParseAlias();
				}

				YamlNode key = this.Peek switch
				{
					'"' => this.FinishScalar(this.ParseDoubleQuoted()),
					'\'' => this.FinishScalar(this.ParseSingleQuoted()),
					'[' => this.ParseFlowSequence(),
					'{' => this.ParseFlowMapping(),
					_ => this.FinishScalar(this.ParsePlain(inFlow: false, parentIndent: int.MinValue / 2)),
				};
				if (this.index - start.Index > ImplicitKeyLimit)
				{
					throw this.Error("An implicit key is longer than 1024 characters.");
				}

				return key;
			}
			finally
			{
				this.inBlockImplicitKey = false;
			}
		}

		private YamlNode ParseBlockValue(int parentIndent, bool compact, bool allowSameLineSequence = false)
		{
			bool tab = false;
			while (this.Peek is ' ' or '\t')
			{
				if (this.Peek == '\t')
				{
					tab = true;
				}

				this.Advance();
			}

			if (tab && (this.IsBlockEntry() || this.IsExplicitKey() || this.LooksLikeBlockMapping(compact: true)))
			{
				throw this.Error("A tab cannot indent a block collection.");
			}

			if (this.IsComment())
			{
				this.SkipComment();
			}

			if (!this.Eof && !this.IsBreak())
			{
				return this.ParseNodeAt(parentIndent, inFlow: false, compact, allowSameLineSequence);
			}

			// A nested block node must be indented further than the key, except a block sequence,
			// which may start in the key's column.
			Mark save = this.Here();
			if (this.PositionBlockContent(parentIndent))
			{
				return this.ParseNodeAt(parentIndent, inFlow: false, compact: false);
			}

			this.Restore(save);
			if (this.allowParentColumnSequence && this.PositionAtColumn(parentIndent) && this.IsBlockEntry())
			{
				return this.ParseBlockSequence(parentIndent);
			}

			this.Restore(save);
			return this.FinishScalar(YamlNode.NullScalar());
		}

		private YamlNode ParseFlowSequence()
		{
			this.EnterDepth();
			this.Advance();
			YamlNode node = new() { Kind = YamlNodeKind.Sequence, Items = [] };
			this.AttachIdentity(node);
			try
			{
				this.SkipFlowSpace();
				if (this.Peek == ']')
				{
					this.Advance();
					return node;
				}

				while (true)
				{
					YamlNode entry;
					int keyLine = this.line;
					if (this.IsFlowExplicitKey())
					{
						this.Advance();
						YamlNode key = this.ParseFlowEntryNode();
						YamlNode value = YamlNode.NullScalar();
						this.SkipFlowSpace();
						if (this.Peek == ':')
						{
							this.Advance();
							value = this.ParseFlowValue();
						}

						entry = this.SinglePair(key, value);
					}
					else
					{
						entry = this.ParseFlowEntryNode();
						this.SkipFlowSpace();
						if (this.Peek == ':')
						{
							if (this.line != keyLine)
							{
								throw this.Error("An implicit key in a flow sequence cannot contain a line break.");
							}

							this.Advance();
							entry = this.SinglePair(entry, this.ParseFlowValue());
						}
					}

					node.Items!.Add(entry);
					this.SkipFlowSpace();
					if (this.Peek == ',')
					{
						this.Advance();
						this.SkipFlowSpace();
						if (this.Peek == ']')
						{
							this.Advance();
							break;
						}

						continue;
					}

					if (this.Peek == ']')
					{
						this.Advance();
						break;
					}

					throw this.Error("A flow sequence is missing ',' or ']'.");
				}
			}
			finally
			{
				this.constructing.Remove(node);
				this.LeaveDepth();
			}

			return node;
		}

		private YamlNode ParseFlowMapping()
		{
			this.EnterDepth();
			this.Advance();
			YamlNode node = new() { Kind = YamlNodeKind.Mapping, Pairs = [] };
			this.AttachIdentity(node);
			try
			{
				this.SkipFlowSpace();
				if (this.Peek == '}')
				{
					this.Advance();
					return node;
				}

				while (true)
				{
					YamlNode key;
					YamlNode value;
					if (this.IsFlowExplicitKey())
					{
						this.Advance();
						key = this.ParseFlowEntryNode();
						value = YamlNode.NullScalar();
						this.SkipFlowSpace();
						if (this.Peek == ':')
						{
							this.Advance();
							value = this.ParseFlowValue();
						}
					}
					else
					{
						key = this.ParseFlowEntryNode();
						this.SkipFlowSpace();
						if (this.Peek == ':')
						{
							this.Advance();
							value = this.ParseFlowValue();
						}
						else
						{
							value = YamlNode.NullScalar();
						}
					}

					this.AddPair(node, key, value);
					this.SkipFlowSpace();
					if (this.Peek == ',')
					{
						this.Advance();
						this.SkipFlowSpace();
						if (this.Peek == '}')
						{
							this.Advance();
							break;
						}

						continue;
					}

					if (this.Peek == '}')
					{
						this.Advance();
						break;
					}

					throw this.Error("A flow mapping is missing ',' or '}'.");
				}
			}
			finally
			{
				this.seenKeys.Remove(node);
				this.constructing.Remove(node);
				this.LeaveDepth();
			}

			return node;
		}

		private YamlNode ParseFlowEntryNode()
		{
			this.SkipFlowSpace();
			if (this.Peek == ',')
			{
				throw this.Error("A flow collection has an unexpected comma.");
			}

			if (this.Peek is ']' or '}' or '\0' || (this.Peek == ':' && this.ColonEndsPlain(inFlow: true)))
			{
				return this.FinishScalar(YamlNode.NullScalar());
			}

			this.parsingFlowKey = true;
			try
			{
				return this.ParseNodeAt(this.blockParent, inFlow: true, compact: false);
			}
			finally
			{
				this.parsingFlowKey = false;
			}
		}

		private YamlNode ParseFlowValue()
		{
			this.SkipFlowSpace();
			if (this.Eof || this.Peek is ',' or ']' or '}')
			{
				return YamlNode.NullScalar();
			}

			return this.ParseNodeAt(this.blockParent, inFlow: true, compact: false);
		}

		private YamlNode SinglePair(YamlNode key, YamlNode value)
		{
			YamlNode map = new() { Kind = YamlNodeKind.Mapping, Pairs = [] };
			this.AddPair(map, key, value);
			this.seenKeys.Remove(map);
			return map;
		}

		private YamlNode ParsePlain(bool inFlow, int parentIndent)
		{
			List<PlainLine> lines = [];
			StringBuilder sb = new();
			while (!this.Eof)
			{
				if (this.IsBreak())
				{
					if (!this.TryContinuePlain(inFlow, parentIndent, out bool empty))
					{
						break;
					}

					lines.Add(new PlainLine(sb.ToString().TrimEnd(' ', '\t'), false, true));
					sb.Clear();
					if (empty)
					{
						lines.Add(new PlainLine(string.Empty, true, true));
					}

					continue;
				}

				if (this.PlainStops(inFlow, sb))
				{
					break;
				}

				sb.Append(this.Peek);
				this.Advance();
			}

			string last = sb.ToString().TrimEnd(' ', '\t');
			if (last.Length > 0 || lines.Count == 0)
			{
				lines.Add(new PlainLine(last, false, false));
			}

			return YamlNode.Plain(this.FoldPlain(lines));
		}

		private bool TryContinuePlain(bool inFlow, int parentIndent, out bool empty)
		{
			empty = false;
			if (this.inBlockImplicitKey)
			{
				return false;
			}

			Mark save = this.Here();
			this.ConsumeBreak();
			while (!this.Eof)
			{
				int spaces = 0;
				while (this.Peek == ' ')
				{
					spaces++;
					this.Advance();
				}

				while (this.Peek == '\t')
				{
					if (this.RestOfLineIsWhitespace())
					{
						this.ConsumeThroughBreak();
						if (!this.PlainContinuationFollows(inFlow, parentIndent))
						{
							this.Restore(save);
							return false;
						}

						empty = true;
						return true;
					}

					if (!inFlow && spaces <= parentIndent)
					{
						this.Restore(save);
						return false;
					}

					while (this.Peek is ' ' or '\t')
					{
						this.Advance();
					}

					break;
				}

				if (this.IsBreak())
				{
					this.ConsumeBreak();
					if (!this.PlainContinuationFollows(inFlow, parentIndent))
					{
						this.Restore(save);
						return false;
					}

					empty = true;
					return true;
				}

				if (this.Eof)
				{
					this.Restore(save);
					return false;
				}

				if (this.Column == 0 && this.IsDocumentBoundary())
				{
					this.Restore(save);
					return false;
				}

				if (this.Peek == '#' && this.IsComment())
				{
					this.Restore(save);
					return false;
				}

				if (inFlow && this.Peek is ',' or '[' or ']' or '{' or '}')
				{
					this.Restore(save);
					return false;
				}

				if (!inFlow && spaces <= parentIndent)
				{
					this.Restore(save);
					return false;
				}

				return true;
			}

			this.Restore(save);
			return false;
		}

		private bool PlainStops(bool inFlow, StringBuilder sb)
		{
			char c = this.Peek;
			if (c == '#' && (sb.Length == 0 || sb[^1] is ' ' or '\t'))
			{
				return true;
			}

			if (inFlow && c is ',' or '[' or ']' or '{' or '}')
			{
				return true;
			}

			if (c == ':' && this.ColonEndsPlain(inFlow))
			{
				return true;
			}

			if (!inFlow && this.Column == 0 && sb.Length == 0 && this.IsDocumentBoundary())
			{
				return true;
			}

			return false;
		}

		private bool ColonEndsPlain(bool inFlow)
		{
			char next = this.PeekAt(1);
			if (next is ' ' or '\t' or '\r' or '\n' or '\0')
			{
				return true;
			}

			return inFlow && next is ',' or '[' or ']' or '{' or '}';
		}

		private string FoldPlain(List<PlainLine> lines)
		{
			StringBuilder sb = new();
			bool first = true;
			bool previousEmpty = false;
			foreach (PlainLine line in lines)
			{
				if (!first && (line.Empty || !previousEmpty))
				{
					// A blank line replaces the folded space with a line break. The line that follows
					// the blank stays attached to that break, so it does not add a second one.
					sb.Append(line.Empty ? '\n' : ' ');
				}

				if (!line.Empty)
				{
					sb.Append(line.Text);
				}

				first = false;
				previousEmpty = line.Empty;
			}

			return sb.ToString();
		}

		private YamlNode ParseDoubleQuoted() => YamlNode.Double(this.ParseQuoted(doubleQuoted: true));

		private YamlNode ParseSingleQuoted() => YamlNode.Single(this.ParseQuoted(doubleQuoted: false));

		private string ParseQuoted(bool doubleQuoted)
		{
			char quote = doubleQuoted ? '"' : '\'';
			this.Advance();
			StringBuilder sb = new();
			int trailing = -1;
			int breaks = 0;
			bool escapedBreak = false;
			while (!this.Eof)
			{
				char c = this.Peek;
				if (c == quote)
				{
					if (!doubleQuoted && this.PeekAt(1) == '\'')
					{
						this.FlushQuotedBreak(sb, ref breaks, ref escapedBreak);
						sb.Append('\'');
						this.Advance();
						this.Advance();
						trailing = -1;
						continue;
					}

					this.FlushQuotedBreak(sb, ref breaks, ref escapedBreak);
					this.Advance();
					return sb.ToString();
				}

				if (doubleQuoted && c == '\\')
				{
					this.FlushQuotedBreak(sb, ref breaks, ref escapedBreak);
					this.Advance();
					if (this.IsBreak())
					{
						this.ConsumeBreak();
						escapedBreak = true;
						breaks = 0;
						trailing = -1;
						continue;
					}

					this.AppendEscape(sb);
					trailing = -1;
					continue;
				}

				if (this.IsBreak())
				{
					if (trailing >= 0)
					{
						sb.Length = trailing;
						trailing = -1;
					}

					this.ConsumeBreak();
					this.RejectBadContinuation(this.blockParent);
					breaks++;
					escapedBreak = false;
					continue;
				}

				if (breaks > 0 || escapedBreak)
				{
					if (c is ' ' or '\t')
					{
						this.Advance();
						continue;
					}

					this.FlushQuotedBreak(sb, ref breaks, ref escapedBreak);
				}

				if (c is ' ' or '\t')
				{
					if (trailing < 0)
					{
						trailing = sb.Length;
					}
				}
				else
				{
					trailing = -1;
				}

				sb.Append(c);
				this.Advance();
			}

			throw this.Error(doubleQuoted
				? "A double-quoted scalar is missing its closing quote."
				: "A single-quoted scalar is missing its closing quote.");
		}

		private void FlushQuotedBreak(StringBuilder sb, ref int breaks, ref bool escapedBreak)
		{
			if (escapedBreak && breaks == 0)
			{
				escapedBreak = false;
				return;
			}

			if (breaks <= 0)
			{
				return;
			}

			if (breaks == 1)
			{
				sb.Append(' ');
			}
			else
			{
				sb.Append('\n', breaks - 1);
			}

			breaks = 0;
			escapedBreak = false;
		}

		private void AppendEscape(StringBuilder sb)
		{
			if (this.Eof)
			{
				throw this.Error("A double-quoted scalar ends in a backslash.");
			}

			char c = this.Peek;
			this.Advance();
			switch (c)
			{
				case '0': sb.Append('\0'); return;
				case 'a': sb.Append('\a'); return;
				case 'b': sb.Append('\b'); return;
				case 't': sb.Append('\t'); return;
				case 'n': sb.Append('\n'); return;
				case 'v': sb.Append('\v'); return;
				case 'f': sb.Append('\f'); return;
				case 'r': sb.Append('\r'); return;
				case 'e': sb.Append('\u001b'); return;
				case ' ': sb.Append(' '); return;
				case '\t': sb.Append('\t'); return;
				case '"': sb.Append('"'); return;
				case '/': sb.Append('/'); return;
				case '\\': sb.Append('\\'); return;
				case 'N': sb.Append('\u0085'); return;
				case '_': sb.Append('\u00a0'); return;
				case 'L': sb.Append('\u2028'); return;
				case 'P': sb.Append('\u2029'); return;
				case 'x': sb.Append((char)this.ReadHex(2)); return;
				case 'u':
					int unit = this.ReadHex(4);
					if (char.IsHighSurrogate((char)unit))
					{
						if (this.Peek != '\\' || this.PeekAt(1) != 'u')
						{
							throw this.Error("A high surrogate escape must be followed by a low surrogate.");
						}

						this.Advance();
						this.Advance();
						int low = this.ReadHex(4);
						if (!char.IsLowSurrogate((char)low))
						{
							throw this.Error("A high surrogate escape must be followed by a low surrogate.");
						}

						sb.Append((char)unit);
						sb.Append((char)low);
						return;
					}

					sb.Append((char)unit);
					return;
				case 'U':
					sb.Append(char.ConvertFromUtf32(this.ReadHex(8)));
					return;
				default:
					throw this.Error($"The escape '\\{c}' is not valid in a double-quoted scalar.");
			}
		}

		private int ReadHex(int digits)
		{
			int value = 0;
			for (int n = 0; n < digits; n++)
			{
				if (this.Eof)
				{
					throw this.Error("A hexadecimal escape ended early.");
				}

				int digit = this.Peek switch
				{
					>= '0' and <= '9' => this.Peek - '0',
					>= 'a' and <= 'f' => this.Peek - 'a' + 10,
					>= 'A' and <= 'F' => this.Peek - 'A' + 10,
					_ => -1,
				};
				if (digit < 0)
				{
					throw this.Error("A hexadecimal escape contains a non-hex digit.");
				}

				value = (value << 4) | digit;
				this.Advance();
			}

			if (digits == 8 && value > 0x10FFFF)
			{
				throw this.Error("A \\\\U escape is outside the Unicode scalar range.");
			}

			return value;
		}

		private YamlNode ParseBlockScalar(int parentIndent)
		{
			bool folded = this.Peek == '>';
			this.Advance();
			Chomp chomp = Chomp.Clip;
			int explicitIndent = 0;
			bool chompSet = false;
			while (true)
			{
				char c = this.Peek;
				if (c is >= '1' and <= '9' && explicitIndent == 0)
				{
					explicitIndent = c - '0';
					this.Advance();
					continue;
				}

				if (c is '+' or '-' && !chompSet)
				{
					chomp = c == '+' ? Chomp.Keep : Chomp.Strip;
					chompSet = true;
					this.Advance();
					continue;
				}

				break;
			}

			if (this.Peek is >= '0' and <= '9')
			{
				throw this.Error("A block scalar indentation indicator must be a single digit from 1 to 9.");
			}

			this.SkipSpaces();
			if (this.Peek == '\t')
			{
				throw this.Error("A tab cannot separate a block scalar header from its comment.");
			}

			if (this.IsComment())
			{
				this.SkipComment();
			}

			if (!this.Eof && !this.IsBreak())
			{
				throw this.Error("A block scalar header must end the line.");
			}

			if (!this.Eof)
			{
				this.ConsumeBreak();
			}

			// At the root the parent column is -1. An explicit indicator is then an absolute column,
			// so "|2" means the content starts at column 2. Nested scalars stay relative to the parent.
			int baseIndent = parentIndent < 0 ? 0 : parentIndent;
			int indent = explicitIndent > 0 ? baseIndent + explicitIndent : -1;
			if (explicitIndent > 0 && indent <= parentIndent)
			{
				throw this.Error("A block scalar indentation indicator is not greater than its parent.");
			}

			List<PlainLine> lines = [];
			List<(int Spaces, bool Broke)> leadingBlanks = [];
			while (!this.Eof)
			{
				if (this.Column == 0 && this.IsDocumentBoundary())
				{
					break;
				}

				Mark lineMark = this.Here();
				int spaces = 0;
				while (this.Peek == ' ')
				{
					spaces++;
					this.Advance();
				}

				if (this.Peek == '\t')
				{
					if (indent < 0)
					{
						if (spaces <= parentIndent)
						{
							throw this.Error("A tab cannot be used to indent a block scalar.");
						}

						indent = spaces;
						this.CheckLeading(leadingBlanks, indent);
						this.AdoptLeading(lines, leadingBlanks);
					}
					else if (spaces < indent)
					{
						throw this.Error("A tab cannot be used to indent a block scalar.");
					}

					string tabRest = this.ReadRestOfLine(out bool tabBroke);
					lines.Add(new PlainLine(new string(' ', Math.Max(0, spaces - indent)) + tabRest, false, tabBroke));
					continue;
				}

				bool blank = this.IsBreak() || this.Eof;
				if (blank)
				{
					bool broke = this.IsBreak();
					if (indent < 0)
					{
						leadingBlanks.Add((spaces, broke));
					}
					else if (spaces > indent)
					{
						lines.Add(new PlainLine(new string(' ', spaces - indent), false, broke));
					}
					else
					{
						lines.Add(new PlainLine(string.Empty, true, broke));
					}

					if (broke)
					{
						this.ConsumeBreak();
					}

					continue;
				}

				if (indent < 0)
				{
					if (spaces <= parentIndent)
					{
						this.Restore(lineMark);
						break;
					}

					indent = spaces;
					this.CheckLeading(leadingBlanks, indent);
					this.AdoptLeading(lines, leadingBlanks);
				}

				if (spaces < indent)
				{
					if (spaces <= parentIndent || this.IsComment())
					{
						this.Restore(lineMark);
						break;
					}

					throw this.Error("A block scalar line is indented less than the block content.");
				}

				string content = new string(' ', spaces - indent) + this.ReadRestOfLine(out bool contentBroke);
				lines.Add(new PlainLine(content, false, contentBroke));
			}

			if (indent < 0)
			{
				foreach ((int _, bool broke) in leadingBlanks)
				{
					lines.Add(new PlainLine(string.Empty, true, broke));
				}
			}

			return YamlNode.Block(this.FoldBlock(lines, folded, chomp), folded);
		}

		private void AdoptLeading(List<PlainLine> lines, List<(int Spaces, bool Broke)> leadingBlanks)
		{
			foreach ((int _, bool broke) in leadingBlanks)
			{
				lines.Add(new PlainLine(string.Empty, true, broke));
			}

			leadingBlanks.Clear();
		}

		private string ReadRestOfLine(out bool broke)
		{
			StringBuilder sb = new();
			while (!this.Eof && !this.IsBreak())
			{
				sb.Append(this.Peek);
				this.Advance();
			}

			broke = this.IsBreak();
			if (broke)
			{
				this.ConsumeBreak();
			}

			return sb.ToString();
		}

		private string FoldBlock(List<PlainLine> lines, bool folded, Chomp chomp)
		{
			static bool StartsWithBlank(string text) => text.Length > 0 && text[0] is ' ' or '\t';

			int end = lines.Count;
			while (end > 0 && lines[end - 1].Empty)
			{
				end--;
			}

			StringBuilder sb = new();
			int index = 0;
			while (index < end && lines[index].Empty)
			{
				if (lines[index].Broke)
				{
					sb.Append('\n');
				}

				index++;
			}

			while (index < end)
			{
				PlainLine line = lines[index];
				sb.Append(line.Text);
				bool leadingBlank = StartsWithBlank(line.Text);
				index++;
				int breaks = line.Broke ? 1 : 0;
				while (index < end && lines[index].Empty)
				{
					if (lines[index].Broke)
					{
						breaks++;
					}

					index++;
				}

				if (index < end)
				{
					bool nextBlank = StartsWithBlank(lines[index].Text);
					if (folded && breaks > 0 && !leadingBlank && !nextBlank)
					{
						// A break followed by empty lines drops the first break. A single break between
						// ordinary lines becomes a space, including when the line ends with spaces.
						sb.Append(breaks == 1 ? " " : new string('\n', breaks - 1));
					}
					else
					{
						sb.Append('\n', breaks);
					}
				}
				else if (chomp == Chomp.Keep)
				{
					sb.Append('\n', breaks);
					this.AppendTrailingBreaks(sb, lines, end);
				}
				else if (chomp == Chomp.Clip && breaks > 0)
				{
					sb.Append('\n');
				}
			}

			if (end == 0 && chomp == Chomp.Keep)
			{
				this.AppendTrailingBreaks(sb, lines, 0);
			}

			return sb.ToString();
		}

		private void AppendTrailingBreaks(StringBuilder sb, List<PlainLine> lines, int start)
		{
			for (int i = start; i < lines.Count; i++)
			{
				if (lines[i].Broke)
				{
					sb.Append('\n');
				}
			}
		}

		private void AddPair(YamlNode map, YamlNode key, YamlNode value)
		{
			map.Pairs!.Add(new YamlPair(key, value));
			if (this.options.AllowDuplicateKeys)
			{
				return;
			}

			if (!this.seenKeys.TryGetValue(map, out HashSet<YamlNode>? seen))
			{
				seen = new HashSet<YamlNode>(this.keyComparer);
				this.seenKeys[map] = seen;
			}

			if (!seen.Add(key))
			{
				throw this.Error("A YAML mapping contains a duplicate key.");
			}
		}

		private int KeyHash(YamlNode key)
		{
			this.keyAliasExpansions = 0;
			return this.HashNode(key, [], []);
		}

		private int HashNode(YamlNode node, Dictionary<YamlNode, int> done, HashSet<YamlNode> stack)
		{
			node = this.FollowKey(node);
			if (done.TryGetValue(node, out int cached))
			{
				return cached;
			}

			if (!stack.Add(node))
			{
				return 0;
			}

			int hash;
			if (node.Kind == YamlNodeKind.Scalar)
			{
				hash = HashCode.Combine(1, YamlSchema.CanonicalScalar(node));
			}
			else if (node.Kind == YamlNodeKind.Sequence)
			{
				HashCode code = default;
				code.Add(2);
				code.Add(node.Items!.Count);
				foreach (YamlNode item in node.Items)
				{
					code.Add(this.HashNode(item, done, stack));
				}

				hash = code.ToHashCode();
			}
			else
			{
				HashCode code = default;
				code.Add(3);
				code.Add(node.Pairs!.Count);
				foreach (YamlPair pair in node.Pairs)
				{
					code.Add(this.HashNode(pair.Key, done, stack));
					code.Add(this.HashNode(pair.Value, done, stack));
				}

				hash = code.ToHashCode();
			}

			stack.Remove(node);
			done[node] = hash;
			return hash;
		}

		private bool SameKey(YamlNode left, YamlNode right)
		{
			this.keyAliasExpansions = 0;
			return this.SameNode(left, right, []);
		}

		private bool SameNode(YamlNode left, YamlNode right, Dictionary<(YamlNode Left, YamlNode Right), bool> memo)
		{
			left = this.FollowKey(left);
			right = this.FollowKey(right);
			if (ReferenceEquals(left, right))
			{
				return true;
			}

			(YamlNode Left, YamlNode Right) pair = (left, right);
			if (memo.TryGetValue(pair, out bool cached))
			{
				return cached;
			}

			// Assume equality until a difference is found so a cycle does not recurse forever.
			memo[pair] = true;
			bool equal = left.Kind == right.Kind && this.SameContent(left, right, memo);
			memo[pair] = equal;
			return equal;
		}

		private bool SameContent(YamlNode left, YamlNode right, Dictionary<(YamlNode Left, YamlNode Right), bool> memo)
		{
			switch (left.Kind)
			{
				case YamlNodeKind.Scalar:
					return YamlSchema.CanonicalScalar(left) == YamlSchema.CanonicalScalar(right);
				case YamlNodeKind.Sequence:
					if (left.Items!.Count != right.Items!.Count)
					{
						return false;
					}

					for (int i = 0; i < left.Items.Count; i++)
					{
						if (!this.SameNode(left.Items[i], right.Items[i], memo))
						{
							return false;
						}
					}

					return true;
				default:
					if (left.Pairs!.Count != right.Pairs!.Count)
					{
						return false;
					}

					for (int i = 0; i < left.Pairs.Count; i++)
					{
						if (!this.SameNode(left.Pairs[i].Key, right.Pairs[i].Key, memo) ||
							!this.SameNode(left.Pairs[i].Value, right.Pairs[i].Value, memo))
						{
							return false;
						}
					}

					return true;
			}
		}

		private YamlNode FollowKey(YamlNode node)
		{
			while (node.Alias is not null)
			{
				this.keyAliasExpansions++;
				if (this.keyAliasExpansions > this.options.MaxAliasExpansions)
				{
					throw this.Error($"YAML alias expansion exceeds the maximum of {this.options.MaxAliasExpansions}.");
				}

				node = node.Alias;
			}

			return node;
		}

		private YamlNode FinishScalar(YamlNode node)
		{
			this.AttachIdentity(node);
			this.constructing.Remove(node);
			this.ValidateTagShape(node);
			return node;
		}

		private void AttachIdentity(YamlNode node)
		{
			if (this.pendingTag != null)
			{
				node.Tag = this.pendingTag;
				this.pendingTag = null;
			}

			if (this.pendingAnchor != null)
			{
				node.Anchor = this.pendingAnchor;
				this.anchors[this.pendingAnchor] = node;
				this.constructing.Add(node);
				this.pendingAnchor = null;
			}

			this.ValidateTagShape(node);
		}

		private void ValidateTagShape(YamlNode node)
		{
			if (node.Tag is null || node.Tag == "!")
			{
				return;
			}

			bool scalarTag = node.Tag is YamlSchema.StrTag or YamlSchema.NullTag or YamlSchema.BoolTag or YamlSchema.IntTag or YamlSchema.FloatTag or YamlSchema.BinaryTag;
			if (scalarTag && node.Kind != YamlNodeKind.Scalar)
			{
				throw this.Error($"The tag {node.Tag} requires a scalar.");
			}

			if (node.Tag == YamlSchema.SeqTag && node.Kind != YamlNodeKind.Sequence)
			{
				throw this.Error($"The tag {node.Tag} requires a sequence.");
			}

			if (node.Tag == YamlSchema.MapTag && node.Kind != YamlNodeKind.Mapping)
			{
				throw this.Error($"The tag {node.Tag} requires a mapping.");
			}
		}

		private bool PositionBlockContent(int parentIndent)
		{
			while (!this.Eof)
			{
				if (this.Column == 0 && this.IsDocumentBoundary())
				{
					return false;
				}

				Mark line = this.Here();
				if (this.Column != 0 && this.Peek is not (' ' or '\t' or '#' or '\r' or '\n'))
				{
					return true;
				}

				int spaces = 0;
				while (this.Peek == ' ')
				{
					spaces++;
					this.Advance();
				}

				if (this.Peek == '\t')
				{
					if (this.RestOfLineIsWhitespace())
					{
						this.ConsumeThroughBreak();
						continue;
					}

					if (spaces <= parentIndent)
					{
						this.Restore(line);
						return false;
					}

					while (this.Peek is ' ' or '\t')
					{
						this.Advance();
					}

					return !this.Eof && !this.IsBreak();
				}

				if (this.IsBreak())
				{
					this.ConsumeBreak();
					continue;
				}

				if (this.Eof)
				{
					return false;
				}

				if (this.Peek == '#' && this.IsComment())
				{
					this.SkipComment();
					if (this.IsBreak())
					{
						this.ConsumeBreak();
					}

					continue;
				}

				if (spaces <= parentIndent)
				{
					this.Restore(line);
					return false;
				}

				return true;
			}

			return false;
		}

		private bool PositionAtColumn(int column)
		{
			Mark save = this.Here();
			if (this.Column != 0 && !this.IsBreak() && !this.Eof)
			{
				this.SkipSpacesAndTabs();
				if (this.IsComment())
				{
					this.SkipComment();
				}

				if (!this.Eof && !this.IsBreak())
				{
					this.Restore(save);
					return false;
				}
			}

			while (!this.Eof)
			{
				if (this.IsBreak())
				{
					this.ConsumeBreak();
					continue;
				}

				if (this.Column == 0 && this.IsDocumentBoundary())
				{
					this.Restore(save);
					return false;
				}

				int spaces = 0;
				while (this.Peek == ' ')
				{
					spaces++;
					this.Advance();
				}

				if (this.Peek == '\t')
				{
					if (this.RestOfLineIsWhitespace())
					{
						this.ConsumeThroughBreak();
						continue;
					}

					this.Restore(save);
					return false;
				}

				if (this.IsBreak())
				{
					this.ConsumeBreak();
					continue;
				}

				if (this.Eof)
				{
					this.Restore(save);
					return false;
				}

				if (this.Peek == '#' && this.IsComment())
				{
					this.SkipComment();
					if (this.IsBreak())
					{
						this.ConsumeBreak();
					}

					continue;
				}

				if (spaces == column)
				{
					return true;
				}

				this.Restore(save);
				return false;
			}

			this.Restore(save);
			return false;
		}

		private bool LooksLikeBlockMapping(bool compact)
		{
			if (!compact && this.LineAlreadyHasValueIndicator())
			{
				return false;
			}

			if (this.IsBlockEntry() || this.IsExplicitKey() || this.IsBlockScalarHeader())
			{
				return false;
			}

			return this.ScanImplicitColon();
		}

		private bool ScanImplicitColon()
		{
			Mark save = this.Here();
			bool found = false;
			try
			{
				this.SkipSpacesAndTabs();
				while (this.Peek is '&' or '!')
				{
					if (this.Peek == '&')
					{
						this.Advance();
						_ = this.ReadAnchorName();
					}
					else
					{
						_ = this.ReadTag();
					}

					this.SkipSpacesAndTabs();
				}

				if (this.Peek is '"' or '\'')
				{
					this.SkipQuotedForScan(this.Peek);
				}
				else if (this.Peek is '[' or '{')
				{
					if (!this.SkipFlowForScan(this.Peek == '[' ? ']' : '}', depth: 1))
					{
						return false;
					}
				}
				else if (this.Peek == '*')
				{
					this.Advance();
					_ = this.ReadAnchorName();
				}
				else
				{
					int scanned = 0;
					while (!this.Eof && !this.IsBreak() && scanned <= ImplicitKeyLimit)
					{
						if (this.Peek == ':' && this.ColonEndsPlain(inFlow: false))
						{
							found = true;
							break;
						}

						if (this.Peek == '#' && scanned > 0 && this.text[this.index - 1] is ' ' or '\t')
						{
							break;
						}

						this.Advance();
						scanned++;
					}
				}

				if (!found)
				{
					this.SkipSpacesAndTabs();
					found = this.Peek == ':' && this.ColonEndsPlain(inFlow: false);
				}
			}
			finally
			{
				this.Restore(save);
			}

			return found;
		}

		private void SkipQuotedForScan(char quote)
		{
			this.Advance();
			while (!this.Eof && !this.IsBreak())
			{
				if (this.Peek == quote)
				{
					if (quote == '\'' && this.PeekAt(1) == '\'')
					{
						this.Advance();
						this.Advance();
						continue;
					}

					this.Advance();
					return;
				}

				if (quote == '"' && this.Peek == '\\')
				{
					this.Advance();
					if (!this.Eof && !this.IsBreak())
					{
						this.Advance();
					}

					continue;
				}

				this.Advance();
			}
		}

		private bool SkipFlowForScan(char end, int depth)
		{
			if (depth > this.options.MaxDepth)
			{
				throw this.Error($"YAML nesting exceeds the maximum depth of {this.options.MaxDepth}.");
			}

			this.Advance();
			while (!this.Eof && !this.IsBreak())
			{
				char c = this.Peek;
				if (c == end)
				{
					this.Advance();
					return true;
				}

				if (c == '[')
				{
					if (!this.SkipFlowForScan(']', depth + 1))
					{
						return false;
					}

					continue;
				}

				if (c == '{')
				{
					if (!this.SkipFlowForScan('}', depth + 1))
					{
						return false;
					}

					continue;
				}

				if (c is '"' or '\'')
				{
					this.SkipQuotedForScan(c);
					continue;
				}

				this.Advance();
			}

			return false;
		}

		private bool FindExplicitColon(int keyColumn)
		{
			this.SkipSpacesAndTabs();
			if (this.IsComment())
			{
				this.SkipComment();
			}

			if (this.ConsumeBlockColon())
			{
				return true;
			}

			if (!this.Eof && !this.IsBreak())
			{
				return false;
			}

			Mark save = this.Here();
			if (this.PositionAtColumn(keyColumn) && this.ConsumeBlockColon())
			{
				return true;
			}

			this.Restore(save);
			return false;
		}

		private bool ConsumeBlockColon()
		{
			this.SkipSpacesAndTabs();
			if (this.Peek != ':')
			{
				return false;
			}

			char next = this.PeekAt(1);
			if (next is ' ' or '\t' or '\r' or '\n' or '\0')
			{
				this.Advance();
				return true;
			}

			return false;
		}

		private bool BoundaryAhead()
		{
			Mark save = this.Here();
			this.SkipStreamSpace();
			bool boundary = this.Eof || (this.Column == 0 && (this.IsDocumentStart() || this.IsDocumentEnd()));
			this.Restore(save);
			return boundary;
		}

		private void EnsureDocumentTail()
		{
			this.SkipSpacesAndTabs();
			if (this.IsComment())
			{
				this.SkipComment();
			}

			if (this.IsBreak())
			{
				this.ConsumeBreak();
			}

			this.SkipStreamSpace();
			if (this.Eof)
			{
				return;
			}

			if (this.Column == 0 && this.IsDocumentEnd())
			{
				this.ConsumeMarker();
				this.SkipSpacesAndTabs();
				if (this.IsComment())
				{
					this.SkipComment();
				}

				if (!this.Eof && !this.IsBreak())
				{
					throw this.Error("A document end marker must end the line.");
				}

				if (!this.Eof)
				{
					this.ConsumeBreak();
				}

				return;
			}

			if (this.Column == 0 && this.IsDocumentStart())
			{
				return;
			}

			if (this.Column == 0 && this.Peek == '%')
			{
				throw this.Error("A directive must be separated from the previous document by '...'.");
			}

			throw this.Error("The YAML document has trailing content.");
		}

		private void SkipStreamSpace()
		{
			while (!this.Eof)
			{
				Mark save = this.Here();
				while (this.Peek is ' ' or '\t')
				{
					this.Advance();
				}

				if (this.Eof)
				{
					return;
				}

				if (this.IsComment())
				{
					this.SkipComment();
					if (this.IsBreak())
					{
						this.ConsumeBreak();
					}

					continue;
				}

				if (this.IsBreak())
				{
					this.ConsumeBreak();
					continue;
				}

				this.Restore(save);
				return;
			}
		}

		private void SkipFlowSpace()
		{
			while (!this.Eof)
			{
				if (this.Peek is ' ' or '\t')
				{
					this.Advance();
					continue;
				}

				if (this.IsBreak())
				{
					if (this.inBlockImplicitKey)
					{
						throw this.Error("An implicit key cannot contain a line break.");
					}

					this.ConsumeBreak();
					this.RejectBadContinuation(this.blockParent);
					continue;
				}

				if (this.IsComment())
				{
					this.SkipComment();
					continue;
				}

				return;
			}
		}

		private void CheckLeading(List<(int Spaces, bool Broke)> leadingBlanks, int indent)
		{
			foreach ((int count, bool _) in leadingBlanks)
			{
				if (count > indent)
				{
					throw this.Error("A block scalar line is indented more than the first content line.");
				}
			}
		}

		private void RejectBadContinuation(int parentIndent)
		{
			if (this.Column == 0 && this.IsDocumentBoundary())
			{
				throw this.Error("A document marker cannot appear inside this node.");
			}

			int spaces = 0;
			int k = this.index;
			while (k < this.text.Length && this.text[k] == ' ')
			{
				spaces++;
				k++;
			}

			if (k < this.text.Length && this.text[k] == '\t' && spaces <= parentIndent)
			{
				int rest = k;
				while (rest < this.text.Length && this.text[rest] is ' ' or '\t')
				{
					rest++;
				}

				if (rest >= this.text.Length || this.text[rest] is '\n' or '\r')
				{
					return;
				}

				throw this.Error("A tab cannot be used as indentation.");
			}

			if (k >= this.text.Length || this.text[k] is '\n' or '\r' or '#')
			{
				return;
			}

			if (parentIndent >= 0 && spaces <= parentIndent)
			{
				throw this.Error("A continuation line is not indented past its parent.");
			}
		}

		private void RejectTabBeforeBlockIndicator()
		{
			if (!this.SeparationContainsTab(out char next, out char after))
			{
				return;
			}

			if ((next is '-' or '?') && after is ' ' or '\t' or '\r' or '\n' or '\0' or '#')
			{
				throw this.Error("A tab cannot precede a block indicator.");
			}
		}

		private bool LineHasSeparationTab() => this.SeparationContainsTab(out _, out _);

		private bool SeparationContainsTab(out char next, out char after)
		{
			next = '\0';
			after = '\0';
			bool tab = false;
			int k = this.index;
			while (k < this.text.Length && this.text[k] is ' ' or '\t')
			{
				if (this.text[k] == '\t')
				{
					tab = true;
				}

				k++;
			}

			if (k < this.text.Length)
			{
				next = this.text[k];
				after = k + 1 < this.text.Length ? this.text[k + 1] : '\0';
			}

			return tab;
		}

		private void SkipSpacesAndTabs()
		{
			while (this.Peek is ' ' or '\t')
			{
				this.Advance();
			}
		}

		private void SkipSpaces()
		{
			while (this.Peek == ' ')
			{
				this.Advance();
			}
		}

		private void SkipComment()
		{
			while (!this.Eof && !this.IsBreak())
			{
				this.Advance();
			}
		}

		private void SkipBom()
		{
			if (!this.Eof && this.Peek == '\uFEFF')
			{
				this.Advance();
				this.line = 1;
				this.lineStart = this.index;
			}
		}

		private bool NodeContentMissing(bool inFlow)
		{
			if (this.Eof)
			{
				return true;
			}

			if (inFlow)
			{
				if (this.Peek is ',' or ']' or '}')
				{
					return true;
				}

				return this.parsingFlowKey && this.Peek == ':' && this.ColonEndsPlain(inFlow: true);
			}

			if (this.IsComment())
			{
				return true;
			}

			return this.IsBreak() || (this.Column == 0 && this.IsDocumentBoundary());
		}

		private bool IsForbiddenFlowPlainStart()
		{
			char c = this.Peek;
			char next = this.PeekAt(1);
			if (c is '-' or '?' or ':')
			{
				return next is ' ' or '\t' or '\r' or '\n' or '\0' or '#' or ',' or '[' or ']' or '{' or '}';
			}

			return false;
		}

		private bool IsBlockEntry()
		{
			if (this.Peek != '-')
			{
				return false;
			}

			char next = this.PeekAt(1);
			return next is ' ' or '\t' or '\r' or '\n' or '\0';
		}

		private bool IsExplicitKey()
		{
			if (this.Peek != '?')
			{
				return false;
			}

			char next = this.PeekAt(1);
			return next is ' ' or '\t' or '\r' or '\n' or '\0' or '#';
		}

		private bool IsFlowExplicitKey()
		{
			if (this.Peek != '?')
			{
				return false;
			}

			char next = this.PeekAt(1);
			return next is ' ' or '\t' or '\r' or '\n' or '\0' or '#' or ',' or '[' or ']' or '{' or '}' or ':';
		}

		private bool IsBlockScalarHeader()
		{
			char c = this.Peek;
			if (c is not ('|' or '>'))
			{
				return false;
			}

			char next = this.PeekAt(1);
			return next is ' ' or '\t' or '\r' or '\n' or '\0' or '#' or '+' or '-' or (>= '0' and <= '9');
		}

		private bool IsAliasStart() => this.Peek == '*' && !this.IsTagBreak(this.PeekAt(1));

		private bool IsDocumentStart() => this.IsMarker('-');

		private bool IsDocumentEnd() => this.IsMarker('.');

		private bool IsDocumentBoundary() => this.IsDocumentStart() || this.IsDocumentEnd();

		private bool IsMarker(char marker)
		{
			if (this.Column != 0 || this.Peek != marker || this.PeekAt(1) != marker || this.PeekAt(2) != marker)
			{
				return false;
			}

			char next = this.PeekAt(3);
			return next is ' ' or '\t' or '\r' or '\n' or '\0' or '#';
		}

		private bool IsComment()
		{
			if (this.Peek != '#')
			{
				return false;
			}

			if (this.index == 0 || this.Column == 0)
			{
				return true;
			}

			char previous = this.text[this.index - 1];
			return previous is ' ' or '\t' or '\n' or '\r';
		}

		private bool IndicatorHasTab()
		{
			for (int k = this.lineStart; k < this.index; k++)
			{
				if (this.text[k] == '\t')
				{
					return true;
				}
			}

			return false;
		}

		private bool PlainContinuationFollows(bool inFlow, int parentIndent)
		{
			while (!this.Eof)
			{
				int spaces = 0;
				while (this.Peek == ' ')
				{
					spaces++;
					this.Advance();
				}

				if (this.Peek == '\t')
				{
					if (this.RestOfLineIsWhitespace())
					{
						this.ConsumeThroughBreak();
						continue;
					}

					return false;
				}

				if (this.IsBreak())
				{
					this.ConsumeBreak();
					continue;
				}

				if (this.Eof || (this.Column == 0 && this.IsDocumentBoundary()) || (this.Peek == '#' && this.IsComment()))
				{
					return false;
				}

				if (!inFlow && spaces <= parentIndent)
				{
					return false;
				}

				return true;
			}

			return false;
		}

		private bool LineStartsWithDocumentMarker()
		{
			if (this.lineStart + 3 > this.text.Length)
			{
				return false;
			}

			char marker = this.text[this.lineStart];
			if (marker is not ('-' or '.') || this.text[this.lineStart + 1] != marker || this.text[this.lineStart + 2] != marker)
			{
				return false;
			}

			char next = this.lineStart + 3 < this.text.Length ? this.text[this.lineStart + 3] : '\0';
			return next is ' ' or '\t' or '\r' or '\n' or '#' or '\0';
		}

		private bool LineAlreadyHasValueIndicator()
		{
			int k = this.lineStart;
			while (k < this.index)
			{
				char c = this.text[k];
				if (c is '"' or '\'')
				{
					k = this.SkipQuotedRegion(k, c);
					continue;
				}

				if (c is '&' or '*')
				{
					k++;
					while (k < this.index && !this.IsAnchorBreak(this.text[k]))
					{
						k++;
					}

					continue;
				}

				if (c == '!')
				{
					k = this.SkipTagRegion(k);
					continue;
				}

				if (c == ':')
				{
					char next = k + 1 < this.text.Length ? this.text[k + 1] : '\0';
					if (next is ' ' or '\t' or '\r' or '\n' or '\0')
					{
						return true;
					}
				}

				k++;
			}

			return false;
		}

		private int SkipQuotedRegion(int k, char quote)
		{
			k++;
			while (k < this.index)
			{
				if (this.text[k] == quote)
				{
					if (quote == '\'' && k + 1 < this.index && this.text[k + 1] == '\'')
					{
						k += 2;
						continue;
					}

					return k + 1;
				}

				if (quote == '"' && this.text[k] == '\\')
				{
					k += k + 1 < this.index ? 2 : 1;
					continue;
				}

				k++;
			}

			return k;
		}

		private int SkipTagRegion(int k)
		{
			k++;
			if (k < this.index && this.text[k] == '<')
			{
				k++;
				while (k < this.index && this.text[k] != '>')
				{
					k++;
				}

				return k < this.index ? k + 1 : k;
			}

			while (k < this.index && !this.IsTagBreak(this.text[k]))
			{
				k++;
			}

			return k;
		}

		private bool AtSpaceIndent()
		{
			for (int k = this.lineStart; k < this.index; k++)
			{
				if (this.text[k] != ' ')
				{
					return false;
				}
			}

			return true;
		}

		private bool RestOfLineIsWhitespace()
		{
			for (int k = this.index; k < this.text.Length; k++)
			{
				char c = this.text[k];
				if (c is '\n' or '\r')
				{
					return true;
				}

				if (c is not (' ' or '\t'))
				{
					return false;
				}
			}

			return true;
		}

		private void ConsumeMarker()
		{
			this.Advance();
			this.Advance();
			this.Advance();
		}

		private void ConsumeThroughBreak()
		{
			while (!this.Eof && !this.IsBreak())
			{
				this.Advance();
			}

			if (this.IsBreak())
			{
				this.ConsumeBreak();
			}
		}

		private string ReadAnchorName()
		{
			StringBuilder sb = new();
			while (!this.Eof && !this.IsAnchorBreak(this.Peek))
			{
				sb.Append(this.Peek);
				this.Advance();
			}

			return sb.ToString();
		}

		private string ReadTagSuffix(bool allowBang = false)
		{
			StringBuilder sb = new();
			while (!this.Eof && !this.IsTagBreak(this.Peek))
			{
				if (this.Peek == '!' && !allowBang)
				{
					break;
				}

				if (this.Peek == '!' && allowBang && sb.ToString().Contains('!', StringComparison.Ordinal))
				{
					break;
				}

				sb.Append(this.Peek);
				this.Advance();
			}

			return sb.ToString();
		}

		private string ReadNsWord()
		{
			StringBuilder sb = new();
			while (!this.Eof && this.Peek is not (' ' or '\t' or '\r' or '\n' or '#'))
			{
				sb.Append(this.Peek);
				this.Advance();
			}

			if (sb.Length == 0)
			{
				throw this.Error("A directive name is missing.");
			}

			return sb.ToString();
		}

		private bool IsTagHandle(string handle)
		{
			if (handle == "!" || handle == "!!")
			{
				return true;
			}

			if (handle.Length < 3 || handle[0] != '!' || handle[^1] != '!')
			{
				return false;
			}

			for (int i = 1; i < handle.Length - 1; i++)
			{
				if (!char.IsAsciiLetterOrDigit(handle[i]) && handle[i] != '-')
				{
					return false;
				}
			}

			return true;
		}

		private bool TrySplitVersion(string text, out int major, out int minor)
		{
			major = 0;
			minor = 0;
			int dot = text.IndexOf('.', StringComparison.Ordinal);
			if (dot <= 0 || dot != text.LastIndexOf('.'))
			{
				return false;
			}

			return int.TryParse(text.AsSpan(0, dot), out major)
				&& int.TryParse(text.AsSpan(dot + 1), out minor);
		}

		private bool IsTagBreak(char c) => c is '\0' or ' ' or '\t' or '\r' or '\n' or ',' or '[' or ']' or '{' or '}' or '#';

		private bool IsAnchorBreak(char c) => this.IsTagBreak(c);

		private void EnterDepth()
		{
			if (++this.depth > this.options.MaxDepth)
			{
				throw this.Error($"YAML nesting exceeds the maximum depth of {this.options.MaxDepth}.");
			}
		}

		private void LeaveDepth() => this.depth--;

		private void ResetDocumentState()
		{
			this.anchors.Clear();
			this.constructing.Clear();
			this.pendingAnchor = null;
			this.pendingTag = null;
			this.sawYamlDirective = false;
			this.directivesPending = false;
			this.declaredHandles.Clear();
			this.ResetHandles();
		}

		private void ResetHandles()
		{
			this.tagHandles.Clear();
			this.tagHandles["!"] = "!";
			this.tagHandles["!!"] = "tag:yaml.org,2002:";
		}

		private char PeekAt(int offset)
		{
			int at = this.index + offset;
			return at >= 0 && at < this.text.Length ? this.text[at] : '\0';
		}

		private bool IsBreak() => this.Peek is '\n' or '\r';

		private void Advance()
		{
			if (this.Eof)
			{
				return;
			}

			char c = this.text[this.index++];
			if (c == '\n' || c == '\r')
			{
				if (c == '\r' && !this.Eof && this.text[this.index] == '\n')
				{
					this.index++;
				}

				this.line++;
				this.lineStart = this.index;
			}
		}

		private void ConsumeBreak()
		{
			if (this.IsBreak())
			{
				this.Advance();
			}
		}

		private Mark Here() => new(this.index, this.line, this.lineStart);

		private void Restore(Mark mark)
		{
			this.index = mark.Index;
			this.line = mark.Line;
			this.lineStart = mark.LineStart;
		}

		private DecoderException Error(string message) => new($"Line {this.line}, column {this.Column + 1}: {message}");

		/// <summary>
		/// Compares mapping keys by resolved structure. The hash reuses a node's cached code, so an alias
		/// chain is not expanded into a string.
		/// </summary>
		/// <param name="session">The parse session that follows aliases.</param>
		private sealed class KeyComparer(Session session) : IEqualityComparer<YamlNode>
		{
			/// <inheritdoc/>
			public bool Equals(YamlNode? x, YamlNode? y) => x is not null && y is not null && session.SameKey(x, y);

			/// <inheritdoc/>
			public int GetHashCode(YamlNode obj) => session.KeyHash(obj);
		}
	}
}
