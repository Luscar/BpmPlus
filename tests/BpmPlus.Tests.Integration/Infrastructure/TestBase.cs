using Autofac;
using BpmPlus.Abstractions;
using BpmPlus.Core.Definition;
using BpmPlus.Persistance.Sqlite;
using BpmPlus.Registration;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Data;
using Xunit;

namespace BpmPlus.Tests.Integration.Infrastructure;

/// <summary>
/// Classe de base pour les tests d'intégration BpmPlus.
/// Utilise SQLite en mémoire avec cache partagé pour l'isolation par test.
/// </summary>
public abstract class TestBase : IAsyncLifetime
{
    private readonly string _dbName = $"testdb_{Guid.NewGuid():N}";
    private SqliteConnection? _ownerConnection;

    protected IContainer Container { get; private set; } = null!;

    protected SqliteConnection AbrirConnexion()
    {
        var conn = new SqliteConnection($"Data Source={_dbName};Mode=Memory;Cache=Shared");
        conn.Open();
        return conn;
    }

    public virtual async Task InitializeAsync()
    {
        _ownerConnection = AbrirConnexion();

        var builder = new ContainerBuilder();

        builder.RegisterModule(new BpmModule(config =>
        {
            config.ScanHandlers(typeof(TestBase).Assembly);
            config.UseGestionTache<GestionTacheEnregistreur>();
            config.UseSqlite("BPM");
        }));

        builder.RegisterGeneric(typeof(NullLogger<>))
            .As(typeof(ILogger<>))
            .SingleInstance();

        Container = builder.Build();

        using var conn = AbrirConnexion();
        using var scope = Container.BeginLifetimeScope(b =>
            b.RegisterInstance(conn).As<IDbConnection>().ExternallyOwned());
        await scope.Resolve<SchemaCreator>().CreerToutesLesTablesAsync(conn);
    }

