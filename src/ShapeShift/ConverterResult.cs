// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using static ShapeShift.ConverterResult;

namespace ShapeShift;

/// <summary>
/// The result of an operation that was to produce a <see cref="ShapeShiftConverter{T, TEncoder, TDecoder}"/>.
/// </summary>
/// <typeparam name="TEncoder"><inheritdoc cref="SerializerBase{TEncoder, TEncoder}" path="/typeparam[@name='TEncoder']"/></typeparam>
/// <typeparam name="TDecoder"><inheritdoc cref="SerializerBase{TEncoder, TDecoder}" path="/typeparam[@name='TDecoder']"/></typeparam>
[DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
internal class ConverterResult<TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	private readonly ShapeShiftConverter<TEncoder, TDecoder>? value;
	private readonly VisitorError? error;

	/// <summary>
	/// Initializes a new instance of the <see cref="ConverterResult{TEncoder, TDecoder}"/> class with a converter.
	/// </summary>
	/// <param name="value">The converter.</param>
	internal ConverterResult(ShapeShiftConverter<TEncoder, TDecoder> value)
	{
		this.value = value;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ConverterResult{TEncoder, TDecoder}"/> class that represents failure.
	/// </summary>
	/// <param name="error">The error.</param>
	internal ConverterResult(VisitorError error)
	{
		this.error = error;
	}

	/// <summary>
	/// Gets a value indicating whether the converter was created.
	/// </summary>
	[MemberNotNullWhen(true, nameof(Value))]
	[MemberNotNullWhen(false, nameof(Error))]
	internal bool Success => this.value is not null;

	/// <summary>
	/// Gets the converter, if successfully created.
	/// </summary>
	internal ShapeShiftConverter<TEncoder, TDecoder>? Value => this.value;

	/// <summary>
	/// Gets the converter.
	/// </summary>
	/// <exception cref="Exception">Thrown when the converter could not be created.</exception>
	internal ShapeShiftConverter<TEncoder, TDecoder> ValueOrThrow
	{
		get
		{
			this.error?.ThrowException();
			return this.value!;
		}
	}

	/// <summary>
	/// Gets the error encountered when trying to create the converter, if any.
	/// </summary>
	internal VisitorError? Error => this.error;

	/// <summary>
	/// Gets a  string used by the debugger to display the current result.
	/// </summary>
	private string DebuggerDisplay => this.Success ? $"Ok: {this.value}" : $"Err: {this.error}";

	/// <summary>
	/// Wraps a visitor failure as a failed result.
	/// </summary>
	/// <param name="error">The error describing the failure.</param>
	/// <returns>A failure result.</returns>
	internal static ConverterResult<TEncoder, TDecoder> Err(VisitorError error) => new(error);

	/// <summary>
	/// Wraps a failure result with additional context about the step that failed, if this result represents a failure.
	/// </summary>
	/// <param name="stepMessage">Additional context about the failure.</param>
	/// <param name="failureResult">Receives a new failure result that contains the original and additional context, if this result represents a failure.</param>
	/// <returns><see langword="true" /> if this result represents a failure; <see langword="false" /> otherwise.</returns>
	[MemberNotNullWhen(false, nameof(Value))]
	[MemberNotNullWhen(true, nameof(Error))]
	internal bool TryPrepareFailPath(string stepMessage, [NotNullWhen(true)] out ConverterResult<TEncoder, TDecoder>? failureResult)
	{
		if (this.Success)
		{
			failureResult = null;
			return false;
		}

		failureResult = new ConverterResult<TEncoder, TDecoder>(new VisitorError(stepMessage, this.Error));
		return true;
	}

	/// <inheritdoc cref="TryPrepareFailPath(string, out ConverterResult{TEncoder, TDecoder}?)" />
	/// <param name="shape">The shape that was being processed when the failure occurred.</param>
	/// <param name="failureResult"><inheritdoc cref="TryPrepareFailPath(string, out ConverterResult{TEncoder, TDecoder}?)" path="/param[@name='failureResult']" /></param>
	[MemberNotNullWhen(false, nameof(Value))]
	[MemberNotNullWhen(true, nameof(Error))]
	internal bool TryPrepareFailPath(ITypeShape shape, [NotNullWhen(true)] out ConverterResult<TEncoder, TDecoder>? failureResult)
	{
		if (this.Success)
		{
			failureResult = null;
			return false;
		}

		failureResult = Err(new VisitorError(shape.Type.FullName ?? shape.Type.Name, this.Error));
		return true;
	}

	/// <inheritdoc cref="TryPrepareFailPath(ITypeShape, out ConverterResult{TEncoder, TDecoder}?)" />
	/// <param name="shape">The shape that was being processed when the failure occurred.</param>
	/// <param name="failureResult"><inheritdoc cref="TryPrepareFailPath(string, out ConverterResult{TEncoder, TDecoder}?)" path="/param[@name='failureResult']" /></param>
	[MemberNotNullWhen(false, nameof(Value))]
	[MemberNotNullWhen(true, nameof(Error))]
	internal bool TryPrepareFailPath(IPropertyShape shape, [NotNullWhen(true)] out ConverterResult<TEncoder, TDecoder>? failureResult)
	{
		if (this.Success)
		{
			failureResult = null;
			return false;
		}

		failureResult = Err(new VisitorError($"{shape.DeclaringType.Type.FullName}.{shape.Name}", this.Error));
		return true;
	}

	/// <inheritdoc cref="TryPrepareFailPath(ITypeShape, out ConverterResult{TEncoder, TDecoder}?)" />
	[MemberNotNullWhen(false, nameof(Value))]
	[MemberNotNullWhen(true, nameof(Error))]
	internal bool TryPrepareFailPath(IParameterShape shape, [NotNullWhen(true)] out ConverterResult<TEncoder, TDecoder>? failureResult)
	{
		if (this.Success)
		{
			failureResult = null;
			return false;
		}

		failureResult = Err(new VisitorError($"{shape.Name}", this.Error));
		return true;
	}

	/// <inheritdoc cref="TryPrepareFailPath(ITypeShape, out ConverterResult{TEncoder, TDecoder}?)" />
	[MemberNotNullWhen(false, nameof(Value))]
	[MemberNotNullWhen(true, nameof(Error))]
	internal bool TryPrepareFailPath(IUnionCaseShape target, [NotNullWhen(true)] out ConverterResult<TEncoder, TDecoder>? failureResult)
	{
		if (this.Success)
		{
			failureResult = null;
			return false;
		}

		failureResult = Err(new VisitorError($"Union case: {target.UnionCaseType.Type.FullName ?? target.UnionCaseType.Type.Name}", this.Error));
		return true;
	}

	/// <inheritdoc cref="TryPrepareFailPath(ITypeShape, out ConverterResult{TEncoder, TDecoder}?)" />
	[MemberNotNullWhen(false, nameof(Value))]
	[MemberNotNullWhen(true, nameof(Error))]
	internal bool TryPrepareFailPath(IOptionalTypeShape shape, [NotNullWhen(true)] out ConverterResult<TEncoder, TDecoder>? failureResult)
	{
		if (this.Success)
		{
			failureResult = null;
			return false;
		}

		failureResult = Err(new VisitorError($"Optional: {shape.Type.FullName ?? shape.Type.Name}", this.Error));
		return true;
	}

	/// <inheritdoc cref="TryPrepareFailPath(ITypeShape, out ConverterResult{TEncoder, TDecoder}?)" />
	[MemberNotNullWhen(false, nameof(Value))]
	[MemberNotNullWhen(true, nameof(Error))]
	internal bool TryPrepareFailPath(ISurrogateTypeShape shape, [NotNullWhen(true)] out ConverterResult<TEncoder, TDecoder>? failureResult)
	{
		if (this.Success)
		{
			failureResult = null;
			return false;
		}

		failureResult = Err(new VisitorError($"surrogate {shape.SurrogateType.Type.FullName ?? shape.SurrogateType.Type.Name}", this.Error));
		return true;
	}

	/// <summary>
	/// Wraps the converter with one that provides reference preservation capabilities.
	/// </summary>
	/// <returns>A new result with the wrapped converter, or this same instance if it is already a failure.</returns>
	internal ConverterResult<TEncoder, TDecoder> WrapWithReferencePreservation()
	{
		return this.Success ? Ok(((IShapeShiftConverterInternal<TEncoder, TDecoder>)this.Value).WrapWithReferencePreservation()) : this;
	}
}

internal static class ConverterResult
{
	/// <summary>
	/// Wraps a converter as a successful result.
	/// </summary>
	/// <param name="value">The converter.</param>
	/// <returns>A successful result.</returns>
	internal static ConverterResult<TEncoder, TDecoder> Ok<TEncoder, TDecoder>(ShapeShiftConverter<TEncoder, TDecoder> value)
		where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
 => new(value);
}
