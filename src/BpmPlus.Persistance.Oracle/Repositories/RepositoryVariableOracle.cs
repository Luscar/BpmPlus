using System.Data;
using BpmPlus.Abstractions;
using BpmPlus.Core.Persistance;
using Dapper;

namespace BpmPlus.Persistance.Oracle.Repositories;

public class RepositoryVariableOracle : OracleRepositoryBase, IRepositoryVariable
{
    public RepositoryVariableOracle(IDbSession session, string prefixe) : base(session, prefixe) { }

    public Task CreerTablesAsync(IDbConnection connection) => Task.CompletedTask;

    public async Task SauvegarderToutesAsync(
        long idInstance, IReadOnlyDictionary<string, object?> variables, CancellationToken ct = default)
    {
        await Cn.ExecuteAsync(OraParam($"""
            DELETE FROM {T("VARIABLE_PROCESSUS")} WHERE ID_INSTANCE = :IdInstance
            """), new { IdInstance = idInstance }, Tx);

        foreach (var (nom, valeur) in variables)
        {
            var (type, valeurStr) = SerialiserValeur(valeur);
            await Cn.ExecuteAsync(OraParam($"""
                INSERT INTO {T("VARIABLE_PROCESSUS")} (ID, ID_INSTANCE, NOM, TYPE, VALEUR)
                VALUES ({T("SEQ_VARIABLE")}.NEXTVAL, :IdInstance, :Nom, :Type, :Valeur)
                """),
                new { IdInstance = idInstance, Nom = nom, Type = type, Valeur = valeurStr },
                Tx);
        }
    }

    public async Task<Dictionary<string, object?>> ChargerToutesAsync(
        long idInstance, CancellationToken ct = default)
    {
        var rows = await Cn.QueryAsync(OraParam($"""
            SELECT NOM, TYPE, VALEUR FROM {T("VARIABLE_PROCESSUS")} WHERE ID_INSTANCE = :IdInstance
            """), new { IdInstance = idInstance }, Tx);

        var variables = new Dictionary<string, object?>();
        foreach (var row in rows)
            variables[(string)row.NOM] = DeserialiserValeur((string)row.TYPE, (string)row.VALEUR);

        return variables;
    }

    public async Task MettreAJourAsync(
        long idInstance, string nom, object? valeur, CancellationToken ct = default)
    {
        var (type, valeurStr) = SerialiserValeur(valeur);

        await Cn.ExecuteAsync(OraParam($"""
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
