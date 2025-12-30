// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using PolyType.Abstractions;

namespace ShapeShift;

/// <summary>
/// A visitor that prepares type converters.
/// </summary>
internal class ShapeVisitor<TEncoder, TDecoder> : TypeShapeVisitor
    where TEncoder : IEncoder, allows ref struct
    where TDecoder : IDecoder, allows ref struct
{
    public override object? VisitObject<T>(IObjectTypeShape<T> objectShape, object? state = null)
    {
        Dictionary<string, PropertyConverter<T, TEncoder, TDecoder>> properties = new(objectShape.Properties.Count);
        foreach (var property in objectShape.Properties)
        {
            properties.Add(property.Name, (PropertyConverter<T, TEncoder, TDecoder>)property.Accept(this)!);
        }

        return new ObjectConverter<T, TEncoder, TDecoder>(objectShape, properties);
    }

    public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> propertyShape, object? state = null)
    {
        Getter<TDeclaringType, TPropertyType> getter = propertyShape.GetGetter();
        Setter<TDeclaringType, TPropertyType> setter = propertyShape.GetSetter();
        var converter = (IConverter<TPropertyType, TEncoder, TDecoder>)propertyShape.PropertyType.Accept(this)!;

        return new PropertyConverter<TDeclaringType, TEncoder, TDecoder>
        {
            Write = (ref encoder, in target, context) => converter.Write(ref encoder, getter(ref Unsafe.AsRef(in target)), context),
            Read = (ref decoder, ref target, context) => setter(ref target, converter.Read(ref decoder, context)!),
        };
    }
}
