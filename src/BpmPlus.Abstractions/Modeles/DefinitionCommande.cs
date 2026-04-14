using System.Text.Json.Serialization;

namespace BpmPlus.Abstractions;

public class DefinitionCommande
{
    [JsonPropertyName("nomCommande")]
    public string NomCommande { get; set; } = string.Empty;

    [JsonPropertyName("sourceAggregateId")]
    public ISourceParametre? SourceAggregateId { get; set; }

    [JsonPropertyName("parametres")]
    public Dictionary<string, ISourceParametre> Parametres { get; set; } = new();
}
