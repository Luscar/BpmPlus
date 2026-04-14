using System.Text.Json.Serialization;

namespace BpmPlus.Abstractions;

public class FluxSortant
{
    [JsonPropertyName("vers")]
    public string Vers { get; set; } = string.Empty;

    [JsonPropertyName("condition")]
    public ICondition? Condition { get; set; }

    [JsonPropertyName("estParDefaut")]
    public bool EstParDefaut { get; set; }
}
