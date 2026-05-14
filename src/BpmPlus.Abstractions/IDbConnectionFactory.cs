using System.Data;

namespace BpmPlus.Abstractions;

/// <summary>
/// Ouvre une nouvelle connexion à la base de données.
/// Utiliser ce contrat plutôt qu'une IDbConnection pré-ouverte permet d'ouvrir
/// la connexion à l'intérieur d'un TransactionScope ambiant, ce qui garantit
/// l'enrôlement automatique et donc le rollback des données BpmPlus.
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>Crée et retourne une connexion déjà ouverte.</summary>
    IDbConnection CreateOpenConnection();

    /// <summary>Crée et retourne une connexion déjà ouverte (variante async).</summary>
    Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken ct = default);
}
