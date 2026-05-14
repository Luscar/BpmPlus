using System.Data;
using System.Reflection;
using Autofac;
using BpmPlus.Abstractions;
using BpmPlus.Core.Execution;
using BpmPlus.Core.Execution.Executeurs;
using BpmPlus.Core.Persistance;
using BpmPlus.Core.Services;
using BpmPlus.Persistance.Oracle.Repositories;
using BpmPlus.Persistance.Sqlite;
using BpmPlus.Persistance.Sqlite.Repositories;

namespace BpmPlus.Registration;

/// <summary>
/// Module Autofac qui enregistre tous les composants du moteur BPM.
/// </summary>
public class BpmModule : Autofac.Module
{
    private readonly BpmConfiguration _config;

    public BpmModule(Action<BpmConfiguration> configure)
    {
        _config = new BpmConfiguration();
        configure(_config);
    }

    protected override void Load(ContainerBuilder builder)
    {
        // ── Persistance ───────────────────────────────────────────────────────
        RegisterPersistance(builder);

        // ── Handlers découverts dynamiquement ─────────────────────────────────
        RegisterHandlers(builder);

        // ── Gestion de tâches (optionnelle) ────────────────────────────────────
        RegisterGestionTache(builder);

        // ── Résolveur de paramètres ────────────────────────────────────────────
        builder.RegisterType<ResolveurParametre>()
            .AsSelf()
            .InstancePerLifetimeScope();

        // ── Exécuteurs de nœuds ──────────────────────────────────────────────
        builder.RegisterType<ExecuteurNoeudMetier>()
            .AsSelf()
            .InstancePerLifetimeScope();

        builder.RegisterType<ExecuteurNoeudInteractif>()
            .AsSelf()
            .InstancePerLifetimeScope();

        builder.RegisterType<ExecuteurNoeudDecision>()
            .AsSelf()
            .InstancePerLifetimeScope();

        builder.RegisterType<ExecuteurNoeudAttenteTemps>()
            .AsSelf()
            .InstancePerLifetimeScope();

        builder.RegisterType<ExecuteurNoeudAttenteSignal>()
            .AsSelf()
            .InstancePerLifetimeScope();

        // ── Moteur d'exécution ────────────────────────────────────────────────
        // La dépendance circulaire entre MoteurExecution et ExecuteurNoeudSousProcessus
        // est cassée via Lazy<T> (Autofac résout Lazy<T> automatiquement).
        builder.RegisterType<MoteurExecution>()
            .AsSelf()
            .InstancePerLifetimeScope();

        // ExecuteurNoeudSousProcessus reçoit Func<MoteurExecution> pour éviter la circularité
        builder.Register(ctx =>
        {
            var scope = ctx.Resolve<ILifetimeScope>();
            return new ExecuteurNoeudSousProcessus(
                ctx.Resolve<IRepositoryDefinition>(),
                ctx.Resolve<IRepositoryInstance>(),
                ctx.Resolve<IRepositoryVariable>(),
                () => scope.Resolve<MoteurExecution>(),
                ctx.Resolve<Microsoft.Extensions.Logging.ILogger<ExecuteurNoeudSousProcessus>>());
        })
        .AsSelf()
        .InstancePerLifetimeScope();

        // ── Services BPM ──────────────────────────────────────────────────────
        builder.RegisterType<ServiceBpm>()
            .As<IServiceBpm>()
            .InstancePerLifetimeScope();

        builder.RegisterType<ServiceMigration>()
            .As<IServiceMigration>()
            .InstancePerLifetimeScope();
    }

