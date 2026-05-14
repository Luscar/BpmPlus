using System.Data;

namespace BpmPlus.Persistance.Sqlite;

/// <summary>
/// Base commune pour tous les repositories SQLite.
/// Fournit l'accès au préfixe de tables et à la connexion courante.
/// </summary>
public abstract class SqliteRepositoryBase
{
    protected readonly string Prefixe;
    protected readonly IDbConnection Cn;
    protected readonly IDbTransaction? Tx;

    protected SqliteRepositoryBase(IDbConnection connection, string prefixe, IDbTransaction? tx = null)
    {
        Cn = connection;
        Tx = tx;
        Prefixe = prefixe.TrimEnd('_').ToUpperInvariant();
    }

    protected string T(string nomTable) => $"{Prefixe}_{nomTable}";
}
