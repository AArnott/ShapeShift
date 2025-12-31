// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift;

internal static class PolyTypeExtensions
{
	internal static object GetOrAddOrThrow(this MultiProviderTypeCache cache, Type type, ITypeShapeProvider provider)
		=> cache.GetOrAdd(type, provider) ?? throw ThrowMissingTypeShape(type, provider);

	private static Exception ThrowMissingTypeShape(Type type, ITypeShapeProvider provider)
		=> new ArgumentException($"The {provider.GetType().FullName} provider had no type shape for {type.FullName}.", nameof(provider));
}
