using System.Data;
using BpmPlus.Core.Persistance;
using Dapper;

namespace BpmPlus.Persistance.Oracle.Repositories;

public class RepositoryVariableOracle : OracleRepositoryBase, IRepositoryVariable
{
    public RepositoryVariableOracle(string prefixe) : base(prefixe) { }

    public Task CreerTablesAsync(IDbConnection connection) => Task.CompletedTask;

    public async Task SauvegarderToutesAsync(
        long idInstance, IReadOnlyDictionary<string, object?> variables,
        IDbTransaction transaction, CancellationToken ct = default)
    {
        await Cn(transaction).ExecuteAsync(OraParam($"""
            DELETE FROM {T("VARIABLE_PROCESSUS")} WHERE ID_INSTANCE = :IdInstance
            """), new { IdInstance = idInstance }, transaction);

        foreach (var (nom, valeur) in variables)
        {
            var (type, valeurStr) = SerialiserValeur(valeur);
            await Cn(transaction).ExecuteAsync(OraParam($"""
                INSERT INTO {T("VARIABLE_PROCESSUS")} (ID, ID_INSTANCE, NOM, TYPE, VALEUR)
                VALUES ({T("SEQ_VARIABLE")}.NEXTVAL, :IdInstance, :Nom, :Type, :Valeur)
                """),
                new { IdInstance = idInstance, Nom = nom, Type = type, Valeur = valeurStr },
                transaction);
        }
    }

    public async Task<Dictionary<string, object?>> ChargerToutesAsync(
        long idInstance, IDbTransaction transaction, CancellationToken ct = default)
    {
        var rows = await Cn(transaction).QueryAsync(OraParam($"""
            SELECT NOM, TYPE, VALEUR FROM {T("VARIABLE_PROCESSUS")} WHERE ID_INSTANCE = :IdInstance
            """), new { IdInstance = idInstance }, transaction);

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

        // Oracle MERGE pour upsert
        await Cn(transaction).ExecuteAsync(OraParam($"""
            MERGE INTO {T("VARIABLE_PROCESSUS")} tgt
            USING (SELECT :IdInstance AS ID_INSTANCE, :Nom AS NOM FROM DUAL) src
            ON (tgt.ID_INSTANCE = src.ID_INSTANCE AND tgt.NOM = src.NOM)
            WHEN MATCHED THEN
                UPDATE SET tgt.TYPE = :Type, tgt.VALEUR = :Valeur
            WHEN NOT MATCHED THEN
                INSERT (ID, ID_INSTANCE, NOM, TYPE, VALEUR)
                VALUES ({T("SEQ_VARIABLE")}.NEXTVAL, :IdInstance, :Nom, :Type, :Valeur)
            """),
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
