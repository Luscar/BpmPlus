using System.Text.Json;
using System.Text.Json.Serialization;
using BpmPlus.Abstractions;

namespace BpmPlus.Core.Definition;

/// <summary>
/// Sérialise et désérialise une DefinitionProcessus vers/depuis JSON maison.
/// </summary>
public static class JsonDefinitionParser
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    public static string Serialiser(DefinitionProcessus definition)
        => JsonSerializer.Serialize(definition, _options);

    public static DefinitionProcessus Deserialiser(string json)
    {
        var definition = JsonSerializer.Deserialize<DefinitionProcessus>(json, _options)
            ?? throw new JsonException("Impossible de désérialiser la définition de processus.");

        ValiderDefinition(definition);
        return definition;
    }

    private static void ValiderDefinition(DefinitionProcessus def)
    {
        if (string.IsNullOrWhiteSpace(def.Cle))
            throw new JsonException("La propriété 'cle' est obligatoire.");
        if (string.IsNullOrWhiteSpace(def.NoeudDebutId))
            throw new JsonException("La propriété 'noeudDebut' est obligatoire.");
        if (def.Noeuds.Count == 0)
            throw new JsonException("La définition doit contenir au moins un nœud.");

        var ids = def.Noeuds.Select(n => n.Id).ToHashSet();
        if (!ids.Contains(def.NoeudDebutId))
            throw new JsonException(
                $"Le nœud de début '{def.NoeudDebutId}' n'existe pas dans la liste des nœuds.");

        // Valider que tous les flux pointent vers des nœuds existants
        foreach (var noeud in def.Noeuds)
        {
            foreach (var flux in noeud.FluxSortants)
            {
                if (!string.IsNullOrWhiteSpace(flux.Vers) && !ids.Contains(flux.Vers))
                    throw new JsonException(
                        $"Le nœud '{noeud.Id}' référence le nœud '{flux.Vers}' qui n'existe pas.");
            }
        }
    }
}
