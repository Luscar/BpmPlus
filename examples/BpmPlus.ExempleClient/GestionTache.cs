using BpmPlus.Abstractions;

namespace BpmPlus.ExempleClient;

/// <summary>
/// Implémentation de démonstration de IGestionTache.
/// En production, cette classe interagirait avec un système de ticketing
/// (base de données, API externe, etc.) dans la même transaction.
/// </summary>
public class GestionTache : IGestionTache
{
    public Task CreerTacheAsync(
        DefinitionTache definitionTache,
        InstanceProcessus instance,
        string? logonAuto = null,
        CancellationToken ct = default)
    {
        Console.WriteLine($"  |   [GestionTache] Tâche créée — processus #{instance.Id}");
        Console.WriteLine($"  |                  Titre            : {definitionTache.Titre}");
        Console.WriteLine($"  |                  Description      : {definitionTache.Description}");
        Console.WriteLine($"  |                  NomNoeud         : {definitionTache.NomNoeud}");
        Console.WriteLine($"  |                  CodeRole         : {definitionTache.CodeRole}");
        Console.WriteLine($"  |                  CodeTache        : {definitionTache.CodeTache}");
        Console.WriteLine($"  |                  LogonAuteur      : {definitionTache.LogonAuteur}");
        Console.WriteLine($"  |                  LogonAuto        : {logonAuto ?? "(aucun)"}");
        Console.WriteLine($"  |                  Agrégat          : commande #{instance.AggregateId}");

        return Task.CompletedTask;
    }

    public Task FermerTacheAsync(
        InstanceProcessus instance,
        IReadOnlyDictionary<string, object?> variables,
        CancellationToken ct = default)
    {
        Console.WriteLine($"  |   [GestionTache] Tâche fermée — processus #{instance.Id}");
        Console.WriteLine($"  |                  Agrégat   : commande #{instance.AggregateId}");
        foreach (var (nom, valeur) in variables)
            Console.WriteLine($"  |                  Variable  : {nom} = {valeur}");
        return Task.CompletedTask;
    }

    public Task<long?> AssignerTacheAsync(long idProcessus, string assignee, CancellationToken ct = default)
    {
        Console.WriteLine($"  |   [GestionTache] Tâche assignée — processus #{idProcessus} → {assignee}");
        return Task.FromResult<long?>(null);
    }
}
