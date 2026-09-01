// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;

namespace ShapeShift.Tests;

/// <summary>
/// Verifies <see cref="IDecoder"/>'s own null contract, independently of any wire format.
/// </summary>
/// <remarks>
/// The per-format proof that a real decoder honors the contract lives in
/// <c>ShapeShift.Conformance</c>'s null suite, which every format's test project runs. What is left
/// over -- and what this file covers -- is the interface itself: the semantics
/// <see cref="IDecoder.TryReadNull"/> promises, and the default <see cref="IDecoder.ReadNull"/>
/// implementation that is expressed in terms of it.
/// </remarks>
public class DecoderNullContractTests : TestBase
{
	/// <summary>
	/// Verifies that a <see langword="true" /> answer consumes the null exactly once.
	/// </summary>
	/// <returns>A task tracking the assertions.</returns>
	[Test]
	public async Task TryReadNull_ConsumesOnTrue()
	{
		TokenListDecoder decoder = new([TokenType.Null, TokenType.Boolean]);

		bool answer = decoder.TryReadNull();

		await Assert.That(answer).IsTrue();
		await Assert.That(decoder.NextTokenType).IsEqualTo(TokenType.Boolean);
		await Assert.That(decoder.Position).IsEqualTo(1);
	}

	/// <summary>
	/// Verifies that a <see langword="false" /> answer consumes nothing, so the token is still
	/// available to whatever reads next.
	/// </summary>
	/// <returns>A task tracking the assertions.</returns>
	[Test]
	public async Task TryReadNull_ConsumesNothingOnFalse()
	{
		TokenListDecoder decoder = new([TokenType.String, TokenType.Null]);

		bool first = decoder.TryReadNull();
		bool second = decoder.TryReadNull();

		await Assert.That(first).IsFalse();
		await Assert.That(second).IsFalse();
		await Assert.That(decoder.NextTokenType).IsEqualTo(TokenType.String);
		await Assert.That(decoder.Position).IsEqualTo(0);
	}

	/// <summary>
	/// Verifies that a second call reports <see langword="false" />, which is the observable difference
	/// between the consuming contract and a peek.
	/// </summary>
	/// <returns>A task tracking the assertions.</returns>
	[Test]
	public async Task TryReadNull_IsNotAPeek()
	{
		TokenListDecoder decoder = new([TokenType.Null, TokenType.Null]);

		await Assert.That(decoder.TryReadNull()).IsTrue();
		await Assert.That(decoder.TryReadNull()).IsTrue();
		await Assert.That(decoder.TryReadNull()).IsFalse();
		await Assert.That(decoder.Position).IsEqualTo(2);
	}

	/// <summary>
	/// Verifies that <see cref="IDecoder.NextTokenType"/> remains the non-consuming peek.
	/// </summary>
	/// <returns>A task tracking the assertions.</returns>
	[Test]
	public async Task NextTokenType_RemainsThePeek()
	{
		TokenListDecoder decoder = new([TokenType.Null]);

		await Assert.That(decoder.NextTokenType).IsEqualTo(TokenType.Null);
		await Assert.That(decoder.NextTokenType).IsEqualTo(TokenType.Null);
		await Assert.That(decoder.Position).IsEqualTo(0);
	}

	/// <summary>
	/// Verifies that the default <see cref="IDecoder.ReadNull"/> implementation consumes, now that it
	/// is expressed in terms of the consuming <see cref="IDecoder.TryReadNull"/>.
	/// </summary>
	/// <returns>A task tracking the assertions.</returns>
	/// <remarks>
	/// The decoder used here deliberately does not declare <c>ReadNull</c>, so the call resolves to the
	/// interface's default implementation. That is only observable for a decoder declared as a class;
	/// a <see langword="ref" /> struct may not inherit a default interface method at all, which is why
	/// every first-party decoder declares one of its own.
	/// </remarks>
	[Test]
	public async Task DefaultReadNull_Consumes()
	{
		TokenListDecoder decoder = new([TokenType.Null, TokenType.Boolean]);

		((IDecoder)decoder).ReadNull();

		await Assert.That(decoder.Position).IsEqualTo(1);
		await Assert.That(decoder.NextTokenType).IsEqualTo(TokenType.Boolean);
	}

	/// <summary>
	/// Verifies that the default <see cref="IDecoder.ReadNull"/> implementation rejects a non-null with
	/// a <see cref="DecoderException"/> and consumes nothing.
	/// </summary>
	/// <returns>A task tracking the assertions.</returns>
	[Test]
	public async Task DefaultReadNull_RejectsNonNull()
	{
		TokenListDecoder decoder = new([TokenType.String]);
		IDecoder asInterface = decoder;

		await Assert.That(asInterface.ReadNull).Throws<DecoderException>();
		await Assert.That(decoder.Position).IsEqualTo(0);
	}

