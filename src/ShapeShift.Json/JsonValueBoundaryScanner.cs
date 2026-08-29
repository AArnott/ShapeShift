// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Json;

/// <summary>
/// Recognizes the boundary of one complete, top-level JSON value using <see cref="Utf8JsonReader"/>'s
/// own incremental parsing support, without buffering more than that one value requires.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Utf8JsonReader"/> already supports incremental parsing of a growing buffer via
/// <see cref="JsonReaderState"/> resumption, so this scanner does not reimplement any JSON grammar. It only
/// drives the reader through exactly the calls required to recognize "one value has been fully read", which
/// takes a small amount of care:
/// </para>
/// <list type="bullet">
/// <item>
/// <see cref="Utf8JsonReader.TrySkip()"/> requires the reader to already be positioned on a token; called on a
/// reader that has never read a token (<see cref="JsonTokenType.None"/>) it returns <see langword="true"/>
/// immediately without consuming anything, which would look like "boundary found at offset zero" if called
/// blindly. So this type calls <see cref="Utf8JsonReader.Read()"/> first to position on the value's first token,
/// and only afterward considers calling <see cref="Utf8JsonReader.TrySkip()"/>.
/// </item>
/// <item>
/// Once positioned, only <see cref="JsonTokenType.StartObject"/> and <see cref="JsonTokenType.StartArray"/>
/// require a further <see cref="Utf8JsonReader.TrySkip()"/> call to consume the rest of the container; every
/// other token type is itself already a complete, self-contained value after the single <see cref="Utf8JsonReader.Read()"/>.
/// </item>
/// <item>
/// Resuming a saved <see cref="JsonReaderState"/> correctly restores <see cref="Utf8JsonReader.TokenType"/>, so
/// the same two checks above can simply be repeated on every call without tracking extra state of our own.
/// </item>
/// </list>
/// </remarks>
public sealed class JsonValueBoundaryScanner : IValueBoundaryScanner
{
	private readonly JsonReaderOptions options;
	private JsonReaderState state;

	/// <summary>
	/// The number of bytes, counted from the start of the buffer most recently passed to <see cref="TryScan"/>,
	/// that have already been fed to a <see cref="Utf8JsonReader"/> in a previous call for the value currently
	/// in progress.
	/// </summary>
	/// <remarks>
	/// This is tracked separately from the <c>examined</c> position <see cref="TryScan"/> reports: bytes that
	/// have been fed to the reader but belong to a value still in progress (e.g. already-skipped children of an
	/// open container) can never be re-fed to <see cref="Utf8JsonReader"/> -- its resumable
	/// <see cref="JsonReaderState"/> does not expect to see them again -- but they also cannot yet be released
	/// back to the caller's underlying <see cref="System.IO.Pipelines.PipeReader"/>, since the eventual decode
	/// step still needs them. This field lets the two be tracked independently: it always advances whenever the
	/// reader makes progress, while <c>examined</c> only advances when it is additionally safe to discard those
	/// bytes entirely (see <see cref="TryScan"/>).
	/// </remarks>
	private long consumed;

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonValueBoundaryScanner"/> class.
	/// </summary>
	/// <param name="options">The options that govern how the JSON is tokenized (e.g. comment handling, trailing commas).</param>
	public JsonValueBoundaryScanner(JsonReaderOptions options = default)
	{
		this.options = options;
		this.state = new JsonReaderState(options);
	}

	/// <inheritdoc/>
	public bool TryScan(in ReadOnlySequence<byte> buffer, bool isFinalBlock, out SequencePosition end, out SequencePosition examined)
	{
		ReadOnlySequence<byte> remainder = this.consumed == 0 ? buffer : buffer.Slice(this.consumed);
		Utf8JsonReader reader = new(remainder, isFinalBlock, this.state);

		if (reader.TokenType == JsonTokenType.None)
		{
			// A reader positioned at JsonTokenType.None with nothing left to examine is ambiguous between two very
			// different situations: (1) genuinely no further value begins here (e.g. we're at the clean end of an
			// NDJSON sequence, or the whole input was empty), or (2) more bytes simply haven't arrived yet. Either
			// way, there is nothing productive to do until the caller decides (from isFinalBlock together with
			// whether any bytes exist at all) whether no further value is coming. Critically, we must not call
			// Read() here: with isFinalBlock true and a truly empty buffer, Utf8JsonReader.Read() throws
			// JsonException("The input does not contain any JSON tokens...") instead of returning false, which
			// would incorrectly fail a graceful end-of-sequence rather than merely reporting "no boundary found yet".
			if (remainder.IsEmpty)
			{
				end = default;
				examined = buffer.GetPosition(this.consumed);
				return false;
			}

			if (!reader.Read())
			{
				// Read() advances past any insignificant whitespace even when it fails to find a token, and
				// (per Utf8JsonReader's documented incremental-parsing contract) never commits to any part of a
				// token it cannot yet fully recognize -- so BytesConsumed here can only ever cover whitespace
				// that precedes the value, never any of the value's own bytes. That makes it always safe to
				// release: unlike the branch below, no partially-scanned value content is ever discarded.
				this.state = reader.CurrentState;
				this.consumed += reader.BytesConsumed;
				end = default;
				examined = buffer.GetPosition(this.consumed);

				// Everything up to `examined` has now been released to the caller, so the next call's buffer
				// will already start there; our own offset into that (new) buffer resets to zero accordingly.
				this.consumed = 0;
				return false;
			}
		}

		if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray && !reader.TrySkip())
		{
			// Unlike the branch above, a value has definitely started here (we're mid-container), and the
			// eventual decode() call still needs every byte of it -- including the portion already walked by
			// TrySkip() -- so nothing may be released to the caller yet. We must still remember how far the
			// reader itself has progressed, though, so the next call doesn't re-feed it bytes it has already
			// consumed (which JsonReaderState resumption does not expect).
			this.state = reader.CurrentState;
			this.consumed += reader.BytesConsumed;
			end = default;
			examined = buffer.Start;
			return false;
		}

		end = buffer.GetPosition(this.consumed + reader.BytesConsumed);
		examined = end;

		// Reset state so this instance is ready to scan the next value (e.g. for NDJSON-style sequences).
		this.state = new JsonReaderState(this.options);
		this.consumed = 0;
		return true;
	}
}
