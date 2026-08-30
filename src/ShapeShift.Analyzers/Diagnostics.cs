// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace ShapeShift.Analyzers;

/// <summary>
/// The stable catalog of diagnostics reported by the ShapeShift analyzers.
/// </summary>
/// <remarks>
/// <para>
/// Diagnostic IDs are permanent. An ID is never reused for a different meaning, and a retired ID is
/// left unused. Each ID has a dedicated documentation topic reachable through
/// <see cref="DiagnosticDescriptor.HelpLinkUri"/>.
/// </para>
/// <para>
/// None of these diagnostics are required for correct runtime behavior. ShapeShift throws actionable
/// exceptions for every condition described here; the analyzers simply move the feedback to build time.
/// </para>
/// </remarks>
public static class Diagnostics
{
	/// <summary>
	/// The category used for diagnostics about how ShapeShift APIs and attributes are applied.
	/// </summary>
	public const string UsageCategory = "ShapeShift.Usage";

	/// <summary>
	/// The category used for diagnostics about trimming, NativeAOT and other deployment risks.
	/// </summary>
	public const string ReliabilityCategory = "ShapeShift.Reliability";

	/// <summary>
	/// The base URL of the documentation topic published for each diagnostic ID.
	/// </summary>
	public const string HelpLinkBase = "https://aarnott.github.io/ShapeShift/docs/analyzers/";

	/// <summary>
	/// SHIFT001: the type named by <c>ShapeShiftConverterAttribute</c> is not a ShapeShift converter.
	/// </summary>
	public static readonly DiagnosticDescriptor ConverterTypeIsNotAConverter = Create(
		"SHIFT001",
		"Converter type is not a ShapeShift converter",
		"'{0}' does not derive from ShapeShiftConverter<T, TEncoder, TDecoder> and cannot be used as a ShapeShift converter",
		UsageCategory,
		DiagnosticSeverity.Error,
		"A type referenced by ShapeShiftConverterAttribute must derive from ShapeShiftConverter<T, TEncoder, TDecoder>. ShapeShift fails at run time when it tries to cast the activated instance.");

	/// <summary>
	/// SHIFT002: the converter type cannot be activated because it has no usable parameterless constructor.
	/// </summary>
	public static readonly DiagnosticDescriptor ConverterTypeIsNotActivatable = Create(
		"SHIFT002",
		"Converter type cannot be activated",
		"ShapeShift cannot activate the converter '{0}' because {1}",
		UsageCategory,
		DiagnosticSeverity.Error,
		"ShapeShift activates a converter named by ShapeShiftConverterAttribute through its public parameterless constructor. Add one, or register a converter instance or factory on the serializer instead.");

	/// <summary>
	/// SHIFT003: the converter converts a different data type than the annotated declaration.
	/// </summary>
	public static readonly DiagnosticDescriptor ConverterTypeConvertsDifferentType = Create(
		"SHIFT003",
		"Converter type converts a different data type",
		"'{0}' converts '{1}', which is not compatible with '{2}'",
		UsageCategory,
		DiagnosticSeverity.Error,
		"A converter named by ShapeShiftConverterAttribute must convert the annotated type, or the type of the annotated member or parameter. ShapeShift throws an invalid cast at run time otherwise.");

	/// <summary>
	/// SHIFT004: a type argument at a ShapeShift call site has no generated type shape.
	/// </summary>
	public static readonly DiagnosticDescriptor MissingGeneratedShape = Create(
		"SHIFT004",
		"Type has no generated shape",
		"'{0}' does not provide a source-generated shape for '{1}'; apply [GenerateShape] to '{1}' or pass a witness class annotated with [GenerateShapeFor<{1}>]",
		UsageCategory,
		DiagnosticSeverity.Warning,
		"ShapeShift APIs constrained to PolyType's IShapeable<T> require a source-generated shape. Apply [GenerateShape] to the type, or declare a witness class annotated with [GenerateShapeFor<T>] and pass it as the provider type argument.");

	/// <summary>
	/// SHIFT005: two members of a type map to the same wire name.
	/// </summary>
	public static readonly DiagnosticDescriptor AmbiguousWireName = Create(
		"SHIFT005",
		"Ambiguous serialized name",
		"'{0}' and '{1}' are both serialized as '{2}'",
		UsageCategory,
		DiagnosticSeverity.Warning,
		"Two members of the same type cannot share a serialized name. ShapeShift rejects the duplicate while building the converter or while reading the duplicated property.");

	/// <summary>
	/// SHIFT006: two members of a type collide once any ShapeShift naming policy is applied.
	/// </summary>
	/// <remarks>
	/// Reported at <see cref="DiagnosticSeverity.Info"/> because the collision only matters to
	/// serializers that set a <c>PropertyNamingPolicy</c>, which is chosen at run time.
	/// </remarks>
	public static readonly DiagnosticDescriptor AmbiguousWireNameUnderNamingPolicy = Create(
		"SHIFT006",
		"Ambiguous serialized name under a naming policy",
		"'{0}' and '{1}' differ only by letter casing, so they both serialize as '{2}' once a ShapeShift naming policy is applied",
		UsageCategory,
		DiagnosticSeverity.Info,
		"Every built-in ShapeShiftNamingPolicy normalizes letter casing, so members whose names differ only by casing collide when a naming policy is configured on the serializer.");

	/// <summary>
	/// SHIFT007: reflection-based converter or shape activation is in use.
	/// </summary>
	public static readonly DiagnosticDescriptor ReflectionActivationRequiresOptIn = Create(
		"SHIFT007",
		"Reflection-based activation is not trimming or NativeAOT safe",
		"'{0}' activates types through reflection; prefer converter instances, converter factories and source-generated shapes in trimmed or NativeAOT applications",
		ReliabilityCategory,
		DiagnosticSeverity.Info,
		"Runtime activation of converter types and reflection-derived type shapes is an explicit opt-in that is not trimming or NativeAOT safe. Suppress this diagnostic where the opt-in is intentional.");

	/// <summary>
	/// SHIFT008: the type declares a contract that ShapeShift cannot represent.
	/// </summary>
	public static readonly DiagnosticDescriptor UnsupportedContract = Create(
		"SHIFT008",
		"Unsupported ShapeShift contract",
		"{0}",
		UsageCategory,
		DiagnosticSeverity.Error,
		"ShapeShift cannot build a converter for this contract and throws while preparing the converter graph.");

	/// <summary>
	/// Gets every descriptor in this catalog.
	/// </summary>
	/// <returns>The complete, stable diagnostic catalog.</returns>
	public static ImmutableArray<DiagnosticDescriptor> GetAll() =>
	[
		ConverterTypeIsNotAConverter,
		ConverterTypeIsNotActivatable,
		ConverterTypeConvertsDifferentType,
		MissingGeneratedShape,
		AmbiguousWireName,
		AmbiguousWireNameUnderNamingPolicy,
		ReflectionActivationRequiresOptIn,
		UnsupportedContract,
	];

	private static DiagnosticDescriptor Create(
		string id,
		string title,
		string messageFormat,
		string category,
		DiagnosticSeverity severity,
		string description,
		bool isEnabledByDefault = true)
		=> new(
			id,
			title,
			messageFormat,
			category,
			severity,
			isEnabledByDefault,
			description,
			$"{HelpLinkBase}{id}.html");
}
