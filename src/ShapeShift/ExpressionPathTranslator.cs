// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq.Expressions;
using ShapeShift.Schema;

namespace ShapeShift;

/// <summary>
/// Translates a LINQ expression that walks CLR members into the <see cref="ShapeShiftPath"/> that locates
/// the same value within a serialized document.
/// </summary>
/// <remarks>
/// The expression is only ever <em>inspected</em>: no part of it is compiled, interpreted, or otherwise
/// executed, and no member is resolved by name at runtime. The translation is therefore safe for trimming
/// and NativeAOT.
/// </remarks>
internal static class ExpressionPathTranslator
{
	/// <summary>
	/// Translates an expression into the path that locates the value it selects.
	/// </summary>
	/// <param name="lambda">The expression, whose single parameter represents the root of the document.</param>
	/// <param name="rootContract">The contract describing how the root of the document is written.</param>
	/// <param name="parameterName">The name of the public API parameter that supplied <paramref name="lambda"/>.</param>
	/// <returns>The path.</returns>
	/// <exception cref="ArgumentException">Thrown when the expression names something that is not part of the serialized contract.</exception>
	/// <exception cref="NotSupportedException">Thrown when the expression uses a construct or reaches a contract that has no path equivalent.</exception>
	internal static ShapeShiftPath Translate(LambdaExpression lambda, DataContract rootContract, string parameterName)
	{
		Walker walker = new(lambda, rootContract, parameterName);
		walker.Visit(lambda.Body);
		return new ShapeShiftPath(CollectionsMarshal.AsSpan(walker.Elements));
	}

	/// <summary>
	/// Accumulates path elements while descending from the root of an expression to the value it selects.
	/// </summary>
	/// <param name="lambda">The whole expression, which makes diagnostics self-explanatory.</param>
	/// <param name="rootContract">The contract for the expression's parameter.</param>
	/// <param name="parameterName">The name of the public API parameter that supplied the expression.</param>
	private sealed class Walker(LambdaExpression lambda, DataContract rootContract, string parameterName)
	{
		/// <summary>
		/// Gets the path elements collected so far, ordered from the root of the document toward the value.
		/// </summary>
		internal List<ShapeShiftPathElement> Elements { get; } = new();

		/// <summary>
		/// Appends the elements that reach <paramref name="expression"/> and reports the contract of its value.
		/// </summary>
		/// <param name="expression">The expression step to translate.</param>
		/// <returns>The contract describing the value that <paramref name="expression"/> selects.</returns>
		internal DataContract Visit(Expression expression)
			=> expression switch
			{
				ParameterExpression parameter => this.VisitParameter(parameter),
				UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked or ExpressionType.TypeAs } conversion => this.VisitConversion(conversion),
				MemberExpression member => this.VisitMember(member),
				BinaryExpression { NodeType: ExpressionType.ArrayIndex } arrayIndex => this.VisitIndexer(arrayIndex, arrayIndex.Left, arrayIndex.Right),
				IndexExpression { Object: not null, Arguments.Count: 1 } index => this.VisitIndexer(index, index.Object, index.Arguments[0]),
				MethodCallExpression { Object: not null, Arguments.Count: 1, Method: { Name: "get_Item", IsSpecialName: true } } call => this.VisitIndexer(call, call.Object, call.Arguments[0]),
				MethodCallExpression call => throw this.Unsupported(call, $"Calling '{call.Method.Name}' would require running code to decide where the value lives. Only member access and constant indexers name a fixed location."),
				_ => throw this.Unsupported(expression, $"{expression.NodeType} expressions have no equivalent in a {nameof(ShapeShiftPath)}."),
			};

		/// <summary>
		/// Peels away optionality, which is never a step of its own within a document.
		/// </summary>
		/// <param name="contract">The contract to unwrap.</param>
		/// <returns>The contract of the value when it is present.</returns>
		private static DataContract Unwrap(DataContract contract)
		{
			while (contract is OptionalContract optional)
			{
				contract = optional.ElementType;
			}

			return contract;
		}

		/// <summary>
		/// Reports whether a member access is <see cref="Nullable{T}.Value"/>, which selects the same
		/// location as the expression it is applied to.
		/// </summary>
		/// <param name="member">The member access to test.</param>
		/// <returns><see langword="true" /> when the access may simply be stepped over.</returns>
		private static bool IsNullableValueAccess(MemberExpression member)
			=> member.Member.Name == "Value"
				&& member.Member.DeclaringType is { IsGenericType: true } declaringType
				&& declaringType.GetGenericTypeDefinition() == typeof(Nullable<>);

		/// <summary>
		/// Reports whether a conversion leaves both the location in the document and the contract that
		/// describes it unchanged.
		/// </summary>
		/// <param name="conversion">The conversion to test.</param>
		/// <returns><see langword="true" /> when the conversion may simply be stepped over.</returns>
		/// <remarks>
		/// Boxing and widening reference conversions are transparent because the operand's contract still
		/// describes the value. A narrowing (downcast), numeric, or user-defined conversion is not, because
		/// the members available afterward belong to a contract this translator has no way to reach.
		/// </remarks>
		private static bool IsTransparentConversion(UnaryExpression conversion)
		{
			if (conversion.Method is not null)
			{
				return false;
			}

			Type from = conversion.Operand.Type;
			Type to = conversion.Type;
			return from == to
				|| to.IsAssignableFrom(from)
				|| Nullable.GetUnderlyingType(to) == from
				|| Nullable.GetUnderlyingType(from) == to;
		}

