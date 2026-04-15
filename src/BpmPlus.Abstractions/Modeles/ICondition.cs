using System.Text.Json.Serialization;

namespace BpmPlus.Abstractions;

[JsonDerivedType(typeof(ConditionVariable), "ConditionVariable")]
[JsonDerivedType(typeof(ConditionQuery), "ConditionQuery")]
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
public interface ICondition { }

public record ConditionVariable(
    [property: JsonPropertyName("nomVariable")] string NomVariable,
    [property: JsonPropertyName("operateur")] Operateur Operateur,
    [property: JsonPropertyName("valeur")] object? Valeur
) : ICondition;

public record ConditionQuery(
    [property: JsonPropertyName("nomQuery")] string NomQuery,
    [property: JsonPropertyName("parametres")] Dictionary<string, ISourceParametre>? Parametres = null
) : ICondition;
