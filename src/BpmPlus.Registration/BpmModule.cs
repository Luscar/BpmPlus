using System.Reflection;
using Autofac;
using BpmPlus.Abstractions;
using BpmPlus.Core.Execution;
using BpmPlus.Core.Execution.Executeurs;
using BpmPlus.Core.Persistance;
using BpmPlus.Core.Services;
using BpmPlus.Persistance.Oracle.Repositories;
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
        builder.RegisterType<ServiceFlux>()
            .As<IServiceFlux>()
            .InstancePerLifetimeScope();

        builder.RegisterType<ServiceMigration>()
            .As<IServiceMigration>()
            .InstancePerLifetimeScope();
    }

    private void RegisterPersistance(ContainerBuilder builder)
    {
        var prefixe = _config.Prefixe;

        switch (_config.Backend)
        {
            case BackendPersistance.Sqlite:
                builder.Register(_ => new RepositoryDefinitionSqlite(prefixe))
                    .As<IRepositoryDefinition>().SingleInstance();
                builder.Register(_ => new RepositoryInstanceSqlite(prefixe))
                    .As<IRepositoryInstance>().SingleInstance();
                builder.Register(_ => new RepositoryVariableSqlite(prefixe))
                    .As<IRepositoryVariable>().SingleInstance();
                builder.Register(_ => new RepositoryEvenementSqlite(prefixe))
                    .As<IRepositoryEvenement>().SingleInstance();
                builder.Register(_ => new RepositoryAttenteSignalSqlite(prefixe))
                    .As<IRepositoryAttenteSignal>().SingleInstance();
                break;

            case BackendPersistance.Oracle:
                builder.Register(_ => new RepositoryDefinitionOracle(prefixe))
                    .As<IRepositoryDefinition>().SingleInstance();
                builder.Register(_ => new RepositoryInstanceOracle(prefixe))
                    .As<IRepositoryInstance>().SingleInstance();
                builder.Register(_ => new RepositoryVariableOracle(prefixe))
                    .As<IRepositoryVariable>().SingleInstance();
                builder.Register(_ => new RepositoryEvenementOracle(prefixe))
                    .As<IRepositoryEvenement>().SingleInstance();
                builder.Register(_ => new RepositoryAttenteSignalOracle(prefixe))
                    .As<IRepositoryAttenteSignal>().SingleInstance();
                break;
        }
    }

    private void RegisterHandlers(ContainerBuilder builder)
    {
        foreach (var assembly in _config.AssembliesHandlers)
        {
            // Découverte des IHandlerCommande
            var commandeTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract
                            && typeof(IHandlerCommande).IsAssignableFrom(t));

            foreach (var type in commandeTypes)
            {
                // Instancier temporairement pour lire NomCommande
                // Alternative : utiliser un attribut ou une convention de nommage
                builder.RegisterType(type)
                    .As<IHandlerCommande>()
                    .Keyed<IHandlerCommande>(GetNomCommande(type))
                    .InstancePerLifetimeScope();
            }

            // Découverte des IHandlerQuery<T>
            var queryTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract
                            && t.GetInterfaces().Any(i =>
                                i.IsGenericType &&
                                i.GetGenericTypeDefinition() == typeof(IHandlerQuery<>)));

            foreach (var type in queryTypes)
            {
                var nomQuery = GetNomQuery(type);
                if (nomQuery is null) continue;

                // Enregistrer comme IHandlerQuery (base non-générique) pour la résolution par nom
                builder.RegisterType(type)
                    .As<IHandlerQuery>()
                    .Keyed<IHandlerQuery>(nomQuery)
                    .InstancePerLifetimeScope();

                // Enregistrer également pour chaque interface IHandlerQuery<T> implémentée
                var queryInterfaces = type.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHandlerQuery<>));

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
            // Enregistrer une implémentation nulle si non configurée
            builder.RegisterType<GestionTacheNulle>()
                .As<IGestionTache>()
                .InstancePerLifetimeScope();
        }
    }

    private static string? GetNomCommande(Type type)
    {
        // Instancier pour lire NomCommande — nécessite un constructeur sans paramètre
        // Alternative recommandée : attribut [NomCommande("...")] ou convention de nommage
        try
        {
            var prop = type.GetProperty("NomCommande");
            if (prop is null) return null;

            // Chercher via méthode statique ou attribut en priorité
            var instance = Activator.CreateInstance(type);
            return instance is null ? null : (string?)prop.GetValue(instance);
        }
        catch
        {
            return type.Name; // Fallback sur le nom de classe
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
    public Task<string> CreerTacheAsync(
        DefinitionTache definitionTache, InstanceProcessus instance,
        CancellationToken ct = default)
        => Task.FromResult(Guid.NewGuid().ToString());

    public Task FermerTacheAsync(string idTacheExterne, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task AssignerTacheAsync(string idTacheExterne, string assignee, CancellationToken ct = default)
        => Task.CompletedTask;
}
