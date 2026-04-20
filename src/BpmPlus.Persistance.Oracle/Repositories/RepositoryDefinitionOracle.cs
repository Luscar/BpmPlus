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
                UPDATE {T("DEFINITION_PROCESSUS")}
                SET NOM = :Nom,
                    DEFINITION_JSON = :Json,
                    DATE_CREATION = :DateCreation
                WHERE ID = :Id
                """),
                new { definition.Nom, Json = json, DateCreation = maintenant, definition.Id },
                Tx);
            return definition.Id.Value;
        }

        var derniereVersion = await Cn.QuerySingleOrDefaultAsync<int?>(OraParam($"""
            SELECT MAX(VERSION) FROM {T("DEFINITION_PROCESSUS")} WHERE CLE = :Cle
            """), new { definition.Cle }) ?? 0;

        var nouvelleVersion = derniereVersion + 1;

        var id = await Cn.QuerySingleAsync<long>(OraParam($"""
            INSERT INTO {T("DEFINITION_PROCESSUS")} (ID, CLE, VERSION, NOM, STATUT, DEFINITION_JSON, DATE_CREATION)
            VALUES ({T("SEQ_DEFINITION")}.NEXTVAL, :Cle, :Version, :Nom, 'Brouillon', :Json, :DateCreation)
            RETURNING ID INTO :NewId
            """),
            new { definition.Cle, Version = nouvelleVersion, definition.Nom, Json = json, DateCreation = maintenant },
            Tx);

        return id;
    }

    public async Task<DefinitionProcessus?> ObtenirBrouillonAsync(string cle, CancellationToken ct = default)
    {
        var row = await Cn.QuerySingleOrDefaultAsync(OraParam($"""
            SELECT * FROM {T("DEFINITION_PROCESSUS")}
            WHERE CLE = :Cle AND STATUT = 'Brouillon'
            ORDER BY VERSION DESC
            FETCH FIRST 1 ROW ONLY
            """), new { Cle = cle });

        return row is null ? null : MapperDefinition(row);
    }

    public async Task<DefinitionProcessus?> ObtenirVersionPublieeAsync(
        string cle, int version, CancellationToken ct = default)
    {
        var row = await Cn.QuerySingleOrDefaultAsync(OraParam($"""
            SELECT * FROM {T("DEFINITION_PROCESSUS")}
            WHERE CLE = :Cle AND VERSION = :Version AND STATUT = 'Publiee'
            """), new { Cle = cle, Version = version });

        return row is null ? null : MapperDefinition(row);
    }

    public async Task<DefinitionProcessus?> ObtenirDerniereVersionPublieeAsync(
        string cle, CancellationToken ct = default)
    {
        var row = await Cn.QuerySingleOrDefaultAsync(OraParam($"""
            SELECT * FROM {T("DEFINITION_PROCESSUS")}
            WHERE CLE = :Cle AND STATUT = 'Publiee'
            ORDER BY VERSION DESC
            FETCH FIRST 1 ROW ONLY
            """), new { Cle = cle });

        return row is null ? null : MapperDefinition(row);
    }

    public async Task PublierAsync(string cle, CancellationToken ct = default)
    {
        await Cn.ExecuteAsync(OraParam($"""
            UPDATE {T("DEFINITION_PROCESSUS")}
            SET STATUT = 'Publiee', DATE_PUBLICATION = :DatePublication
            WHERE CLE = :Cle AND STATUT = 'Brouillon'
            """), new { Cle = cle, DatePublication = DateTime.UtcNow });
    }

    public async Task<IReadOnlyList<DefinitionProcessus>> ObtenirToutesAsync(CancellationToken ct = default)
    {
        var rows = await Cn.QueryAsync($"""
            SELECT * FROM {T("DEFINITION_PROCESSUS")} ORDER BY CLE, VERSION
            """);

        return rows.Select(r => (DefinitionProcessus)MapperDefinition(r)).ToList();
    }

    private static DefinitionProcessus MapperDefinition(dynamic row)
    {
        var def = JsonDefinitionParser.Deserialiser((string)row.DEFINITION_JSON);
        def.Id = Convert.ToInt64(row.ID);
        def.Version = Convert.ToInt32(row.VERSION);
        def.Statut = ((string)row.STATUT) == "Publiee"
            ? StatutDefinition.Publiee : StatutDefinition.Brouillon;
        def.DateCreation = Convert.ToDateTime(row.DATE_CREATION);
        def.DatePublication = row.DATE_PUBLICATION is not null
            ? Convert.ToDateTime(row.DATE_PUBLICATION)
            : null;
        return def;
    }
}
