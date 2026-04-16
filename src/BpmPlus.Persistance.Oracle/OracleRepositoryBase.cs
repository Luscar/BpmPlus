using System.Data;

namespace BpmPlus.Persistance.Oracle;

/// <summary>
/// Base commune pour tous les repositories Oracle.
/// </summary>
public abstract class OracleRepositoryBase
{
    protected readonly string Prefixe;
    protected readonly IDbConnection Cn;

    protected OracleRepositoryBase(IDbConnection connection, string prefixe)
    {
        Cn = connection;
        Prefixe = prefixe.TrimEnd('_').ToUpperInvariant();
    }

    protected string T(string nomTable) => $"{Prefixe}_{nomTable}";

    /// <summary>Convertit un paramètre nommé @param en :param (convention Oracle).</summary>
    protected static string OraParam(string sql) => sql.Replace("@", ":");
}
