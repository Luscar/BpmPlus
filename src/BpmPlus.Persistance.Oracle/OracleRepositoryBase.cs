using System.Data;

namespace BpmPlus.Persistance.Oracle;

/// <summary>
/// Base commune pour tous les repositories Oracle.
/// </summary>
public abstract class OracleRepositoryBase
{
    protected readonly string Prefixe;
    private readonly Lazy<IDbConnection> _cn;
    protected IDbConnection Cn => _cn.Value;
    protected readonly IDbTransaction? Tx;

    // La connexion est résolue via Lazy<T> : elle n'est ouverte qu'au premier accès,
    // c'est-à-dire au moment où la première requête s'exécute. Si l'appelant a
    // démarré un TransactionScope avant d'appeler BpmPlus, la connexion s'ouvre à
    // l'intérieur de ce scope et s'y enrôle automatiquement.
    protected OracleRepositoryBase(Lazy<IDbConnection> connection, string prefixe, IDbTransaction? tx = null)
    {
        _cn = connection;
        Tx = tx;
        Prefixe = prefixe.TrimEnd('_').ToUpperInvariant();
    }

    protected string T(string nomTable) => $"{Prefixe}_{nomTable}";

    /// <summary>Convertit un paramètre nommé @param en :param (convention Oracle).</summary>
    protected static string OraParam(string sql) => sql.Replace("@", ":");
}
