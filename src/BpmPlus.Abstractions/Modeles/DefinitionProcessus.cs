using System.Text.Json.Serialization;

namespace BpmPlus.Abstractions;

public class DefinitionProcessus
{
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    [JsonPropertyName("cle")]
    public string Cle { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("nom")]
    public string Nom { get; set; } = string.Empty;

    [JsonIgnore]
    public StatutDefinition Statut { get; set; } = StatutDefinition.Brouillon;

    [JsonPropertyName("noeudDebut")]
    public string NoeudDebutId { get; set; } = string.Empty;

    [JsonPropertyName("noeuds")]
    public List<NoeudProcessus> Noeuds { get; set; } = new();

    [JsonIgnore]
    public DateTime DateCreation { get; set; }

    [JsonIgnore]
    public DateTime? DatePublication { get; set; }

    public NoeudProcessus? TrouverNoeud(string id)
        => Noeuds.FirstOrDefault(n => n.Id == id);
}
