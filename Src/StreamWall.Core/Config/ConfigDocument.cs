using System.Text.Json;
using System.Text.Json.Serialization;

namespace StreamWall.Core.Config;

/// <summary>The config as JSON (the plaintext inside the encrypted file).</summary>
public static class ConfigDocument
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static string Serialize(AppConfig config) => JsonSerializer.Serialize(ConfigRules.Normalize(config), Options);

    /// <summary>Parses and normalizes; returns false (with a reason that holds no content) if the text is not a config.</summary>
    public static bool TryParse(string json, out AppConfig config, out string? problem)
    {
        try
        {
            AppConfig? parsed = JsonSerializer.Deserialize<AppConfig>(json, Options);
            if (parsed is null)
            {
                config = ConfigRules.Normalize(new AppConfig());
                problem = "empty document";
                return false;
            }
            config = ConfigRules.Normalize(parsed);
            problem = null;
            return true;
        }
        catch (JsonException ex)
        {
            config = ConfigRules.Normalize(new AppConfig());
            // Position only: the message itself could quote content.
            problem = "not valid JSON at line " + (ex.LineNumber + 1);
            return false;
        }
    }
}
