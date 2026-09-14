// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Conformance.Suites;

/// <summary>
/// Resolves the source-generated shapes the suites serialize, without reflection.
/// </summary>
internal static class Shapes
{
	/// <summary>
	/// Gets the shape a type provides for itself.
	/// </summary>
	/// <typeparam name="T">The self-describing type.</typeparam>
	/// <returns>The shape.</returns>
	internal static ITypeShape<T> Of<T>()
		where T : IShapeable<T> => T.GetTypeShape();

	/// <summary>
	/// Gets the shape a witness class provides for a type.
	/// </summary>
	/// <typeparam name="T">The type to describe.</typeparam>
	/// <typeparam name="TProvider">The witness that describes <typeparamref name="T"/>.</typeparam>
	/// <returns>The shape.</returns>
	internal static ITypeShape<T> Of<T, TProvider>()
		where TProvider : IShapeable<T> => TProvider.GetTypeShape();
}
