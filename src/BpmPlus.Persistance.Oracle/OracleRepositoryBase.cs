using System.Data;
using BpmPlus.Abstractions;

namespace BpmPlus.Persistance.Oracle;

/// <summary>
/// Base commune pour tous les repositories Oracle.
/// </summary>
public abstract class OracleRepositoryBase
{
    protected readonly string Prefixe;
    protected readonly IDbSession Session;

    protected OracleRepositoryBase(IDbSession session, string prefixe)
    {
        Session = session;
        Prefixe = prefixe.TrimEnd('_').ToUpperInvariant();
    }

    protected string T(string nomTable) => $"{Prefixe}_{nomTable}";

    protected IDbConnection Cn => Session.Connection;
    protected IDbTransaction? Tx => Session.Transaction;

    /// <summary>Convertit un paramètre nommé @param en :param (convention Oracle).</summary>
    protected static string OraParam(string sql) => sql.Replace("@", ":");
}
