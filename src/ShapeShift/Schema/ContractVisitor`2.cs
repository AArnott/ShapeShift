// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text;

namespace ShapeShift.Schema;

/// <summary>
/// A visitor that describes the serialized form of types as <see cref="DataContract"/> graphs.
/// </summary>
/// <typeparam name="TEncoder"><inheritdoc cref="ShapeShiftSerializer{TEncoder, TDecoder}" path="/typeparam[@name='TEncoder']"/></typeparam>
/// <typeparam name="TDecoder"><inheritdoc cref="ShapeShiftSerializer{TEncoder, TDecoder}" path="/typeparam[@name='TDecoder']"/></typeparam>
/// <param name="owner">The cache that carries the policies that influence the serialized form.</param>
/// <remarks>
/// This visitor mirrors the structural decisions made by <see cref="ShapeVisitor{TEncoder, TDecoder}"/>.
/// It is <em>not</em> thread-safe; callers must serialize access to it.
/// </remarks>
internal sealed class ContractVisitor<TEncoder, TDecoder>(ConverterCache<TEncoder, TDecoder> owner) : TypeShapeVisitor
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	private readonly Dictionary<ITypeShape, DataContract> contracts = new();

	/// <inheritdoc/>
	public override object? VisitObject<T>(IObjectTypeShape<T> objectShape, object? state = null)
	{
		if (this.TryGetCustomOrPrimitiveContract(objectShape.Type, objectShape, objectShape.Provider, objectShape.AttributeProvider, out DataContract? custom))
		{
			return this.Remember(objectShape, custom);
		}

		if (objectShape.Constructor is not IConstructorShape constructorShape)
		{
			return this.Remember(objectShape, new UndocumentedContract(objectShape.Type, "The type cannot be deserialized because it has no usable constructor."));
		}

		ObjectContract contract = new(objectShape.Type);
		this.Remember(objectShape, contract);

		bool parameterized = constructorShape.Parameters.Count > 0;
		Dictionary<string, IParameterShape> parametersByName = parameterized
			? constructorShape.Parameters.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase)
			: new(StringComparer.OrdinalIgnoreCase);

		List<PropertyContract> properties = new(objectShape.Properties.Count);
		HashSet<string> declaredNames = new(StringComparer.Ordinal);
		bool hasExtensionData = false;
		foreach (IPropertyShape property in objectShape.Properties)
		{
			if (property.AttributeProvider.GetCustomAttribute<ShapeShiftExtensionDataAttribute>() is not null)
			{
				hasExtensionData = true;
				continue;
			}

			string name = owner.GetSerializedPropertyName(property.Name, property.AttributeProvider);
			parametersByName.TryGetValue(property.Name, out IParameterShape? parameter);
			properties.Add(this.DescribeProperty(name, property, parameter, parameterized));
			declaredNames.Add(name);
		}

		if (parameterized)
		{
			foreach (IParameterShape parameter in constructorShape.Parameters)
			{
				string name = owner.GetSerializedPropertyName(parameter.Name, parameter.AttributeProvider);
				if (declaredNames.Add(name))
				{
					properties.Add(this.DescribeParameter(name, parameter));
				}
			}
		}

		contract.Complete([.. properties], hasExtensionData);
		return contract;
	}

	/// <inheritdoc/>
	public override object? VisitUnion<TUnion>(IUnionTypeShape<TUnion> unionShape, object? state = null)
	{
		UnionContract contract = new(unionShape.Type);
		this.Remember(unionShape, contract);

		DataContract baseContract = this.GetContract(unionShape.BaseType);
		List<UnionCaseContract> cases = new(unionShape.UnionCases.Count);
		foreach (IUnionCaseShape unionCase in unionShape.UnionCases)
		{
			cases.Add(new(unionCase.Name, unionCase.Tag, this.GetContract(unionCase.UnionCaseType))
			{
				IsTagSpecified = unionCase.IsTagSpecified,
			});
		}

		contract.Complete(baseContract, [.. cases]);
		return contract;
	}

	/// <inheritdoc/>
	public override object? VisitOptional<TOptional, TElement>(IOptionalTypeShape<TOptional, TElement> optionalShape, object? state = null)
	{
		OptionalContract contract = new(optionalShape.Type);
		this.Remember(optionalShape, contract);
		contract.Complete(this.GetContract(optionalShape.ElementType));
		return contract;
	}

	/// <inheritdoc/>
	public override object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryTypeShape<TDictionary, TKey, TValue> dictionaryShape, object? state = null)
	{
		if (this.TryGetCustomOrPrimitiveContract(dictionaryShape.Type, dictionaryShape, dictionaryShape.Provider, dictionaryShape.AttributeProvider, out DataContract? custom))
		{
			return this.Remember(dictionaryShape, custom);
		}

		MapContract contract = new(dictionaryShape.Type, typeof(TKey) == typeof(string) ? MapEncoding.StringKeyedMap : MapEncoding.KeyValuePairSequence);
		this.Remember(dictionaryShape, contract);
		contract.Complete(this.GetContract(dictionaryShape.KeyType), this.GetContract(dictionaryShape.ValueType));
		return contract;
	}

	/// <inheritdoc/>
	public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> enumerableShape, object? state = null)
	{
		if (this.TryGetCustomOrPrimitiveContract(enumerableShape.Type, enumerableShape, enumerableShape.Provider, enumerableShape.AttributeProvider, out DataContract? custom))
		{
			return this.Remember(enumerableShape, custom);
		}

		if (enumerableShape.Type.IsArray && enumerableShape.Rank > 1)
		{
			RectangularArrayContract rectangular = new(enumerableShape.Type, enumerableShape.Rank);
			this.Remember(enumerableShape, rectangular);
			rectangular.Complete(this.GetContract(enumerableShape.ElementType));
			return rectangular;
		}

		SequenceContract contract = new(enumerableShape.Type);
		this.Remember(enumerableShape, contract);
		contract.Complete(this.GetContract(enumerableShape.ElementType), enumerableShape.IsSetType);
		return contract;
	}

	/// <inheritdoc/>
	public override object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> enumShape, object? state = null)
	{
		if (this.TryGetCustomOrPrimitiveContract(enumShape.Type, enumShape, enumShape.Provider, enumShape.AttributeProvider, out DataContract? custom))
		{
			return this.Remember(enumShape, custom);
		}

		List<EnumMemberContract> members = new(enumShape.Members.Count);
		foreach (KeyValuePair<string, TUnderlying> member in enumShape.Members)
		{
			members.Add(new(member.Key, ToNumericValue(member.Value)));
		}

		EnumContract contract = new(enumShape.Type, this.GetContract(enumShape.UnderlyingType), members)
		{
			IsSerializedByName = owner.SerializeEnumValuesByName,
			IsFlags = enumShape.IsFlags,
		};
		return this.Remember(enumShape, contract);
	}

	/// <inheritdoc/>
	public override object? VisitSurrogate<T, TSurrogate>(ISurrogateTypeShape<T, TSurrogate> surrogateShape, object? state = null)
	{
		if (this.TryGetCustomOrPrimitiveContract(surrogateShape.Type, surrogateShape, surrogateShape.Provider, surrogateShape.AttributeProvider, out DataContract? custom))
		{
			return this.Remember(surrogateShape, custom);
		}

		SurrogateContract contract = new(surrogateShape.Type);
		this.Remember(surrogateShape, contract);
		contract.Complete(this.GetContract(surrogateShape.SurrogateType));
		return contract;
	}

	/// <inheritdoc/>
	public override object? VisitFunction<TFunction, TArgumentState, TResult>(IFunctionTypeShape<TFunction, TArgumentState, TResult> functionShape, object? state = null)
		=> this.Remember(functionShape, new UndocumentedContract(functionShape.Type, "Delegate types cannot be serialized."));

	/// <summary>
	/// Gets the contract for a given type shape, creating it if necessary.
	/// </summary>
	/// <param name="shape">The shape of the type to describe.</param>
	/// <returns>The contract. It may still be under construction if <paramref name="shape"/> participates in a cycle.</returns>
	internal DataContract GetContract(ITypeShape shape)
		=> this.contracts.TryGetValue(shape, out DataContract? existing) ? existing : (DataContract)shape.Accept(this)!;

	private static ShapeShiftValue ToNumericValue<TUnderlying>(TUnderlying value)
		=> value switch
		{
			byte b => (long)b,
			sbyte b => (long)b,
			short s => (long)s,
			ushort s => (long)s,
			int i => (long)i,
			uint i => (long)i,
			long l => l,
			ulong l => l,
			_ => ShapeShiftValue.Null,
		};

	private static bool TryGetPrimitiveDataType(Type type, out PrimitiveDataType primitiveType)
	{
		if (type == typeof(bool))
		{
			primitiveType = PrimitiveDataType.Boolean;
		}
		else if (type == typeof(char))
		{
			primitiveType = PrimitiveDataType.Char;
		}
		else if (type == typeof(Rune))
		{
			primitiveType = PrimitiveDataType.Rune;
		}
		else if (type == typeof(string))
		{
			primitiveType = PrimitiveDataType.String;
		}
		else if (type == typeof(sbyte))
		{
			primitiveType = PrimitiveDataType.SByte;
		}
		else if (type == typeof(byte))
		{
			primitiveType = PrimitiveDataType.Byte;
		}
		else if (type == typeof(short))
		{
			primitiveType = PrimitiveDataType.Int16;
		}
		else if (type == typeof(ushort))
		{
			primitiveType = PrimitiveDataType.UInt16;
		}
		else if (type == typeof(int))
		{
			primitiveType = PrimitiveDataType.Int32;
		}
		else if (type == typeof(uint))
		{
			primitiveType = PrimitiveDataType.UInt32;
		}
		else if (type == typeof(long))
		{
			primitiveType = PrimitiveDataType.Int64;
		}
		else if (type == typeof(ulong))
		{
			primitiveType = PrimitiveDataType.UInt64;
		}
		else if (type == typeof(Int128))
		{
			primitiveType = PrimitiveDataType.Int128;
		}
		else if (type == typeof(UInt128))
		{
			primitiveType = PrimitiveDataType.UInt128;
		}
		else if (type == typeof(BigInteger))
		{
			primitiveType = PrimitiveDataType.BigInteger;
		}
		else if (type == typeof(Half))
		{
			primitiveType = PrimitiveDataType.Half;
		}
		else if (type == typeof(float))
		{
			primitiveType = PrimitiveDataType.Single;
		}
		else if (type == typeof(double))
		{
			primitiveType = PrimitiveDataType.Double;
		}
		else if (type == typeof(decimal))
		{
			primitiveType = PrimitiveDataType.Decimal;
		}
		else if (type == typeof(DateTime))
		{
			primitiveType = PrimitiveDataType.DateTime;
		}
		else if (type == typeof(DateTimeOffset))
		{
			primitiveType = PrimitiveDataType.DateTimeOffset;
		}
		else if (type == typeof(TimeSpan))
		{
			primitiveType = PrimitiveDataType.TimeSpan;
		}
		else
		{
			primitiveType = default;
			return false;
		}

		return true;
	}

	private static ShapeShiftValue? TryDescribeDefaultValue(object? value)
		=> value switch
		{
			null => ShapeShiftValue.Null,
			bool b => b,
			string s => s,
			sbyte v => (long)v,
			byte v => (long)v,
			short v => (long)v,
			ushort v => (long)v,
			int v => (long)v,
			uint v => (long)v,
			long v => v,
			ulong v => v,
			float v => (double)v,
			double v => v,
			decimal v => v,
			char v => v.ToString(),
			_ => null,
		};

	private DataContract Remember(ITypeShape shape, DataContract contract)
	{
		this.contracts[shape] = contract;
		return contract;
	}

	private PropertyContract DescribeProperty(string name, IPropertyShape property, IParameterShape? parameter, bool parameterized)
	{
		bool rejectNull = (owner.DeserializeDefaultValues & DeserializeDefaultValuesPolicy.AllowNullValuesForNonNullableProperties) == 0
			&& (!property.HasGetter || property.IsGetterNonNullable)
			&& (!property.HasSetter || property.IsSetterNonNullable)
			&& (parameter is null || parameter.IsNonNullable);

		bool alwaysWritten = true;
		if (property.HasGetter && owner.SerializeDefaultValues != SerializeDefaultValuesPolicy.Always)
		{
			bool required = parameter?.IsRequired is true;
			alwaysWritten = (required && (owner.SerializeDefaultValues & SerializeDefaultValuesPolicy.Required) != 0)
				|| (property.PropertyType.Type.IsValueType
					? (owner.SerializeDefaultValues & SerializeDefaultValuesPolicy.ValueTypes) != 0
					: (owner.SerializeDefaultValues & SerializeDefaultValuesPolicy.ReferenceTypes) != 0);
		}

		return new(name, this.GetContractForMember(property.PropertyType, property.AttributeProvider))
		{
			DeclaredName = property.Name,
			MemberName = PropertyContract.GetMemberName(property),
			IsReadable = property.HasGetter,
			IsWritable = parameterized ? parameter is not null : property.HasSetter,
			IsRequired = this.IsRequired(parameter),
			IsNullable = !rejectNull,
			IsAlwaysWritten = alwaysWritten,
			DefaultValue = parameter?.HasDefaultValue is true ? TryDescribeDefaultValue(parameter.DefaultValue) : null,
		};
	}

	private PropertyContract DescribeParameter(string name, IParameterShape parameter)
	{
		bool rejectNull = (owner.DeserializeDefaultValues & DeserializeDefaultValuesPolicy.AllowNullValuesForNonNullableProperties) == 0
			&& parameter.IsNonNullable;

		return new(name, this.GetContractForMember(parameter.ParameterType, parameter.AttributeProvider))
		{
			DeclaredName = parameter.Name,
			IsReadable = false,
			IsWritable = true,
			IsRequired = this.IsRequired(parameter),
			IsNullable = !rejectNull,
			DefaultValue = parameter.HasDefaultValue ? TryDescribeDefaultValue(parameter.DefaultValue) : null,
		};
	}

	private bool IsRequired(IParameterShape? parameter)
		=> parameter?.IsRequired is true
			&& (owner.DeserializeDefaultValues & DeserializeDefaultValuesPolicy.AllowMissingValuesForRequiredProperties) == 0;

	private DataContract GetContractForMember(ITypeShape typeShape, IGenericCustomAttributeProvider attributeProvider)
		=> owner.TryGetConverterFromAttribute(typeShape.Type, typeShape, attributeProvider, out ShapeShiftConverter<TEncoder, TDecoder>? converter)
			? this.DescribeConverter(converter, typeShape.Type, typeShape)
			: this.GetContract(typeShape);

	private bool TryGetCustomOrPrimitiveContract(Type type, ITypeShape? typeShape, ITypeShapeProvider shapeProvider, IGenericCustomAttributeProvider attributeProvider, [NotNullWhen(true)] out DataContract? contract)
	{
		if (owner.TryGetRuntimeProfferedConverter(type, typeShape, shapeProvider, out ShapeShiftConverter<TEncoder, TDecoder>? profferedConverter))
		{
			contract = this.DescribeConverter(profferedConverter, type, typeShape);
			return true;
		}

		if (type == typeof(ShapeShiftValue))
		{
			contract = new DynamicContract(type);
			return true;
		}

		if (TryGetPrimitiveDataType(type, out PrimitiveDataType primitiveType))
		{
			contract = new PrimitiveContract(type, primitiveType);
			return true;
		}

		if (owner.TryGetConverterFromAttribute(type, typeShape, attributeProvider, out ShapeShiftConverter<TEncoder, TDecoder>? attributeConverter))
		{
			contract = this.DescribeConverter(attributeConverter, type, typeShape);
			return true;
		}

		contract = null;
		return false;
	}

	private DataContract DescribeConverter(ShapeShiftConverter<TEncoder, TDecoder> converter, Type type, ITypeShape? typeShape)
	{
		ContractContext<TEncoder, TDecoder> context = new(this, typeShape);
		return converter.GetContract(context)
			?? new UndocumentedContract(type, $"The converter '{converter.GetType().FullName}' does not describe the representation it produces. Override {nameof(ShapeShiftConverter<TEncoder, TDecoder>.GetContract)} to describe it.")
			{
				ConverterType = converter.GetType(),
			};
	}
}
