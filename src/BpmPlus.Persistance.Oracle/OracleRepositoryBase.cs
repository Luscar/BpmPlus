using System.Data;
using Dapper;

namespace BpmPlus.Persistance.Oracle;

/// <summary>
/// Base commune pour tous les repositories Oracle.
/// </summary>
public abstract class OracleRepositoryBase
{
    protected readonly string Prefixe;

    protected OracleRepositoryBase(string prefixe)
    {
        Prefixe = prefixe.TrimEnd('_').ToUpperInvariant();
    }

    protected string T(string nomTable) => $"{Prefixe}_{nomTable}";

    protected IDbConnection Cn(IDbTransaction transaction)
        => transaction.Connection
           ?? throw new InvalidOperationException("La transaction ne possède pas de connexion active.");

    /// <summary>Convertit un paramètre nommé @param en :param (convention Oracle).</summary>
    protected static string OraParam(string sql) => sql.Replace("@", ":");
}
