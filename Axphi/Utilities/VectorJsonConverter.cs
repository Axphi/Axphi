using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace Axphi.Utilities;

internal sealed class VectorJsonConverter : JsonConverter<Vector>
{
    public override Vector Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected start of object when reading Vector.");
        }

        double x = 0;
        double y = 0;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return new Vector(x, y);
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected property name when reading Vector.");
            }

            string propertyName = reader.GetString() ?? string.Empty;
            reader.Read();

            if (propertyName.Equals(nameof(Vector.X), StringComparison.OrdinalIgnoreCase))
            {
                x = reader.GetDouble();
            }
            else if (propertyName.Equals(nameof(Vector.Y), StringComparison.OrdinalIgnoreCase))
            {
                y = reader.GetDouble();
            }
            else
            {
                reader.Skip();
            }
        }

        throw new JsonException("Unexpected end of JSON when reading Vector.");
    }

    public override void Write(Utf8JsonWriter writer, Vector value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber(nameof(Vector.X), value.X);
        writer.WriteNumber(nameof(Vector.Y), value.Y);
        writer.WriteEndObject();
    }
}