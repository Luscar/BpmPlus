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

    protected SqliteRepositoryBase(IDbConnection connection, string prefixe)
    {
        Cn = connection;
        Prefixe = prefixe.TrimEnd('_').ToUpperInvariant();
    }

    protected string T(string nomTable) => $"{Prefixe}_{nomTable}";
}
