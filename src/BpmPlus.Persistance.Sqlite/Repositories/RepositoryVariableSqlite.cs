using System.Data;
using BpmPlus.Abstractions;
using BpmPlus.Core.Persistance;
using Dapper;

namespace BpmPlus.Persistance.Sqlite.Repositories;

public class RepositoryVariableSqlite : SqliteRepositoryBase, IRepositoryVariable
{
    public RepositoryVariableSqlite(IDbSession session, string prefixe) : base(session, prefixe) { }

    public async Task CreerTablesAsync(IDbConnection connection)
    {
        await connection.ExecuteAsync($"""
            CREATE TABLE IF NOT EXISTS {T("VARIABLE_PROCESSUS")} (
                ID          INTEGER PRIMARY KEY AUTOINCREMENT,
                ID_INSTANCE INTEGER NOT NULL,
                NOM         TEXT    NOT NULL,
                TYPE        TEXT    NOT NULL,
                VALEUR      TEXT    NOT NULL,
                UNIQUE(ID_INSTANCE, NOM)
            )
            """);
    }

    public async Task SauvegarderToutesAsync(
        long idInstance, IReadOnlyDictionary<string, object?> variables, CancellationToken ct = default)
    {
        await Cn.ExecuteAsync($"""
            DELETE FROM {T("VARIABLE_PROCESSUS")} WHERE ID_INSTANCE = @IdInstance
            """, new { IdInstance = idInstance }, Tx);

        foreach (var (nom, valeur) in variables)
        {
            var (type, valeurStr) = SerialiserValeur(valeur);
            await Cn.ExecuteAsync($"""
                INSERT INTO {T("VARIABLE_PROCESSUS")} (ID_INSTANCE, NOM, TYPE, VALEUR)
                VALUES (@IdInstance, @Nom, @Type, @Valeur)
                """,
                new { IdInstance = idInstance, Nom = nom, Type = type, Valeur = valeurStr },
                Tx);
        }
    }

    public async Task<Dictionary<string, object?>> ChargerToutesAsync(
        long idInstance, CancellationToken ct = default)
    {
        var rows = await Cn.QueryAsync($"""
            SELECT NOM, TYPE, VALEUR FROM {T("VARIABLE_PROCESSUS")} WHERE ID_INSTANCE = @IdInstance
            """, new { IdInstance = idInstance }, Tx);

        var variables = new Dictionary<string, object?>();
        foreach (var row in rows)
            variables[(string)row.NOM] = DeserialiserValeur((string)row.TYPE, (string)row.VALEUR);

        return variables;
    }

    public async Task MettreAJourAsync(
        long idInstance, string nom, object? valeur, CancellationToken ct = default)
    {
        var (type, valeurStr) = SerialiserValeur(valeur);
        await Cn.ExecuteAsync($"""
            INSERT INTO {T("VARIABLE_PROCESSUS")} (ID_INSTANCE, NOM, TYPE, VALEUR)
            VALUES (@IdInstance, @Nom, @Type, @Valeur)
            ON CONFLICT(ID_INSTANCE, NOM) DO UPDATE
            SET TYPE = excluded.TYPE, VALEUR = excluded.VALEUR
            """,
            new { IdInstance = idInstance, Nom = nom, Type = type, Valeur = valeurStr },
            Tx);
    }

    private static (string type, string valeur) SerialiserValeur(object? valeur)
    {
        if (valeur is null) return ("String", string.Empty);
        return valeur switch
        {
            bool b => ("Bool", b.ToString()),
            int i => ("Int", i.ToString()),
            long l => ("Int", l.ToString()),
            decimal d => ("Decimal", d.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            double dbl => ("Decimal", ((decimal)dbl).ToString(System.Globalization.CultureInfo.InvariantCulture)),
            DateTime dt => ("DateTime", dt.ToString("O")),
            _ => ("String", valeur.ToString() ?? string.Empty)
        };
    }

    private static object? DeserialiserValeur(string type, string valeur)
    {
        return type switch
        {
            "Bool" => bool.Parse(valeur),
            "Int" => long.TryParse(valeur, out var l) ? l : (object?)int.Parse(valeur),
            "Decimal" => decimal.Parse(valeur, System.Globalization.CultureInfo.InvariantCulture),
            "DateTime" => DateTime.Parse(valeur),
            _ => valeur
        };
    }
}
