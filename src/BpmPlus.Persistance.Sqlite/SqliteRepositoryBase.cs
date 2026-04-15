using System.Data;
using BpmPlus.Abstractions;

namespace BpmPlus.Persistance.Sqlite;

/// <summary>
/// Base commune pour tous les repositories SQLite.
/// Fournit l'accès au préfixe de tables et à la session courante.
/// </summary>
public abstract class SqliteRepositoryBase
{
    protected readonly string Prefixe;
    protected readonly IDbSession Session;

    protected SqliteRepositoryBase(IDbSession session, string prefixe)
    {
        Session = session;
        Prefixe = prefixe.TrimEnd('_').ToUpperInvariant();
    }

    protected string T(string nomTable) => $"{Prefixe}_{nomTable}";

    protected IDbConnection Cn => Session.Connection;
    protected IDbTransaction? Tx => Session.Transaction;
}
