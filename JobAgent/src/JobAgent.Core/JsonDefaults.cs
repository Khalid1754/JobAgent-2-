using System.Text.Encodings.Web;
using System.Text.Json;

namespace JobAgent.Core;

public static class JsonDefaults
{
    /// <summary>JobTech och Claude API använder snake_case i sin JSON.</summary>
    public static readonly JsonSerializerOptions SnakeCase = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>För filer vi själva skriver: läsbar, indenterad och med å/ä/ö oescapade.</summary>
    public static readonly JsonSerializerOptions Pretty = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}