    public virtual Task DisposeAsync()
    {
        _ownerConnection?.Dispose();
        Container?.Dispose();
        return Task.CompletedTask;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    protected async Task PublierDefinitionAsync(DefinitionProcessus definition)
    {
        using var conn = AbrirConnexion();
        using var tx = conn.BeginTransaction();
        using var scope = Container.BeginLifetimeScope(b =>
            b.RegisterInstance(conn).As<IDbConnection>().ExternallyOwned());

        var sf = scope.Resolve<IServiceFlux>();
        await sf.SauvegarderDefinitionAsync(definition);
        await sf.PublierDefinitionAsync(definition.Cle);
        tx.Commit();
    }

    protected async Task<long> DemarrerAsync(
        string cleDefinition,
        long aggregateId,
        Dictionary<string, object?>? variables = null)
    {
        using var conn = AbrirConnexion();
        using var tx = conn.BeginTransaction();
        using var scope = Container.BeginLifetimeScope(b =>
            b.RegisterInstance(conn).As<IDbConnection>().ExternallyOwned());

        var idInstance = await scope.Resolve<IServiceFlux>()
            .DemarrerAsync(cleDefinition, aggregateId, variables);
        tx.Commit();
        return idInstance;
    }

    protected async Task<InstanceProcessus> ObtenirAsync(long idInstance)
    {
        using var conn = AbrirConnexion();
        using var scope = Container.BeginLifetimeScope(b =>
            b.RegisterInstance(conn).As<IDbConnection>().ExternallyOwned());
        return await scope.Resolve<IServiceFlux>().ObtenirAsync(idInstance);
    }

    protected async Task ModifierVariableEtTerminerEtapeAsync(
        long idInstance,
        string? nomVariable = null,
        object? valeur = null)
    {
        using var conn = AbrirConnexion();
        using var tx = conn.BeginTransaction();
        using var scope = Container.BeginLifetimeScope(b =>
            b.RegisterInstance(conn).As<IDbConnection>().ExternallyOwned());

        var sf = scope.Resolve<IServiceFlux>();
        if (nomVariable is not null)
            await sf.ModifierVariableAsync(idInstance, nomVariable, valeur);
        await sf.TerminerEtapeAsync(idInstance);
        tx.Commit();
    }

    protected async Task TerminerEtapeAsync(long idInstance)
        => await ModifierVariableEtTerminerEtapeAsync(idInstance);

    protected async Task EnvoyerSignalAsync(string nomSignal, long? idInstance = null)
    {
        using var conn = AbrirConnexion();
        using var tx = conn.BeginTransaction();
        using var scope = Container.BeginLifetimeScope(b =>
            b.RegisterInstance(conn).As<IDbConnection>().ExternallyOwned());

        await scope.Resolve<IServiceFlux>().EnvoyerSignalAsync(nomSignal, idInstance);
        tx.Commit();
    }

    protected async Task ReprendreAttenteTempsAsync(long idInstance)
    {
        using var conn = AbrirConnexion();
        using var tx = conn.BeginTransaction();
        using var scope = Container.BeginLifetimeScope(b =>
            b.RegisterInstance(conn).As<IDbConnection>().ExternallyOwned());

        await scope.Resolve<IServiceFlux>().ReprendreAttenteTempsAsync(idInstance);
        tx.Commit();
    }

    protected async Task<IReadOnlyList<EvenementInstance>> ObtenirHistoriqueAsync(long idInstance)
    {
        using var conn = AbrirConnexion();
        using var scope = Container.BeginLifetimeScope(b =>
            b.RegisterInstance(conn).As<IDbConnection>().ExternallyOwned());
        return await scope.Resolve<IServiceFlux>().ObtenirHistoriqueAsync(idInstance);
    }

    protected async Task<IReadOnlyList<InstanceProcessus>> RechercherParVariableAsync(
        string nomVariable, object valeur)
    {
        using var conn = AbrirConnexion();
        using var scope = Container.BeginLifetimeScope(b =>
            b.RegisterInstance(conn).As<IDbConnection>().ExternallyOwned());
        return await scope.Resolve<IServiceFlux>()
            .RechercherParVariableAsync(nomVariable, valeur);
    }

    protected async Task<IReadOnlyList<string>> ObtenirSignauxEnAttenteAsync(long idInstance)
    {
        using var conn = AbrirConnexion();
        using var scope = Container.BeginLifetimeScope(b =>
            b.RegisterInstance(conn).As<IDbConnection>().ExternallyOwned());
        return await scope.Resolve<IServiceFlux>().ObtenirSignauxEnAttenteAsync(idInstance);
    }

    protected async Task<IReadOnlyList<InstanceEchue>> ObtenirInstancesEchuesAsync(DateTime dateReference)
    {
        using var conn = AbrirConnexion();
        using var scope = Container.BeginLifetimeScope(b =>
            b.RegisterInstance(conn).As<IDbConnection>().ExternallyOwned());
        return await scope.Resolve<IServiceFlux>().ObtenirInstancesEchuesAsync(dateReference);
    }

    protected async Task<IReadOnlyList<InstanceProcessus>> ObtenirEnfantsAsync(long idInstanceParent)
    {
        using var conn = AbrirConnexion();
        using var scope = Container.BeginLifetimeScope(b =>
            b.RegisterInstance(conn).As<IDbConnection>().ExternallyOwned());
        return await scope.Resolve<IServiceFlux>().ObtenirEnfantsAsync(idInstanceParent);
    }

    protected static void AssertEvenementPresent(
        IReadOnlyList<EvenementInstance> historique,
        TypeEvenement type,
        string? idNoeud = null)
    {
        var found = historique.Any(e =>
            e.TypeEvenement == type &&
            (idNoeud is null || e.IdNoeud == idNoeud));

        var detail = idNoeud is null ? type.ToString() : $"{type} sur '{idNoeud}'";
        Assert.True(found,
            $"Événement attendu '{detail}' absent de l'historique. " +
            $"Historique : {string.Join(", ", historique.Select(e => $"{e.TypeEvenement}({e.IdNoeud})"))}");
    }
}
