using BpmPlus.Abstractions;

namespace BpmPlus.IntegrationTests.Handlers;

/// <summary>
/// Handler de commande simple qui ne fait rien — utilisé dans les tests
/// pour simuler des nœuds métier sans logique applicative.
/// </summary>
[BpmCommande("NoOpCommand")]
public class NoOpCommand : IBpmHandlerCommande
{
    public Task ExecuterAsync(
        long idInstance, long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte)
        => Task.CompletedTask;
}

[BpmCommande("TacheCommand")]
public class TacheCommand : IBpmHandlerCommande
{
    public Task ExecuterAsync(long idInstance, long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres, IContexteExecution contexte)
        => Task.CompletedTask;
}

[BpmCommande("FinCommand")]
public class FinCommand : IBpmHandlerCommande
{
    public Task ExecuterAsync(long idInstance, long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres, IContexteExecution contexte)
        => Task.CompletedTask;
}

/// <summary>
/// Handler qui définit une variable dans le contexte d'exécution.
/// Lit la clé "nom" et la valeur "valeur" dans les paramètres.
/// </summary>
[BpmCommande("DefinirVariableCommand")]
public class DefinirVariableCommand : IBpmHandlerCommande
{
    public Task ExecuterAsync(
        long idInstance, long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte)
    {
        if (parametres.TryGetValue("nom", out var nom) && nom is string nomVar &&
            parametres.TryGetValue("valeur", out var valeur))
        {
            contexte.Variables.Definir(nomVar, valeur);
        }
        return Task.CompletedTask;
    }
}
