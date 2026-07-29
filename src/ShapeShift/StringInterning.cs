// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ShapeShift;

/// <summary>
/// A strong-reference string interning collection.
/// </summary>
internal class StringInterning : IPoolableObject
{
	private const int InitialCapacity = 32;
	private const uint HashCollisionThreshold = 100;

	private int[]? buckets;
	private Entry[]? entries;
	private bool useSecureHash;
	private int count;

	/// <inheritdoc/>
	IShapeShiftSerializer? IPoolableObject.Owner { get; set; }

	/// <inheritdoc/>
	void IPoolableObject.Recycle() => this.Clear();

	/// <summary>
	/// Clears the cache of any strong references to interned strings.
	/// </summary>
	internal void Clear()
	{
		if (this.count > 0)
		{
			Array.Clear(this.buckets!);
			Array.Clear(this.entries!, 0, this.count);
			this.useSecureHash = false;
			this.count = 0;
		}
	}

	/// <summary>
	/// Returns an interned string for a given character span.
	/// </summary>
	/// <param name="value">The characters for which an interned string is required.</param>
	/// <returns>The interned string.</returns>
	internal string Intern(scoped ReadOnlySpan<char> value) => this.GetOrAdd(value, candidateValue: null);

	/// <summary>
	/// Returns an interned string, reusing the supplied string when it is first added.
	/// </summary>
	/// <param name="value">The string for which an interned string is required.</param>
	/// <returns>The interned string.</returns>
	internal string Intern(string value) => this.GetOrAdd(value.AsSpan(), value);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static uint CalculateHashCode(scoped ReadOnlySpan<char> value, bool secureHash)
	{
		return unchecked((uint)string.GetHashCode(value, StringComparison.Ordinal));
	}

	private string GetOrAdd(scoped ReadOnlySpan<char> value, string? candidateValue)
	{
		if (this.buckets is null)
		{
			this.Initialize(InitialCapacity);
		}

		Entry[] entries = this.entries!;
		uint hashCode = CalculateHashCode(value, this.useSecureHash);
		ref int bucket = ref this.GetBucket(hashCode);
		uint collisionCount = 0;

		for (int probeIndex = bucket - 1; (uint)probeIndex < (uint)entries.Length; probeIndex = entries[probeIndex].Next)
		{
			ref Entry entry = ref entries[probeIndex];
			if (entry.HashCode == hashCode && entry.Value.AsSpan().SequenceEqual(value))
			{
				return entry.Value;
			}

			if (!this.useSecureHash && ++collisionCount > HashCollisionThreshold)
			{
				this.SwitchToSecureHashing();
				return this.GetOrAdd(value, candidateValue);
			}
		}

		if (this.count == entries.Length)
		{
			this.Resize(this.ExpandPrime(this.count));
			entries = this.entries!;
			bucket = ref this.GetBucket(hashCode);
		}

		string interned = candidateValue ?? value.ToString();
		ref Entry newEntry = ref entries[this.count];
		newEntry.HashCode = hashCode;
		newEntry.Next = bucket - 1;
		newEntry.Value = interned;
		bucket = ++this.count;
		return interned;
	}

	private void SwitchToSecureHashing()
	{
		Debug.Assert(!this.useSecureHash, "This method should only be called once.");
		this.useSecureHash = true;
		Array.Clear(this.buckets!);

		for (int i = 0; i < this.count; i++)
		{
			ref Entry entry = ref this.entries![i];
			entry.HashCode = CalculateHashCode(entry.Value.AsSpan(), secureHash: true);
			ref int bucket = ref this.GetBucket(entry.HashCode);
			entry.Next = bucket - 1;
			bucket = i + 1;
		}
	}

	private void Initialize(int capacity)
	{
		int size = this.GetPrimeGreaterThan(capacity);
		this.buckets = new int[size];
		this.entries = new Entry[size];
	}

	private void Resize(int newSize)
	{
		Entry[] newEntries = new Entry[newSize];
		Array.Copy(this.entries!, newEntries, this.count);
		this.entries = newEntries;
		this.buckets = new int[newSize];

		for (int i = 0; i < this.count; i++)
		{
			ref Entry entry = ref newEntries[i];
			ref int bucket = ref this.GetBucket(entry.HashCode);
			entry.Next = bucket - 1;
			bucket = i + 1;
		}
	}

	private ref int GetBucket(uint hashCode) => ref this.buckets![(uint)(hashCode % this.buckets!.Length)];

	private int ExpandPrime(int oldSize) => this.GetPrimeGreaterThan(checked(oldSize * 2));

	private int GetPrimeGreaterThan(int minimum)
	{
		for (int candidate = minimum | 1; candidate < int.MaxValue; candidate += 2)
		{
			if (this.IsPrime(candidate))
			{
				return candidate;
			}
		}

		throw new OverflowException();
	}

	private bool IsPrime(int candidate)
	{
		for (int divisor = 3; divisor <= candidate / divisor; divisor += 2)
		{
			if (candidate % divisor == 0)
			{
				return false;
			}
		}

		return candidate > 1;
	}

	private struct Entry
	{
		internal uint HashCode;
		internal int Next;
		internal string Value;
	}
}
