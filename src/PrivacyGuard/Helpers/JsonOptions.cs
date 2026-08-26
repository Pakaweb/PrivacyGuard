using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrivacyGuard.Helpers;

/// <summary>
/// Shared JSON options for preferences and restore-point payloads.
/// </summary>
public static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
}