    private void RegisterPersistance(ContainerBuilder builder)
    {
        // Si le client enregistre une IDbConnectionFactory (au lieu d'une IDbConnection
        // déjà ouverte), on l'utilise pour ouvrir la connexion à la demande — c'est-à-dire
        // à l'intérieur du TransactionScope ambiant du client, ce qui garantit l'enrôlement.
        builder.Register(ctx => ctx.Resolve<IDbConnectionFactory>().CreateOpenConnection())
            .As<IDbConnection>()
            .InstancePerLifetimeScope()
            .IfNotRegistered(typeof(IDbConnection));

        var prefixe = _config.Prefixe;

        switch (_config.Backend)
        {
            case BackendPersistance.Sqlite:
                builder.Register(ctx => new RepositoryDefinitionSqlite(ctx.Resolve<IDbConnection>(), prefixe, ctx.ResolveOptional<IDbTransaction>()))
                    .As<IRepositoryDefinition>().InstancePerLifetimeScope();
                builder.Register(ctx => new RepositoryInstanceSqlite(ctx.Resolve<IDbConnection>(), prefixe, ctx.ResolveOptional<IDbTransaction>()))
                    .As<IRepositoryInstance>().InstancePerLifetimeScope();
                builder.Register(ctx => new RepositoryVariableSqlite(ctx.Resolve<IDbConnection>(), prefixe, ctx.ResolveOptional<IDbTransaction>()))
                    .As<IRepositoryVariable>().InstancePerLifetimeScope();
                builder.Register(ctx => new RepositoryEvenementSqlite(ctx.Resolve<IDbConnection>(), prefixe, ctx.ResolveOptional<IDbTransaction>()))
                    .As<IRepositoryEvenement>().InstancePerLifetimeScope();
                builder.Register(ctx => new RepositoryAttenteSignalSqlite(ctx.Resolve<IDbConnection>(), prefixe, ctx.ResolveOptional<IDbTransaction>()))
                    .As<IRepositoryAttenteSignal>().InstancePerLifetimeScope();
                builder.RegisterType<SchemaCreator>()
                    .AsSelf().InstancePerLifetimeScope();
                break;

            case BackendPersistance.Oracle:
                builder.Register(ctx => new RepositoryDefinitionOracle(ctx.Resolve<Lazy<IDbConnection>>(), prefixe, ctx.ResolveOptional<IDbTransaction>()))
                    .As<IRepositoryDefinition>().InstancePerLifetimeScope();
                builder.Register(ctx => new RepositoryInstanceOracle(ctx.Resolve<Lazy<IDbConnection>>(), prefixe, ctx.ResolveOptional<IDbTransaction>()))
                    .As<IRepositoryInstance>().InstancePerLifetimeScope();
                builder.Register(ctx => new RepositoryVariableOracle(ctx.Resolve<Lazy<IDbConnection>>(), prefixe, ctx.ResolveOptional<IDbTransaction>()))
                    .As<IRepositoryVariable>().InstancePerLifetimeScope();
                builder.Register(ctx => new RepositoryEvenementOracle(ctx.Resolve<Lazy<IDbConnection>>(), prefixe, ctx.ResolveOptional<IDbTransaction>()))
                    .As<IRepositoryEvenement>().InstancePerLifetimeScope();
                builder.Register(ctx => new RepositoryAttenteSignalOracle(ctx.Resolve<Lazy<IDbConnection>>(), prefixe, ctx.ResolveOptional<IDbTransaction>()))
                    .As<IRepositoryAttenteSignal>().InstancePerLifetimeScope();
                break;
        }
    }

    private void RegisterHandlers(ContainerBuilder builder)
    {
        foreach (var assembly in _config.AssembliesHandlers)
        {
            // Découverte des IBpmHandlerCommande
            var commandeTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract
                            && typeof(IBpmHandlerCommande).IsAssignableFrom(t));

            foreach (var type in commandeTypes)
            {
                var nomCommande = GetNomCommande(type);
                if (nomCommande is null) continue;

                builder.RegisterType(type)
                    .As<IBpmHandlerCommande>()
                    .Keyed<IBpmHandlerCommande>(nomCommande)
                    .InstancePerLifetimeScope();
            }

            // Découverte des IBpmHandlerQuery<T>
            var queryTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract
                            && t.GetInterfaces().Any(i =>
                                i.IsGenericType &&
                                i.GetGenericTypeDefinition() == typeof(IBpmHandlerQuery<>)));

            foreach (var type in queryTypes)
            {
                var nomQuery = GetNomQuery(type);
                if (nomQuery is null) continue;

                builder.RegisterType(type)
                    .As<IBpmHandlerQuery>()
                    .Keyed<IBpmHandlerQuery>(nomQuery)
                    .InstancePerLifetimeScope();

                var queryInterfaces = type.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IBpmHandlerQuery<>));

                foreach (var iface in queryInterfaces)
                {
                    builder.RegisterType(type)
                        .As(iface)
                        .Keyed(nomQuery, iface)
                        .InstancePerLifetimeScope();
                }
            }
        }
    }

    private void RegisterGestionTache(ContainerBuilder builder)
    {
        if (_config.TypeGestionTache is not null)
        {
            builder.RegisterType(_config.TypeGestionTache)
                .As<IGestionTache>()
                .InstancePerLifetimeScope();
        }
        else
        {
            builder.RegisterType<GestionTacheNulle>()
                .As<IGestionTache>()
                .InstancePerLifetimeScope();
        }
    }

    private static string? GetNomCommande(Type type)
    {
        try
        {
            var prop = type.GetProperty("NomCommande");
            if (prop is null) return null;

            var instance = Activator.CreateInstance(type);
            return instance is null ? null : (string?)prop.GetValue(instance);
        }
        catch
        {
            return null;
        }
    }

    private static string? GetNomQuery(Type type)
    {
        try
        {
            var prop = type.GetProperty("NomQuery");
            if (prop is null) return null;

            var instance = Activator.CreateInstance(type);
            return instance is null ? null : (string?)prop.GetValue(instance);
        }
        catch
        {
            return type.Name;
        }
    }
}

/// <summary>
/// Implémentation nulle de IGestionTache pour les processus sans gestion de tâches.
/// </summary>
internal class GestionTacheNulle : IGestionTache
{
    public Task CreerTacheAsync(
        DefinitionTache definitionTache, InstanceProcessus instance,
        CancellationToken ct = default)
        => Task.CompletedTask;

    public Task FermerTacheAsync(
        InstanceProcessus instance,
        IReadOnlyDictionary<string, object?> variables,
        CancellationToken ct = default)
        => Task.CompletedTask;

    public Task AssignerTacheAsync(long idProcessus, string assignee, CancellationToken ct = default)
        => Task.CompletedTask;
}
