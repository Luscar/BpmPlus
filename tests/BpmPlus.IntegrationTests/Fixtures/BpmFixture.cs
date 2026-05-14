using System.Data;
using Autofac;
using BpmPlus.Abstractions;
using BpmPlus.Core.Definition;
using BpmPlus.Core.Persistance;
using BpmPlus.IntegrationTests.Handlers;
using BpmPlus.Persistance.Sqlite;
using BpmPlus.Registration;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BpmPlus.IntegrationTests.Fixtures;

/// <summary>
/// Fixture d'intégration : SQLite en mémoire + conteneur Autofac complet.
/// Chaque instance crée une base fraîche, isolant les tests les uns des autres.
/// </summary>
public class BpmFixture : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IContainer _container;
    private readonly ILifetimeScope _scope;

    public IServiceBpm ServiceBpm => _scope.Resolve<IServiceBpm>();
    public IRepositoryDefinition RepoDefinition => _scope.Resolve<IRepositoryDefinition>();
    public IRepositoryInstance RepoInstance => _scope.Resolve<IRepositoryInstance>();
    public IRepositoryVariable RepoVariable => _scope.Resolve<IRepositoryVariable>();
    public IRepositoryEvenement RepoEvenement => _scope.Resolve<IRepositoryEvenement>();
    public IRepositoryAttenteSignal RepoSignal => _scope.Resolve<IRepositoryAttenteSignal>();

    public BpmFixture()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var builder = new ContainerBuilder();

        builder.RegisterInstance(_connection).As<IDbConnection>().ExternallyOwned();

        builder.RegisterInstance(NullLoggerFactory.Instance).As<ILoggerFactory>();
        builder.RegisterGeneric(typeof(Logger<>)).As(typeof(ILogger<>));

        builder.RegisterModule(new BpmModule(cfg => cfg
            .UseSqlite("BPM")
            .ScanHandlers(typeof(NoOpCommand).Assembly)));

        _container = builder.Build();
        _scope = _container.BeginLifetimeScope();

        var schema = _scope.Resolve<SchemaCreator>();
        schema.CreerToutesLesTablesAsync(_connection).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Crée et publie une définition de processus dans la base de test.
    /// Retourne la définition avec son identifiant et version assignés.
    /// </summary>
    public async Task<DefinitionProcessus> PublierDefinitionAsync(DefinitionProcessus definition)
    {
        await ServiceBpm.SauvegarderDefinitionAsync(definition);
        await ServiceBpm.PublierDefinitionAsync(definition.Cle);
        return (await RepoDefinition.ObtenirDerniereVersionPublieeAsync(definition.Cle))!;
    }

    /// <summary>
    /// Raccourci : construit un processus linéaire minimal et le publie.
    /// Nœuds : start (NoOpCommand) → fin (NoOpCommand, final).
    /// </summary>
    public Task<DefinitionProcessus> PublierProcessusSimpleAsync(string cle = "processus-test")
    {
        var def = DefinitionBuilder.Definir(cle)
            .Intitule("Processus de test")
            .Commence("start")
            .Metier("start", "Démarrage", b => b.Commande("NoOpCommand").Vers("fin"))
            .Metier("fin", "Fin", b => b.Commande("NoOpCommand"))
            .Build();
        return PublierDefinitionAsync(def);
    }

    public void Dispose()
    {
        _scope.Dispose();
        _container.Dispose();
        _connection.Dispose();
    }
}
