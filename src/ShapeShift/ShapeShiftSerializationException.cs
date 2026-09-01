// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift;

/// <summary>
/// The exception thrown when a value cannot be serialized or deserialized.
/// </summary>
/// <remarks>
/// <para>
/// ShapeShift augments these exceptions with a <see cref="Path"/> breadcrumb trail as the failure propagates
/// out through the converters that were responsible for each enclosing map property or vector element.
/// The path is rendered into <see cref="Message"/> so that logs identify precisely which value failed,
/// even when the failure originates deep inside a large object graph.
/// </para>
/// <para>
/// Exceptions thrown by user code (including custom converters) are never swallowed:
/// they are preserved as the <see cref="Exception.InnerException"/> of the exception that carries the path.
/// </para>
/// <para>
/// Instances of this type are not thread-safe. A single (de)serialization operation is expected to
/// augment the path on the thread that is unwinding the failure.
/// </para>
/// </remarks>
public class ShapeShiftSerializationException : Exception
{
	/// <summary>
	/// The path elements recorded so far, ordered from the innermost (deepest) element to the outermost.
	/// </summary>
	private List<ShapeShiftPathElement>? reversedPath;

	/// <summary>
	/// Initializes a new instance of the <see cref="ShapeShiftSerializationException"/> class.
	/// </summary>
	/// <param name="message">The message that describes the failure.</param>
	public ShapeShiftSerializationException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ShapeShiftSerializationException"/> class.
	/// </summary>
	/// <param name="message">The message that describes the failure.</param>
	/// <param name="innerException">The exception that caused this failure, if any.</param>
	public ShapeShiftSerializationException(string message, Exception? innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ShapeShiftSerializationException"/> class
	/// with an initial <see cref="Path"/>.
	/// </summary>
	/// <param name="message">The message that describes the failure.</param>
	/// <param name="innerException">The exception that caused this failure, if any.</param>
	/// <param name="path">The location of the value that failed, relative to the root of the document being processed.</param>
	public ShapeShiftSerializationException(string message, Exception? innerException, ShapeShiftPath path)
		: base(message, innerException)
	{
		this.reversedPath = new(path.Count);
		for (int i = path.Count - 1; i >= 0; i--)
		{
			this.reversedPath.Add(path[i]);
		}
	}

	/// <summary>
	/// Gets the location of the value that failed to (de)serialize, relative to the root of the document.
	/// </summary>
	/// <value>
	/// <see cref="ShapeShiftPath.Root"/> when the failure was not attributable to a particular value
	/// within a map or vector.
	/// </value>
	public ShapeShiftPath Path
	{
		get
		{
			if (this.reversedPath is not { Count: > 0 } reversed)
			{
				return ShapeShiftPath.Root;
			}

			ShapeShiftPathElement[] elements = new ShapeShiftPathElement[reversed.Count];
			for (int i = 0; i < elements.Length; i++)
			{
				elements[i] = reversed[elements.Length - 1 - i];
			}

			return new ShapeShiftPath(elements);
		}
	}

	/// <summary>
	/// Gets the message that describes the failure, including the <see cref="Path"/> when one is known.
	/// </summary>
	public override string Message
		=> this.reversedPath is { Count: > 0 } ? $"{base.Message} Path: {this.Path}." : base.Message;

	/// <summary>
	/// Records that this failure occurred within the value identified by <paramref name="element"/>
	/// of an enclosing map or vector, prepending it to <see cref="Path"/>.
	/// </summary>
	/// <param name="element">The step from the enclosing container to the value that was being processed.</param>
	/// <returns>Always <see langword="true" />, so that this method may be invoked from an exception filter that rethrows.</returns>
	/// <remarks>
	/// Converters call this method as an exception propagates outward so that the outermost frame
	/// observes a complete path from the root of the document.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// try
	/// {
	///     this.elementConverter.Write(ref encoder, element, context);
	/// }
	/// catch (ShapeShiftSerializationException ex) when (ex.AddEnclosingPathElement(index))
	/// {
	///     throw;
	/// }
	/// ]]></code>
	/// </example>
	public bool AddEnclosingPathElement(ShapeShiftPathElement element)
	{
		(this.reversedPath ??= []).Add(element);
		return true;
	}
}