		/// <summary>
		/// Describes the value of a constant for a diagnostic message.
		/// </summary>
		/// <param name="constant">The constant to describe.</param>
		/// <returns>The description.</returns>
		private static string DescribeConstant(ConstantExpression constant)
			=> constant.Value is null ? "null" : $"of type {constant.Value.GetType().FullName}";

		/// <summary>
		/// Describes a contract for a diagnostic message, calling out the cases a caller can act on.
		/// </summary>
		/// <param name="contract">The contract to describe.</param>
		/// <returns>The description.</returns>
		private static string Describe(DataContract contract)
			=> contract switch
			{
				UndocumentedContract { ConverterType: { } converterType } undocumented => $"{contract.DataType.FullName} is converted by {converterType.FullName}, which does not describe the representation it produces ({undocumented.Reason})",
				UndocumentedContract undocumented => $"{contract.DataType.FullName} has no described representation ({undocumented.Reason})",
				SurrogateContract => $"{contract.DataType.FullName} is serialized through a surrogate type, whose members are the ones that appear in the payload",
				_ => $"{contract.DataType.FullName} is serialized as {contract.Kind}",
			};

		private DataContract VisitParameter(ParameterExpression parameter)
			=> lambda.Parameters.Count == 1 && lambda.Parameters[0] == parameter
				? rootContract
				: throw this.Invalid(parameter, $"'{parameter.Name}' is not the root of the document");

		private DataContract VisitConversion(UnaryExpression conversion)
			=> IsTransparentConversion(conversion)
				? this.Visit(conversion.Operand)
				: throw this.Unsupported(
					conversion,
					$"A conversion from {conversion.Operand.Type.FullName} to {conversion.Type.FullName} changes which contract describes the value, so anything that follows it cannot be resolved.");

		private DataContract VisitMember(MemberExpression member)
		{
			if (member.Expression is null)
			{
				throw this.Invalid(member, $"'{member.Member.Name}' is static, so it is never part of a serialized document");
			}

			// 'x.Value' on Nullable<T> selects exactly the location that 'x' does.
			if (IsNullableValueAccess(member))
			{
				return Unwrap(this.Visit(member.Expression));
			}

			DataContract parent = Unwrap(this.Visit(member.Expression));
			if (parent is not ObjectContract objectContract)
			{
				throw this.Unsupported(member, $"{Describe(parent)}, so it has no property named '{member.Member.Name}'.");
			}

			PropertyContract? property = null;
			foreach (PropertyContract candidate in objectContract.Properties)
			{
				if (string.Equals(candidate.MemberName, member.Member.Name, StringComparison.Ordinal))
				{
					property = candidate;
					break;
				}
			}

			if (property is null)
			{
				throw this.Invalid(
					member,
					$"'{member.Member.Name}' is not one of the serialized properties of {objectContract.DataType.FullName}; members the shape ignores, and extension-data members, have no fixed location in the payload");
			}

			if (objectContract.Encoding == ObjectEncoding.Positional)
			{
				if (property.Position is not int position)
				{
					throw this.Unsupported(member, $"{objectContract.DataType.FullName} is written positionally but '{member.Member.Name}' has no position assigned to it.");
				}

				this.Elements.Add(ShapeShiftPathElement.Vector(position));
			}
			else
			{
				this.Elements.Add(ShapeShiftPathElement.Property(property.Name));
			}

			return property.Type;
		}

		private DataContract VisitIndexer(Expression step, Expression target, Expression argument)
		{
			if (argument is not ConstantExpression constant)
			{
				throw this.Unsupported(
					step,
					$"Only a constant index names a fixed location. Build a {nameof(ShapeShiftPath)} directly when the index is computed at runtime.");
			}

			DataContract parent = Unwrap(this.Visit(target));
			switch (parent)
			{
				case SequenceContract sequence:
					if (constant.Value is not int index)
					{
						throw this.Invalid(step, $"a vector is indexed by a 32-bit integer, but the index given is {DescribeConstant(constant)}");
					}

					if (index < 0)
					{
						throw this.Invalid(step, $"the index {index} is negative, and vector positions start at 0");
					}

					this.Elements.Add(ShapeShiftPathElement.Vector(index));
					return sequence.ElementType;

				case MapContract { Encoding: MapEncoding.StringKeyedMap } map:
					if (constant.Value is not string key)
					{
						throw this.Invalid(step, $"a string-keyed map is indexed by a string, but the index given is {DescribeConstant(constant)}");
					}

					this.Elements.Add(ShapeShiftPathElement.Property(key));
					return map.ValueType;

				case MapContract:
					throw this.Unsupported(
						step,
						$"{parent.DataType.FullName} is written as a vector of key/value pairs because its keys are not strings, so an entry has no addressable name or index.");

				default:
					throw this.Unsupported(step, $"{Describe(parent)}, so it cannot be indexed.");
			}
		}

		private Exception Invalid(Expression step, string reason)
			=> new ArgumentException($"The path expression '{lambda}' cannot be translated because {reason}, at '{step}'.", parameterName);

		private Exception Unsupported(Expression step, string reason)
			=> new NotSupportedException($"The path expression '{lambda}' cannot be translated at '{step}'. {reason}");
	}
}
