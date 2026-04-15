using System.Text.Json.Serialization;

namespace BpmPlus.Abstractions;

[JsonDerivedType(typeof(SourceVariable), "Variable")]
[JsonDerivedType(typeof(SourceValeurStatique), "Statique")]
[JsonDerivedType(typeof(SourceQuery), "Query")]
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
public interface ISourceParametre { }

public record SourceVariable(
    [property: JsonPropertyName("nom")] string NomVariable
) : ISourceParametre;

public record SourceValeurStatique(
    [property: JsonPropertyName("valeur")] object? Valeur
) : ISourceParametre;

public record SourceQuery(
    [property: JsonPropertyName("nomQuery")] string NomQuery,
    [property: JsonPropertyName("parametres")] Dictionary<string, ISourceParametre>? Parametres = null
) : ISourceParametre;
