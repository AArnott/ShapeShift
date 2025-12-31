// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace CustomConverterFactory
{
    #region NonGeneric
    internal class NonGenericCustomConverterFactory<TEncoder, TDecoder> : IShapeShiftConverterFactory<TEncoder, TDecoder>
        where TEncoder : IEncoder, allows ref struct
        where TDecoder : IDecoder, allows ref struct
    {
        // special purpose factory knows exactly what it supports. No generic type parameter needed.
        public ShapeShiftConverter<TEncoder, TDecoder>? CreateConverter(Type type, ITypeShape? shape, in ConverterContext<TEncoder, TDecoder> context)
            => type == typeof(List<Guid>) ? new WrapInArrayConverter<List<Guid>, TEncoder, TDecoder>() : null;
    }
    #endregion

    #region Generic
    internal class GenericCustomConverterFactory<TEncoder, TDecoder> : IShapeShiftConverterFactory<TEncoder, TDecoder>, ITypeShapeFunc
        where TEncoder : IEncoder, allows ref struct
        where TDecoder : IDecoder, allows ref struct
    {
        // perform type check, then defer to generic method.
        public ShapeShiftConverter<TEncoder, TDecoder>? CreateConverter(Type type, ITypeShape? shape, in ConverterContext<TEncoder, TDecoder> context)
            => shape?.Type == typeof(int) ? (ShapeShiftConverter<TEncoder, TDecoder>?)shape.Invoke(this) : null;

        // type check is already done, just create the converter.
        object? ITypeShapeFunc.Invoke<T>(ITypeShape<T> typeShape, object? state)
            => new WrapInArrayConverter<T, TEncoder, TDecoder>();
    }
    #endregion

    #region Visitor
    internal class VisitorCustomConverterFactory<TEncoder, TDecoder> : IShapeShiftConverterFactory<TEncoder, TDecoder>
        where TEncoder : IEncoder, allows ref struct
        where TDecoder : IDecoder, allows ref struct
    {
        // perform type check, then defer to generic method.
        public ShapeShiftConverter<TEncoder, TDecoder>? CreateConverter(Type type, ITypeShape? shape, in ConverterContext<TEncoder, TDecoder> context)
            => shape is IEnumerableTypeShape enumShape && enumShape.Type.GetGenericTypeDefinition() == typeof(List<>) ?
                (ShapeShiftConverter<TEncoder, TDecoder>?)shape.Accept(Visitor.Instance) : null;

        private class Visitor : TypeShapeVisitor
        {
            internal static readonly Visitor Instance = new();

            public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> enumerableShape, object? state = null)
                => new CustomListConverter<TElement, TEncoder, TDecoder>();
        }
    }
    #endregion

    class WrapInArrayConverter<T, TEncoder, TDecoder> : ShapeShiftConverter<T, TEncoder, TDecoder>
        where TEncoder : IEncoder, allows ref struct
        where TDecoder : IDecoder, allows ref struct
    {
        public override T? Read(ref TDecoder reader, SerializationContext<TEncoder, TDecoder> context)
        {
            throw new NotImplementedException();
        }

        public override void Write(ref TEncoder writer, in T? value, SerializationContext<TEncoder, TDecoder> context)
        {
            throw new NotImplementedException();
        }
    }

    class CustomListConverter<T, TEncoder, TDecoder> : ShapeShiftConverter<List<T>, TEncoder, TDecoder>
        where TEncoder : IEncoder, allows ref struct
        where TDecoder : IDecoder, allows ref struct
    {
        public override List<T>? Read(ref TDecoder reader, SerializationContext<TEncoder, TDecoder> context)
        {
            throw new NotImplementedException();
        }

        public override void Write(ref TEncoder writer, in List<T>? value, SerializationContext<TEncoder, TDecoder> context)
        {
            throw new NotImplementedException();
        }
    }
}