	/// <summary>
	/// Verifies that the contract composes the way converters rely on: consume the null and return, or
	/// leave the token untouched for the converter that is delegated to.
	/// </summary>
	/// <returns>A task tracking the assertions.</returns>
	[Test]
	public async Task DelegatingReadPattern_SeesTheUnconsumedToken()
	{
		TokenListDecoder present = new([TokenType.Boolean]);
		TokenListDecoder absent = new([TokenType.Null]);

		await Assert.That(ReadOptional(present)).IsEqualTo("value");
		await Assert.That(present.Position).IsEqualTo(1);
		await Assert.That(ReadOptional(absent)).IsNull();
		await Assert.That(absent.Position).IsEqualTo(1);

		static string? ReadOptional(TokenListDecoder decoder)
		{
			if (decoder.TryReadNull())
			{
				return null;
			}

			decoder.ReadBoolean();
			return "value";
		}
	}

	/// <summary>
	/// A minimal <see cref="IDecoder"/> over a list of token types, sufficient to observe the
	/// null-handling contract and nothing else.
	/// </summary>
	/// <remarks>
	/// It is a class rather than the conventional <see langword="ref" /> struct precisely so that the
	/// default interface implementations of <see cref="IDecoder.ReadNull"/> and friends are reachable.
	/// </remarks>
	/// <param name="tokens">The tokens the decoder will report, in order.</param>
	private sealed class TokenListDecoder(TokenType[] tokens) : IDecoder
	{
		/// <summary>
		/// Gets the number of tokens consumed so far.
		/// </summary>
		public int Position { get; private set; }

		/// <inheritdoc/>
		public TokenType NextTokenType => this.Position < tokens.Length ? tokens[this.Position] : TokenType.EndDocument;

		/// <inheritdoc/>
		public bool TryReadNull()
		{
			if (this.NextTokenType != TokenType.Null)
			{
				return false;
			}

			this.Position++;
			return true;
		}

		/// <inheritdoc/>
		public bool ReadBoolean() => this.Take(TokenType.Boolean) && true;

		/// <inheritdoc/>
		public int? ReadStartMap() => this.Take(TokenType.StartMap) ? null : null;

		/// <inheritdoc/>
		public void ReadEndMap() => this.Take(TokenType.EndMap);

		/// <inheritdoc/>
		public int? ReadStartVector() => this.Take(TokenType.StartVector) ? null : null;

		/// <inheritdoc/>
		public void ReadEndVector() => this.Take(TokenType.EndVector);

		/// <inheritdoc/>
		public ReadOnlySpan<char> ReadPropertyName() => this.Take(TokenType.PropertyName) ? default : default;

		/// <inheritdoc/>
		public void Skip() => this.Position++;

		/// <inheritdoc/>
		public long ReadInt64() => this.Take(TokenType.Number) ? 0 : 0;

		/// <inheritdoc/>
		public ulong ReadUInt64() => (ulong)this.ReadInt64();

		/// <inheritdoc/>
		public Int128 ReadInt128() => this.ReadInt64();

		/// <inheritdoc/>
		public UInt128 ReadUInt128() => this.ReadUInt64();

		/// <inheritdoc/>
		public Half ReadHalf() => (Half)this.ReadInt64();

		/// <inheritdoc/>
		public float ReadSingle() => this.ReadInt64();

		/// <inheritdoc/>
		public double ReadDouble() => this.ReadInt64();

		/// <inheritdoc/>
		public decimal ReadDecimal() => this.ReadInt64();

		/// <inheritdoc/>
		public DateTime ReadDateTime() => this.Take(TokenType.String) ? default : default;

		/// <inheritdoc/>
		public TimeSpan ReadTimeSpan() => this.Take(TokenType.String) ? default : default;

		/// <inheritdoc/>
		public BigInteger ReadBigInteger() => this.ReadInt64();

		/// <inheritdoc/>
		public string ReadString() => this.Take(TokenType.String) ? string.Empty : string.Empty;

		/// <inheritdoc/>
		public ReadOnlySpan<char> ReadCharSpan() => this.ReadString().AsSpan();

		private bool Take(TokenType expected)
		{
			if (this.NextTokenType != expected)
			{
				throw new DecoderException($"Expected {expected} but instead got {this.NextTokenType}.");
			}

			this.Position++;
			return true;
		}
	}
}
