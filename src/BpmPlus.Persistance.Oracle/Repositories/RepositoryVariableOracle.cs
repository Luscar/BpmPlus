using System.Data;
using BpmPlus.Abstractions;
using BpmPlus.Core.Persistance;
using Dapper;

namespace BpmPlus.Persistance.Oracle.Repositories;

public class RepositoryVariableOracle : OracleRepositoryBase, IRepositoryVariable
{
    public RepositoryVariableOracle(IDbConnection connection, string prefixe, IDbTransaction? tx = null) : base(connection, prefixe, tx) { }

    public Task CreerTablesAsync(IDbConnection connection) => Task.CompletedTask;

    public async Task SauvegarderToutesAsync(
        long idInstance, IReadOnlyDictionary<string, object?> variables, CancellationToken ct = default)
    {
        await Cn.ExecuteAsync(OraParam($"""
            DELETE FROM {T("VAR_PROCS")} WHERE NO_SEQ_INSTC_PROCS = :IdInstance
            """), new { IdInstance = idInstance });

        foreach (var (nom, valeur) in variables)
        {
            var (type, valeurStr) = SerialiserValeur(valeur);
            await Cn.ExecuteAsync(OraParam($"""
                INSERT INTO {T("VAR_PROCS")} (NO_SEQ_VAR_PROCS, NO_SEQ_INSTC_PROCS, NOM_VAR, TYP_VAR, VAL_VAR)
                VALUES ({T("SEQ_VAR_PROCS")}.NEXTVAL, :IdInstance, :Nom, :Type, :Valeur)
                """),
                new { IdInstance = idInstance, Nom = nom, Type = type, Valeur = valeurStr },
                Tx);
        }
    }

    public async Task<Dictionary<string, object?>> ChargerToutesAsync(
        long idInstance, CancellationToken ct = default)
    {
        var rows = await Cn.QueryAsync(OraParam($"""
            SELECT NOM_VAR, TYP_VAR, VAL_VAR FROM {T("VAR_PROCS")} WHERE NO_SEQ_INSTC_PROCS = :IdInstance
            """), new { IdInstance = idInstance });

        var variables = new Dictionary<string, object?>();
        foreach (var row in rows)
            variables[(string)row.NOM_VAR] = DeserialiserValeur((string)row.TYP_VAR, (string)row.VAL_VAR);

        return variables;
    }

    public async Task MettreAJourAsync(
        long idInstance, string nom, object? valeur, CancellationToken ct = default)
    {
        var (type, valeurStr) = SerialiserValeur(valeur);

        await Cn.ExecuteAsync(OraParam($"""
            MERGE INTO {T("VAR_PROCS")} tgt
            USING (SELECT :IdInstance AS NO_SEQ_INSTC_PROCS, :Nom AS NOM_VAR FROM DUAL) src
            ON (tgt.NO_SEQ_INSTC_PROCS = src.NO_SEQ_INSTC_PROCS AND tgt.NOM_VAR = src.NOM_VAR)
            WHEN MATCHED THEN
                UPDATE SET tgt.TYP_VAR = :Type, tgt.VAL_VAR = :Valeur
            WHEN NOT MATCHED THEN
                INSERT (NO_SEQ_VAR_PROCS, NO_SEQ_INSTC_PROCS, NOM_VAR, TYP_VAR, VAL_VAR)
                VALUES ({T("SEQ_VAR_PROCS")}.NEXTVAL, :IdInstance, :Nom, :Type, :Valeur)
            """),
            new { IdInstance = idInstance, Nom = nom, Type = type, Valeur = valeurStr },
            Tx);
    }

    private static (string type, string valeur) SerialiserValeur(object? valeur)
    {
        if (valeur is null) return ("Null", string.Empty);
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
            "Null" => null,
            "Bool" => bool.Parse(valeur),
            "Int" => long.TryParse(valeur, out var l) ? l : (object?)int.Parse(valeur),
            "Decimal" => decimal.Parse(valeur, System.Globalization.CultureInfo.InvariantCulture),
            "DateTime" => DateTime.Parse(valeur),
            _ => valeur
        };
    }
}
