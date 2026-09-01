// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ShapeShift.Json;

namespace DiagnosingFailures;

internal static partial class DiagnosingFailuresSample
{
    internal static void Run()
    {
        #region ExceptionPaths
        var serializer = new JsonSerializer();

        string json = """
            {
                "Id": 5,
                "Lines": [
                    { "Sku": "a-1", "Quantity": 2 },
                    { "Sku": "b-2", "Quantity": "two" }
                ]
            }
            """;

        try
        {
            Order? order = serializer.Deserialize<Order>(json);
        }
        catch (ShapeShiftSerializationException ex)
        {
            // ex.Path is $.Lines[1].Quantity, so the failing value can be located
            // in the document without re-reading the whole payload.
            Console.WriteLine(ex.Path);

            // The message ends with "Path: $.Lines[1].Quantity." and the original
            // decoder failure is preserved as the inner exception.
            Console.WriteLine(ex.Message);
            Console.WriteLine(ex.InnerException?.Message);
        }
        #endregion
    }

    #region CustomConverterBreadcrumbs
    internal sealed class TotalsConverter : ShapeShiftConverter<int[], JsonEncoder, JsonDecoder>
    {
        public override int[]? Read(ref JsonDecoder decoder, SerializationContext<JsonEncoder, JsonDecoder> context)
            => throw new NotSupportedException();

        public override void Write(ref JsonEncoder encoder, in int[]? value, SerializationContext<JsonEncoder, JsonDecoder> context)
        {
            if (value is null)
            {
                encoder.WriteNull();
                return;
            }

            encoder.WriteStartVector(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                try
                {
                    encoder.WriteValue(value[i]);
                }
                catch (ShapeShiftSerializationException ex) when (ex.AddEnclosingPathElement(i))
                {
                    // AddEnclosingPathElement always returns true, so the filter falls through
                    // to this rethrow, which preserves the original stack trace.
                    throw;
                }
            }

            encoder.WriteEndVector();
        }
    }
    #endregion

    [GenerateShape]
    internal partial record Order(int Id, List<OrderLine> Lines);

    [GenerateShape]
    internal partial record OrderLine(string Sku, int Quantity);
}
