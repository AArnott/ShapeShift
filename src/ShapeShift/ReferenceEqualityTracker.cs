// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft;
using Microsoft.NET.StringTools;

namespace ShapeShift;

/// <summary>
/// Tracks the state for a particular serialization/deserialization operation to preserve object references.
/// </summary>
internal class ReferenceEqualityTracker<TEncoder, TDecoder> : IPoolableObject
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	private readonly Dictionary<object, (int, bool)> serializedObjects = new(ReferenceEqualityComparer.Instance);
	private readonly List<object?> deserializedObjects = new();
	private int serializingObjectCounter;

	/// <inheritdoc/>
	public IShapeShiftSerializer? Owner
	{
		get => field;
		set => field = value;
	}

	/// <summary>
	/// Gets the active preservation mode.
	/// </summary>
	private ReferencePreservationMode Mode => (this.Owner as IReferencePreservingSerializer<TEncoder, TDecoder>)?.PreserveReferences ?? throw new InvalidOperationException("No owner set.");

	/// <inheritdoc/>
	void IPoolableObject.Recycle()
	{
		this.serializingObjectCounter = 0;
		this.serializedObjects.Clear();
		this.deserializedObjects.Clear();
	}

	/// <summary>
	/// Writes an object to the stream, replacing the object with a reference if the object has been seen in this serialization before.
	/// </summary>
	/// <typeparam name="T">The type of value to be written.</typeparam>
	/// <param name="writer">The writer.</param>
	/// <param name="value">The object to write.</param>
	/// <param name="inner">The converter to use to write the object if it has not already been written.</param>
	/// <param name="context">The serialization context.</param>
	internal void WriteObject<T>(ref TEncoder writer, T value, ShapeShiftConverter<T, TEncoder, TDecoder> inner, SerializationContext<TEncoder, TDecoder> context)
	{
		Requires.NotNullAllowStructs(value);
		Verify.Operation(this.Owner is not null, $"{nameof(this.Owner)} must be set before use.");

		if (this.Owner.InternStrings && value is string)
		{
			value = (T)(object)Strings.WeakIntern((string)(object)value);
		}

		if (this.TryGetSerializedObject(value, out int referenceId))
		{
			// This object has already been written. Skip it this time.
			((IReferencePreservingSerializer<TEncoder, TDecoder>)this.Owner).WriteObjectReference(ref writer, referenceId, context);
		}
		else
		{
			int assignedIndex = this.serializingObjectCounter++;
			this.serializedObjects.Add(value, (assignedIndex, false));
			inner.Write(ref writer, value, context);
			this.serializedObjects[value] = (assignedIndex, true);
		}
	}

	/// <summary>
	/// Reads an object or its reference from the stream.
	/// </summary>
	/// <typeparam name="T">The type of object to read.</typeparam>
	/// <param name="reader">The reader.</param>
	/// <param name="inner">The converter to use to deserialize the object if it is not a reference.</param>
	/// <param name="context">The serialization context.</param>
	/// <returns>The reference to an object, whether it was deserialized fresh or just referenced.</returns>
	/// <exception cref="ShapeShiftSerializationException">Thrown if there is a dependency cycle detected or the <paramref name="inner"/> converter returned null unexpectedly.</exception>
	internal T ReadObject<T>(ref TDecoder reader, ShapeShiftConverter<T, TEncoder, TDecoder> inner, SerializationContext<TEncoder, TDecoder> context)
	{
		Verify.Operation(this.Owner is not null, $"{nameof(this.Owner)} must be set before use.");


		if (((IReferencePreservingSerializer<TEncoder, TDecoder>)this.Owner).TryReadObjectReference(ref reader, out int referenceId, context))
		{
			return (T?)this.GetDeserializedObject(referenceId)!;
		}

		// Reserve our position in the array.
		context.ReferenceIndex = this.deserializedObjects.Count;
		this.deserializedObjects.Add(null);
		T value = inner.Read(ref reader, context) ?? throw new ShapeShiftSerializationException("Converter returned null for non-null value.");
		this.deserializedObjects[context.ReferenceIndex] = value;
		return value;
	}

	/// <summary>
	/// Reports the construction of an object and stores it if cycles are allowed.
	/// </summary>
	/// <param name="value">The constructed object to be reported and stored for reference.</param>
	/// <param name="referenceIndex">Indicates the position in the collection where the object should be stored.</param>
	internal void ReportObjectConstructed(object? value, int referenceIndex)
	{
		if (this.Mode == ReferencePreservationMode.AllowCycles)
		{
			Verify.Operation(this.deserializedObjects[referenceIndex] is null, "The object was already constructed and should not be reported again.");
			this.deserializedObjects[referenceIndex] = value;
		}
	}

	private object? GetDeserializedObject(int id)
	{
		if (this.deserializedObjects[id] is object result)
		{
			// No cycle detected.
			return result;
		}
		else if (this.Mode == ReferencePreservationMode.AllowCycles)
		{
			// Reference cycle detected and allowed.
			// But we don't have the object yet, because the converter responsible for creating it has not or cannot report the object's reference back (yet).
			// This may be because the object has a non-default constructor (or "required" properties) and
			// and the cycle implicates the properties that must be constructed before the object itself.
			throw new NotSupportedException("A reference cycle cannot be reconstructed due to a limitation in the converters or the data types themselves.");
		}
		else
		{
			// Reference cycle detected and not allowed.
			throw new ShapeShiftSerializationException("Disallowed reference cycle detected.");
		}
	}

	private bool TryGetSerializedObject(object value, out int referenceId)
	{
		if (!this.serializedObjects.TryGetValue(value, out (int ReferenceId, bool Done) slot))
		{
			referenceId = 0;
			return false;
		}

		if (!slot.Done && this.Mode != ReferencePreservationMode.AllowCycles)
		{
			throw new ShapeShiftSerializationException("Disallowed reference cycle detected.");
		}

		referenceId = slot.ReferenceId;
		return true;
	}

	/// <summary>
	/// An <see cref="IEqualityComparer{T}"/> that explicitly disregards any chance of by-value equality or hash code computation,
	/// since we very explicitly want to preserve <em>references</em> without accidentally combining two distinct objects that happen to be considered equal.
	/// </summary>
	private class ReferenceEqualityComparer : IEqualityComparer<object>
	{
		internal static readonly ReferenceEqualityComparer Instance = new();

		private ReferenceEqualityComparer()
		{
		}

		public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

		public int GetHashCode([DisallowNull] object obj) => RuntimeHelpers.GetHashCode(obj);
	}
}
