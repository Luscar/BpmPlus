using BpmPlus.Abstractions;

namespace BpmPlus.ExempleClient;

/// <summary>
/// Implémentation de démonstration de IGestionTache.
/// En production, cette classe interagirait avec un système de ticketing
/// (base de données, API externe, etc.) dans la même transaction.
/// </summary>
public class GestionTache : IGestionTache
{
    private static long _compteur;

    public Task<long> CreerTacheAsync(
        DefinitionTache definitionTache,
        InstanceProcessus instance,
        CancellationToken ct = default)
    {
        var idTache = ++_compteur;

        Console.WriteLine($"  |   [GestionTache] Tâche créée #{idTache}");
        Console.WriteLine($"  |                  Titre       : {definitionTache.Titre}");
        Console.WriteLine($"  |                  Description : {definitionTache.Description}");
        Console.WriteLine($"  |                  Agrégat     : commande #{instance.AggregateId}");

        return Task.FromResult(idTache);
    }

    public Task FermerTacheAsync(long idTacheExterne, CancellationToken ct = default)
    {
        Console.WriteLine($"  |   [GestionTache] Tâche #{idTacheExterne} fermée.");
        return Task.CompletedTask;
    }

    public Task AssignerTacheAsync(long idTacheExterne, string assignee, CancellationToken ct = default)
    {
        Console.WriteLine($"  |   [GestionTache] Tâche #{idTacheExterne} assignée à {assignee}.");
        return Task.CompletedTask;
    }
}
