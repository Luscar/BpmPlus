namespace BpmPlus.Abstractions;

/// <summary>
/// Contexte fourni à chaque handler lors de l'exécution d'un nœud.
/// Donne accès aux variables et aux informations de l'instance.
/// Pour accéder à la base de données, injectez IDbConnection directement dans votre handler.
/// </summary>
public interface IContexteExecution
{
    long IdInstance { get; }
    string CleDefinition { get; }
    int VersionDefinition { get; }
    long? AggregateId { get; }

    /// <summary>
    /// Accès en lecture et en écriture aux variables scalaires de l'instance.
    /// </summary>
    IAccesseurVariables Variables { get; }

    CancellationToken CancellationToken { get; }
}
