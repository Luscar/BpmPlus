using System.Text.Json.Serialization;

namespace BpmPlus.Abstractions;

[JsonDerivedType(typeof(NoeudMetier), "NoeudMetier")]
[JsonDerivedType(typeof(NoeudInteractif), "NoeudInteractif")]
[JsonDerivedType(typeof(NoeudDecision), "NoeudDecision")]
[JsonDerivedType(typeof(NoeudAttenteTemps), "NoeudAttenteTemps")]
[JsonDerivedType(typeof(NoeudAttenteSignal), "NoeudAttenteSignal")]
[JsonDerivedType(typeof(NoeudSousProcessus), "NoeudSousProcessus")]
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
public abstract class NoeudProcessus
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("nom")]
    public string Nom { get; set; } = string.Empty;

    [JsonPropertyName("estFinale")]
    public bool EstFinale { get; set; }

    [JsonPropertyName("fluxSortants")]
    public List<FluxSortant> FluxSortants { get; set; } = new();
}

public class NoeudMetier : NoeudProcessus
{
    [JsonPropertyName("nomCommande")]
    public string NomCommande { get; set; } = string.Empty;

    [JsonPropertyName("sourceAggregateId")]
    public ISourceParametre? SourceAggregateId { get; set; }

    [JsonPropertyName("parametres")]
    public Dictionary<string, ISourceParametre> Parametres { get; set; } = new();
}

public class NoeudInteractif : NoeudProcessus
{
    [JsonPropertyName("definitionTache")]
    public DefinitionTache DefinitionTache { get; set; } = new();

    [JsonPropertyName("commandePre")]
    public DefinitionCommande? CommandePre { get; set; }

    [JsonPropertyName("commandePost")]
    public DefinitionCommande? CommandePost { get; set; }
}

public class NoeudDecision : NoeudProcessus
{
    // Les conditions sont portées par FluxSortant (hérité)
}

public class NoeudAttenteTemps : NoeudProcessus
{
    [JsonPropertyName("sourceDateEcheance")]
    public ISourceParametre SourceDateEcheance { get; set; } = new SourceValeurStatique(null);
}

public class NoeudAttenteSignal : NoeudProcessus
{
    [JsonPropertyName("nomSignal")]
    public string NomSignal { get; set; } = string.Empty;
}

public class NoeudSousProcessus : NoeudProcessus
{
    [JsonPropertyName("cleDefinition")]
    public string CleDefinition { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("variablesSorties")]
    public List<string> VariablesSorties { get; set; } = new();
}
