using System.Data;
using BpmPlus.Abstractions;
using BpmPlus.Core.Definition;
using BpmPlus.Core.Persistance;
using Dapper;

namespace BpmPlus.Persistance.Oracle.Repositories;

public class RepositoryDefinitionOracle : OracleRepositoryBase, IRepositoryDefinition
{
    public RepositoryDefinitionOracle(IDbConnection connection, string prefixe) : base(connection, prefixe) { }

    public async Task CreerTablesAsync(IDbConnection connection)
    {
        // En production Oracle, les tables doivent être créées via scripts DDL séparés
        await Task.CompletedTask;
    }

    public async Task<long> SauvegarderAsync(DefinitionProcessus definition, CancellationToken ct = default)
    {
        var json = JsonDefinitionParser.Serialiser(definition);
        var maintenant = DateTime.UtcNow;

        if (definition.Id.HasValue)
        {
            await Cn.ExecuteAsync(OraParam($"""
                UPDATE {T("DEFIN_PROCS")}
                SET NOM_DEFIN = :Nom,
                    DEFIN_JSON = :Json,
                    DH_CREA = :DateCreation
                WHERE NO_SEQ_DEFIN_PROCS = :Id
                """),
                new { definition.Nom, Json = json, DateCreation = maintenant, definition.Id },
                Tx);
            return definition.Id.Value;
        }

        var derniereVersion = await Cn.QuerySingleOrDefaultAsync<int?>(OraParam($"""
            SELECT MAX(VERSI) FROM {T("DEFIN_PROCS")} WHERE CLE = :Cle
            """), new { definition.Cle }) ?? 0;

        var nouvelleVersion = derniereVersion + 1;

        var id = await Cn.QuerySingleAsync<long>(OraParam($"""
            INSERT INTO {T("DEFIN_PROCS")} (NO_SEQ_DEFIN_PROCS, CLE, VERSI, NOM_DEFIN, STAT, DEFIN_JSON, DH_CREA)
            VALUES ({T("SEQ_DEFIN")}.NEXTVAL, :Cle, :Version, :Nom, 'Brouillon', :Json, :DateCreation)
            RETURNING NO_SEQ_DEFIN_PROCS INTO :NewId
            """),
            new { definition.Cle, Version = nouvelleVersion, definition.Nom, Json = json, DateCreation = maintenant },
            Tx);

        return id;
    }

    public async Task<DefinitionProcessus?> ObtenirBrouillonAsync(string cle, CancellationToken ct = default)
    {
        var row = await Cn.QuerySingleOrDefaultAsync(OraParam($"""
            SELECT * FROM {T("DEFIN_PROCS")}
            WHERE CLE = :Cle AND STAT = 'Brouillon'
            ORDER BY VERSI DESC
            FETCH FIRST 1 ROW ONLY
            """), new { Cle = cle });

        return row is null ? null : MapperDefinition(row);
    }

    public async Task<DefinitionProcessus?> ObtenirVersionPublieeAsync(
        string cle, int version, CancellationToken ct = default)
    {
        var row = await Cn.QuerySingleOrDefaultAsync(OraParam($"""
            SELECT * FROM {T("DEFIN_PROCS")}
            WHERE CLE = :Cle AND VERSI = :Version AND STAT = 'Publiee'
            """), new { Cle = cle, Version = version });

        return row is null ? null : MapperDefinition(row);
    }

    public async Task<DefinitionProcessus?> ObtenirDerniereVersionPublieeAsync(
        string cle, CancellationToken ct = default)
    {
        var row = await Cn.QuerySingleOrDefaultAsync(OraParam($"""
            SELECT * FROM {T("DEFIN_PROCS")}
            WHERE CLE = :Cle AND STAT = 'Publiee'
            ORDER BY VERSI DESC
            FETCH FIRST 1 ROW ONLY
            """), new { Cle = cle });

        return row is null ? null : MapperDefinition(row);
    }

    public async Task PublierAsync(string cle, CancellationToken ct = default)
    {
        await Cn.ExecuteAsync(OraParam($"""
            UPDATE {T("DEFIN_PROCS")}
            SET STAT = 'Publiee', DH_PUBL = :DatePublication
            WHERE CLE = :Cle AND STAT = 'Brouillon'
            """), new { Cle = cle, DatePublication = DateTime.UtcNow });
    }

    public async Task<IReadOnlyList<DefinitionProcessus>> ObtenirToutesAsync(CancellationToken ct = default)
    {
        var rows = await Cn.QueryAsync($"""
            SELECT * FROM {T("DEFIN_PROCS")} ORDER BY CLE, VERSI
            """);

        return rows.Select(r => (DefinitionProcessus)MapperDefinition(r)).ToList();
    }

    private static DefinitionProcessus MapperDefinition(dynamic row)
    {
        var def = JsonDefinitionParser.Deserialiser((string)row.DEFIN_JSON);
        def.Id = Convert.ToInt64(row.NO_SEQ_DEFIN_PROCS);
        def.Version = Convert.ToInt32(row.VERSI);
        def.Statut = ((string)row.STAT) == "Publiee"
            ? StatutDefinition.Publiee : StatutDefinition.Brouillon;
        def.DateCreation = Convert.ToDateTime(row.DH_CREA);
        def.DatePublication = row.DH_PUBL is not null
            ? Convert.ToDateTime(row.DH_PUBL)
            : null;
        return def;
    }
}
