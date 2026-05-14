using System.Data;

namespace BpmPlus.Persistance.Oracle;

/// <summary>
/// Base commune pour tous les repositories Oracle.
/// </summary>
public abstract class OracleRepositoryBase
{
    protected readonly string Prefixe;
    protected readonly IDbConnection Cn;
    protected readonly IDbTransaction? Tx;

    protected OracleRepositoryBase(IDbConnection connection, string prefixe, IDbTransaction? tx = null)
    {
        Cn = connection;
        Tx = tx;
        Prefixe = prefixe.TrimEnd('_').ToUpperInvariant();
    }

    protected string T(string nomTable) => $"{Prefixe}_{nomTable}";

    /// <summary>Convertit un paramètre nommé @param en :param (convention Oracle).</summary>
    protected static string OraParam(string sql) => sql.Replace("@", ":");
}
