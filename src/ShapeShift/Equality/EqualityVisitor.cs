// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Numerics;
using System.Text;

namespace ShapeShift.Equality;

/// <summary>
/// Builds <see cref="StructuralComparer{T}"/> instances from PolyType shapes.
/// </summary>
/// <param name="policy">The hashing policy to apply to leaf values.</param>
/// <param name="overrides">User supplied comparers, keyed by the type they compare.</param>
/// <param name="context">The type generation context that memoizes and links recursive comparers.</param>
internal sealed class EqualityVisitor(
	HashingPolicy policy,
	FrozenDictionary<Type, object> overrides,
	TypeGenerationContext context) : TypeShapeVisitor, ITypeShapeFunc
{
	/// <summary>
	/// Types that are treated as indivisible values, compared with their own <see cref="object.Equals(object)"/>
	/// semantics rather than by recursing into their members.
	/// </summary>
	private static readonly FrozenSet<Type> LeafTypes = new HashSet<Type>
	{
		typeof(bool), typeof(char), typeof(Rune), typeof(string),
		typeof(sbyte), typeof(byte), typeof(short), typeof(ushort),
		typeof(int), typeof(uint), typeof(long), typeof(ulong),
		typeof(nint), typeof(nuint), typeof(Int128), typeof(UInt128),
		typeof(BigInteger), typeof(Half), typeof(float), typeof(double), typeof(decimal),
		typeof(DateTime), typeof(DateTimeOffset), typeof(TimeSpan), typeof(DateOnly), typeof(TimeOnly),
		typeof(Guid), typeof(Uri), typeof(Version), typeof(byte[]),
	}.ToFrozenSet();

	/// <inheritdoc/>
	object? ITypeShapeFunc.Invoke<T>(ITypeShape<T> typeShape, object? state)
	{
		if (overrides.TryGetValue(typeof(T), out object? userComparer))
		{
			return new UserComparer<T>((IEqualityComparer<T>)userComparer, policy);
		}

		if (typeof(ShapeShiftValue).IsAssignableFrom(typeof(T)))
		{
			ShapeShiftValueComparer inner = new(policy);
			return typeof(T) == typeof(ShapeShiftValue)
				? (object)inner
				: new DerivedShapeShiftValueComparer<T>(inner);
		}

		if (typeof(T) == typeof(byte[]))
		{
			return policy.CreateLeafComparer((IEqualityComparer<T>)(object)ByteSequenceEqualityComparer.Instance);
		}

		if (LeafTypes.Contains(typeof(T)))
		{
			return policy.CreateLeafComparer(EqualityComparer<T>.Default);
		}

		return typeShape.Accept(this, state);
	}

	/// <inheritdoc/>
	public override object? VisitObject<T>(IObjectTypeShape<T> objectShape, object? state = null)
	{
		List<MemberComparer<T>> members = new(objectShape.Properties.Count);
		foreach (IPropertyShape property in objectShape.Properties)
		{
			if (!property.HasGetter)
			{
				continue;
			}

			members.Add((MemberComparer<T>)property.Accept(this)!);
		}

		return members.Count == 0
			? policy.CreateLeafComparer(EqualityComparer<T>.Default)
			: new ObjectComparer<T>([.. members]);
	}

	/// <inheritdoc/>
	public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> propertyShape, object? state = null)
		=> new MemberComparer<TDeclaringType, TPropertyType>(propertyShape.GetGetter(), this.GetComparer(propertyShape.PropertyType));

	/// <inheritdoc/>
	public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> enumerableShape, object? state = null)
	{
		StructuralComparer<TElement> elementComparer = this.GetComparer(enumerableShape.ElementType);

		if (enumerableShape.Type.IsArray && enumerableShape.Rank > 1)
		{
			return new MultidimensionalArrayComparer<TEnumerable, TElement>(enumerableShape.GetGetEnumerable(), elementComparer, enumerableShape.Rank);
		}

		Func<TEnumerable, IEnumerable<TElement>> getEnumerable = enumerableShape.GetGetEnumerable();
		return enumerableShape.IsSetType
			? new SetComparer<TEnumerable, TElement>(getEnumerable, elementComparer)
			: new SequenceComparer<TEnumerable, TElement>(getEnumerable, elementComparer);
	}

	/// <inheritdoc/>
	public override object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryTypeShape<TDictionary, TKey, TValue> dictionaryShape, object? state = null)
		=> new DictionaryComparer<TDictionary, TKey, TValue>(
			dictionaryShape.GetGetDictionary(),
			this.GetComparer(dictionaryShape.KeyType),
			this.GetComparer(dictionaryShape.ValueType));

	/// <inheritdoc/>
	public override object? VisitOptional<TOptional, TElement>(IOptionalTypeShape<TOptional, TElement> optionalShape, object? state = null)
		=> new OptionalComparer<TOptional, TElement>(optionalShape.GetDeconstructor(), this.GetComparer(optionalShape.ElementType));

	/// <inheritdoc/>
	public override object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> enumShape, object? state = null)
		=> policy.CreateLeafComparer(EqualityComparer<TEnum>.Default);

	/// <inheritdoc/>
	public override object? VisitSurrogate<T, TSurrogate>(ISurrogateTypeShape<T, TSurrogate> surrogateShape, object? state = null)
		=> new SurrogateComparer<T, TSurrogate>(surrogateShape.Marshaler, this.GetComparer(surrogateShape.SurrogateType));

	/// <inheritdoc/>
	public override object? VisitUnion<TUnion>(IUnionTypeShape<TUnion> unionShape, object? state = null)
	{
		StructuralComparer<TUnion> baseComparer = (StructuralComparer<TUnion>)unionShape.BaseType.Accept(this)!;
		StructuralComparer<TUnion>[] caseComparers = new StructuralComparer<TUnion>[unionShape.UnionCases.Count];
		foreach (IUnionCaseShape unionCase in unionShape.UnionCases)
		{
			caseComparers[unionCase.Index] = (StructuralComparer<TUnion>)unionCase.Accept(this)!;
		}

		return new UnionComparer<TUnion>(unionShape.GetGetUnionCaseIndex(), baseComparer, caseComparers);
	}

	/// <inheritdoc/>
	public override object? VisitUnionCase<TUnionCase, TUnion>(IUnionCaseShape<TUnionCase, TUnion> unionCaseShape, object? state = null)
		=> new UnionCaseComparer<TUnionCase, TUnion>(this.GetComparer(unionCaseShape.UnionCaseType), unionCaseShape.Marshaler);

	/// <inheritdoc/>
	public override object? VisitFunction<TFunction, TArgumentState, TResult>(IFunctionTypeShape<TFunction, TArgumentState, TResult> functionShape, object? state = null)
		=> policy.CreateLeafComparer(EqualityComparer<TFunction>.Default);

	/// <summary>
	/// Gets or creates the comparer for a given type shape.
	/// </summary>
	/// <typeparam name="T">The type to compare.</typeparam>
	/// <param name="shape">The shape of the type to compare.</param>
	/// <returns>The comparer.</returns>
	internal StructuralComparer<T> GetComparer<T>(ITypeShape<T> shape) => (StructuralComparer<T>)context.GetOrAdd(shape)!;
}
