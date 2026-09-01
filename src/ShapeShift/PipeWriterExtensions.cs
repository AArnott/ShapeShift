// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift;

/// <summary>
/// Extension methods for <see cref="PipeWriter"/>.
/// </summary>
public static class PipeWriterExtensions
{
	/// <summary>
	/// Flushes a <see cref="PipeWriter"/>, translating a canceled flush into an <see cref="OperationCanceledException"/>.
	/// </summary>
	/// <param name="writer">The writer to flush.</param>
	/// <param name="cancellationToken">
	/// The cancellation token to throw from, when the flush reports <see cref="FlushResult.IsCanceled"/>.
	/// </param>
	/// <returns>A task that represents the asynchronous flush operation.</returns>
	/// <remarks>
	/// <see cref="PipeWriter.FlushAsync(CancellationToken)"/> reports cancellation through the returned
	/// <see cref="FlushResult"/> rather than by throwing, which is easy to overlook. This method restores the more
	/// familiar throwing behavior expected of asynchronous APIs elsewhere in .NET.
	/// </remarks>
	public static async ValueTask FlushAndThrowIfCanceledAsync(this PipeWriter writer, CancellationToken cancellationToken)
	{
		Requires.NotNull(writer);
		FlushResult result = await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
		if (result.IsCanceled)
		{
			throw new OperationCanceledException(cancellationToken);
		}
	}
}
