using System.Data;

namespace BpmPlus.Abstractions;

/// <summary>
/// Contexte fourni à chaque handler lors de l'exécution d'un nœud.
/// Donne accès à la session de base de données, aux variables et aux informations de l'instance.
/// </summary>
public interface IContexteExecution
{
    long IdInstance { get; }
    string CleDefinition { get; }
    int VersionDefinition { get; }
    long? AggregateId { get; }

    /// <summary>
    /// Session de base de données active (connexion + transaction). Fournie par l'application cliente.
    /// </summary>
    IDbSession Session { get; }

    /// <summary>
    /// Transaction de base de données active. Raccourci vers Session.Transaction.
    /// </summary>
    IDbTransaction? Transaction { get; }

    /// <summary>
    /// Accès en lecture et en écriture aux variables scalaires de l'instance.
    /// </summary>
    IAccesseurVariables Variables { get; }

    CancellationToken CancellationToken { get; }
}
