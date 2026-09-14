// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift;

/// <summary>
/// Marks a <see cref="Dictionary{TKey, TValue}"/> of string to <see cref="ShapeShiftValue"/>
/// that captures unknown properties and writes them back as peer properties.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class ShapeShiftExtensionDataAttribute : Attribute;
