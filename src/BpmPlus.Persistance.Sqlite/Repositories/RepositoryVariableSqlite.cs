using System.Data;
using BpmPlus.Core.Persistance;
using Dapper;

namespace BpmPlus.Persistance.Sqlite.Repositories;

public class RepositoryVariableSqlite : SqliteRepositoryBase, IRepositoryVariable
{
    public RepositoryVariableSqlite(string prefixe) : base(prefixe) { }

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
        long idInstance, IReadOnlyDictionary<string, object?> variables,
        IDbTransaction transaction, CancellationToken ct = default)
    {
        // Supprimer puis réinsérer (stratégie simple)
        await Cn(transaction).ExecuteAsync($"""
            DELETE FROM {T("VARIABLE_PROCESSUS")} WHERE ID_INSTANCE = @IdInstance
            """, new { IdInstance = idInstance }, transaction);

        foreach (var (nom, valeur) in variables)
        {
            var (type, valeurStr) = SerialiserValeur(valeur);
            await Cn(transaction).ExecuteAsync($"""
                INSERT INTO {T("VARIABLE_PROCESSUS")} (ID_INSTANCE, NOM, TYPE, VALEUR)
                VALUES (@IdInstance, @Nom, @Type, @Valeur)
                """,
                new { IdInstance = idInstance, Nom = nom, Type = type, Valeur = valeurStr },
                transaction);
        }
    }

    public async Task<Dictionary<string, object?>> ChargerToutesAsync(
        long idInstance, IDbTransaction transaction, CancellationToken ct = default)
    {
        var rows = await Cn(transaction).QueryAsync($"""
            SELECT NOM, TYPE, VALEUR FROM {T("VARIABLE_PROCESSUS")} WHERE ID_INSTANCE = @IdInstance
            """, new { IdInstance = idInstance }, transaction);

        var variables = new Dictionary<string, object?>();
        foreach (var row in rows)
            variables[(string)row.NOM] = DeserialiserValeur((string)row.TYPE, (string)row.VALEUR);

        return variables;
    }

    public async Task MettreAJourAsync(
        long idInstance, string nom, object? valeur,
        IDbTransaction transaction, CancellationToken ct = default)
    {
        var (type, valeurStr) = SerialiserValeur(valeur);
        await Cn(transaction).ExecuteAsync($"""
            INSERT INTO {T("VARIABLE_PROCESSUS")} (ID_INSTANCE, NOM, TYPE, VALEUR)
            VALUES (@IdInstance, @Nom, @Type, @Valeur)
            ON CONFLICT(ID_INSTANCE, NOM) DO UPDATE
            SET TYPE = excluded.TYPE, VALEUR = excluded.VALEUR
            """,
            new { IdInstance = idInstance, Nom = nom, Type = type, Valeur = valeurStr },
            transaction);
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
