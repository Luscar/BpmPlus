using BpmPlus.Abstractions;

namespace BpmPlus.IntegrationTests.Handlers;

/// <summary>
/// Handler de commande simple qui ne fait rien — utilisé dans les tests
/// pour simuler des nœuds métier sans logique applicative.
/// </summary>
public class NoOpCommand : IBpmHandlerCommande
{
    public string NomCommande => "NoOpCommand";

    public Task ExecuterAsync(
        long idInstance, long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte)
        => Task.CompletedTask;
}

public class TacheCommand : IBpmHandlerCommande
{
    public string NomCommande => "TacheCommand";
    public Task ExecuterAsync(long idInstance, long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres, IContexteExecution contexte)
        => Task.CompletedTask;
}

public class FinCommand : IBpmHandlerCommande
{
    public string NomCommande => "FinCommand";
    public Task ExecuterAsync(long idInstance, long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres, IContexteExecution contexte)
        => Task.CompletedTask;
}

/// <summary>
/// Handler qui définit une variable dans le contexte d'exécution.
/// Lit la clé "nom" et la valeur "valeur" dans les paramètres.
/// </summary>
public class DefinirVariableCommand : IBpmHandlerCommande
{
    public string NomCommande => "DefinirVariableCommand";

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
