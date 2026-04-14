using System.Data;
using Dapper;

namespace BpmPlus.Persistance.Sqlite;

/// <summary>
/// Base commune pour tous les repositories SQLite.
/// Fournit l'accès au préfixe de tables et aux helpers Dapper.
/// </summary>
public abstract class SqliteRepositoryBase
{
    protected readonly string Prefixe;

    protected SqliteRepositoryBase(string prefixe)
    {
        Prefixe = prefixe.TrimEnd('_').ToUpperInvariant();
    }

    protected string T(string nomTable) => $"{Prefixe}_{nomTable}";

    protected IDbConnection Cn(IDbTransaction transaction)
        => transaction.Connection
           ?? throw new InvalidOperationException("La transaction ne possède pas de connexion active.");
}
