// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;
using ShapeShift.Json;
using ShapeShift.MsgPack;
using ShapeShift.Schema;

namespace SchemaGeneration
{
    internal static class Samples
    {
        #region Contract
        internal static void DescribeContract()
        {
            JsonSerializer serializer = new();
            DataContract contract = serializer.GetContract<Product>();

            foreach (PropertyContract property in ((ObjectContract)contract).Properties)
            {
                Console.WriteLine($"{property.Name} ({property.Type.Kind}) required={property.IsRequired} nullable={property.IsNullable}");
            }
        }
        #endregion

        #region JsonSchema
        internal static void GenerateJsonSchema()
        {
            JsonSerializer serializer = new();
            JsonObject schema = serializer.GetJsonSchema<Product>();

            Console.WriteLine(Render(schema));
        }
        #endregion

        #region MessagePackProfile
        internal static void GenerateMessagePackAnnotatedSchema()
        {
            MsgPackSerializer serializer = new();
            JsonObject schema = JsonSchema.Create(
                serializer.GetContract<Product>(),
                new JsonSchemaOptions { Profile = JsonSchemaProfile.MessagePack });

            Console.WriteLine(Render(schema));
        }
        #endregion

        #region Limits
        internal static void GenerateSchemaWithLimits()
        {
            JsonSerializer serializer = new();
            SerializationContext<JsonEncoder, JsonDecoder> context = new();
            JsonObject schema = serializer.GetJsonSchema<Product>(new JsonSchemaOptions
            {
                Limits = JsonSchemaLimits.FromContext(context),
            });

            Console.WriteLine(Render(schema));
        }
        #endregion

        #region CustomConverterContract
        internal class HexInt32Converter : ShapeShiftConverter<int, JsonEncoder, JsonDecoder>
        {
            public override int Read(ref JsonDecoder decoder, SerializationContext<JsonEncoder, JsonDecoder> context)
                => Convert.ToInt32(decoder.ReadString(), 16);

            public override void Write(ref JsonEncoder encoder, in int value, SerializationContext<JsonEncoder, JsonDecoder> context)
                => encoder.WriteValue(value.ToString("X8", System.Globalization.CultureInfo.InvariantCulture));

            // Without this override the schema would say "this value is undocumented"
            // rather than describing a shape the converter does not actually produce.
            public override DataContract? GetContract(ContractContext<JsonEncoder, JsonDecoder> context)
                => new PrimitiveContract(typeof(int), PrimitiveDataType.String);
        }
        #endregion

        private static string Render(JsonObject schema)
        {
            using MemoryStream stream = new();
            using (System.Text.Json.Utf8JsonWriter writer = new(stream, new System.Text.Json.JsonWriterOptions { Indented = true }))
            {
                schema.WriteTo(writer);
            }

            return System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }
    }

    #region Model
    [GenerateShape]
    internal partial record Product(
        string Name,
        decimal Price,
        Category Category,
        [property: ShapeShiftConverter(typeof(Samples.HexInt32Converter))] int Sku,
        IReadOnlyList<Product>? Bundled = null);

    internal enum Category
    {
        /// <summary>Physical goods.</summary>
        Hardware,

        /// <summary>Licensed software.</summary>
        Software,
    }
    #endregion
}
