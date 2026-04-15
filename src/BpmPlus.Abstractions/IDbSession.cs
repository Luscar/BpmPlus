using System.Data;

namespace BpmPlus.Abstractions;

/// <summary>
/// Encapsule la connexion et la transaction actives pour une unité de travail.
/// À enregistrer dans le conteneur IoC par scope de durée de vie (InstancePerLifetimeScope).
/// </summary>
public interface IDbSession
{
    IDbConnection Connection { get; }
    IDbTransaction? Transaction { get; }
}

/// <summary>
/// Implémentation par défaut d'IDbSession.
/// </summary>
public sealed class DbSession : IDbSession
{
    public IDbConnection Connection { get; }
    public IDbTransaction? Transaction { get; }

    public DbSession(IDbConnection connection, IDbTransaction? transaction = null)
    {
        Connection = connection;
        Transaction = transaction;
    }
}
