using System.Data;
using BpmPlus.Abstractions;
using BpmPlus.Core.Definition;
using BpmPlus.Core.Persistance;
using Dapper;

namespace BpmPlus.Persistance.Sqlite.Repositories;

public class RepositoryDefinitionSqlite : SqliteRepositoryBase, IRepositoryDefinition
{
    public RepositoryDefinitionSqlite(IDbConnection connection, string prefixe) : base(connection, prefixe) { }

    public async Task CreerTablesAsync(IDbConnection connection)
    {
        await connection.ExecuteAsync($"""
            CREATE TABLE IF NOT EXISTS {T("DEFINITION_PROCESSUS")} (
                ID               INTEGER PRIMARY KEY AUTOINCREMENT,
                CLE              TEXT    NOT NULL,
                VERSION          INTEGER NOT NULL,
                NOM              TEXT    NOT NULL,
                STATUT           TEXT    NOT NULL,
                DEFINITION_JSON  TEXT    NOT NULL,
                DATE_CREATION    TEXT    NOT NULL,
                DATE_PUBLICATION TEXT    NULL,
                UNIQUE(CLE, VERSION)
            )
            """);
    }

    public async Task<long> SauvegarderAsync(DefinitionProcessus definition, CancellationToken ct = default)
    {
        var json = JsonDefinitionParser.Serialiser(definition);
        var maintenant = DateTime.UtcNow.ToString("O");

        if (definition.Id.HasValue)
        {
            await Cn.ExecuteAsync($"""
                UPDATE {T("DEFINITION_PROCESSUS")}
                SET NOM = @Nom,
                    DEFINITION_JSON = @Json,
                    DATE_CREATION = @DateCreation
                WHERE ID = @Id
                """,
                new { definition.Nom, Json = json, DateCreation = maintenant, definition.Id },
                Tx);
            return definition.Id.Value;
        }

        var derniereVersion = await Cn.QuerySingleOrDefaultAsync<int?>($"""
            SELECT MAX(VERSION) FROM {T("DEFINITION_PROCESSUS")} WHERE CLE = @Cle
            """, new { definition.Cle }) ?? 0;

        var nouvelleVersion = derniereVersion + 1;

        var id = await Cn.QuerySingleAsync<long>($"""
            INSERT INTO {T("DEFINITION_PROCESSUS")} (CLE, VERSION, NOM, STATUT, DEFINITION_JSON, DATE_CREATION)
            VALUES (@Cle, @Version, @Nom, 'Brouillon', @Json, @DateCreation);
            SELECT last_insert_rowid()
            """,
            new { definition.Cle, Version = nouvelleVersion, definition.Nom, Json = json, DateCreation = maintenant },
            Tx);

        return id;
    }

    public async Task<DefinitionProcessus?> ObtenirBrouillonAsync(string cle, CancellationToken ct = default)
    {
        var row = await Cn.QuerySingleOrDefaultAsync($"""
            SELECT * FROM {T("DEFINITION_PROCESSUS")}
            WHERE CLE = @Cle AND STATUT = 'Brouillon'
            ORDER BY VERSION DESC LIMIT 1
            """, new { Cle = cle });

        return row is null ? null : MapperDefinition(row);
    }

    public async Task<DefinitionProcessus?> ObtenirVersionPublieeAsync(
        string cle, int version, CancellationToken ct = default)
    {
        var row = await Cn.QuerySingleOrDefaultAsync($"""
            SELECT * FROM {T("DEFINITION_PROCESSUS")}
            WHERE CLE = @Cle AND VERSION = @Version AND STATUT = 'Publiee'
            """, new { Cle = cle, Version = version });

        return row is null ? null : MapperDefinition(row);
    }

    public async Task<DefinitionProcessus?> ObtenirDerniereVersionPublieeAsync(
        string cle, CancellationToken ct = default)
    {
        var row = await Cn.QuerySingleOrDefaultAsync($"""
            SELECT * FROM {T("DEFINITION_PROCESSUS")}
            WHERE CLE = @Cle AND STATUT = 'Publiee'
            ORDER BY VERSION DESC LIMIT 1
            """, new { Cle = cle });

        return row is null ? null : MapperDefinition(row);
    }

    public async Task PublierAsync(string cle, CancellationToken ct = default)
    {
        var maintenant = DateTime.UtcNow.ToString("O");
        await Cn.ExecuteAsync($"""
            UPDATE {T("DEFINITION_PROCESSUS")}
            SET STATUT = 'Publiee', DATE_PUBLICATION = @DatePublication
            WHERE CLE = @Cle AND STATUT = 'Brouillon'
            """, new { Cle = cle, DatePublication = maintenant });
    }

    public async Task<IReadOnlyList<DefinitionProcessus>> ObtenirToutesAsync(CancellationToken ct = default)
    {
        var rows = await Cn.QueryAsync($"""
            SELECT * FROM {T("DEFINITION_PROCESSUS")} ORDER BY CLE, VERSION
            """);

        return rows.Select(r => MapperDefinition(r)).ToList();
    }

    private static DefinitionProcessus MapperDefinition(dynamic row)
    {
        var def = JsonDefinitionParser.Deserialiser((string)row.DEFINITION_JSON);
        def.Id = (long)row.ID;
        def.Version = (int)row.VERSION;
        def.Statut = row.STATUT == "Publiee" ? StatutDefinition.Publiee : StatutDefinition.Brouillon;
        def.DateCreation = DateTime.Parse((string)row.DATE_CREATION);
        def.DatePublication = row.DATE_PUBLICATION is not null
            ? DateTime.Parse((string)row.DATE_PUBLICATION)
            : null;
        return def;
    }
}
