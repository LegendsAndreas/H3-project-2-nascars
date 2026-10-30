using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game.Api.Models;

public sealed record Coordinates(
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y,
    [property: JsonPropertyName("z")] double Z)
{
    public static bool TryRead(JsonElement value, out Coordinates? coordinates)
    {
        coordinates = null;
        if (value.ValueKind != JsonValueKind.Object
            || !TryReadNumber(value, "x", out var x)
            || !TryReadNumber(value, "y", out var y)
            || !TryReadNumber(value, "z", out var z))
        {
            return false;
        }

        coordinates = new Coordinates(x, y, z);
        return true;
    }

    private static bool TryReadNumber(JsonElement value, string name, out double number)
    {
        number = default;
        return value.TryGetProperty(name, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetDouble(out number)
            && double.IsFinite(number);
    }
}
