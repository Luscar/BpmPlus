using Autofac;
using BpmPlus.Abstractions;
using BpmPlus.Core.Definition;
using BpmPlus.ExempleClient;
using BpmPlus.Persistance.Sqlite;
using BpmPlus.Registration;
using Microsoft.Data.Sqlite;
using System.Data;

Console.OutputEncoding = System.Text.Encoding.UTF8;

const string BaseDeDonnees = "bpm_exemple.db";

AfficherBanniere("BpmPlus — Exemple client : Processus d'approbation de commande");

// ── Étape 1 : Conteneur Autofac ───────────────────────────────────────────────
//
// BpmModule découvre automatiquement tous les IBpmHandlerCommande et
// IBpmHandlerQuery<> présents dans l'assembly, et enregistre IGestionTache.
// La connexion IDbConnection est fournie par l'application à chaque scope.

AfficherSection("1/4", "Configuration du conteneur Autofac");

var containerBuilder = new ContainerBuilder();

containerBuilder.RegisterModule(new BpmModule(config =>
{
    config.ScanHandlers(typeof(Program).Assembly);
    config.UseGestionTache<GestionTache>();
    config.UseSqlite("BPM");
}));

IContainer container = containerBuilder.Build();
Console.WriteLine("  Conteneur prêt.");
Console.WriteLine();

// ── Étape 2 : Schéma SQLite ───────────────────────────────────────────────────
//
// SchemaCreator crée toutes les tables BPM_ si elles n'existent pas.
// En production Oracle, les tables sont créées via des scripts DDL séparés.

AfficherSection("2/4", $"Création du schéma SQLite ({BaseDeDonnees})");

{
    using var conn = OuvrirConnexion(BaseDeDonnees);
    using var scope = container.BeginLifetimeScope(b =>
        b.RegisterInstance(conn).As<IDbConnection>().ExternallyOwned());

    await scope.Resolve<SchemaCreator>().CreerToutesLesTablesAsync(conn);
}

Console.WriteLine("  Tables créées.");
Console.WriteLine();

// ── Étape 3 : Définition et publication du processus ──────────────────────────
//
// Processus d'approbation de commande :
//
//   valider-commande (NoeudMetier)
//       → approbation-responsable (NoeudInteractif, CommandePost: EnregistrerDecision)
//           → decision-approbation (NoeudDecision)
//               [statut == "Approuvee"] → notification-approbation (NoeudMetier final)
//               [par défaut]            → notification-refus        (NoeudMetier final)

AfficherSection("3/4", "Publication de la définition du processus");

var definition = new ProcessusBuilder(
        "approbation-commande",
        "Processus d'approbation de commande",
        "valider-commande")

    // Valide la commande et initialise la variable "statut" à "EnAttente"
    .Metier("valider-commande", "Valider la commande", "approbation-responsable")

    // Suspension : crée une tâche pour le responsable via IGestionTache
    // La CommandePost est exécutée dans la même transaction que la reprise
    .Interactif("approbation-responsable", "Approbation responsable", n => n
        .Tache("Approuver la commande", "Veuillez approuver ou refuser la commande")
        .CommandePost("EnregistrerDecisionCommand")
        .Vers("decision-approbation"))

    // Branchement XOR via query (EstCommandeApprouveeHandler lit la variable "statut")
    .Decision("decision-approbation", "Décision d'approbation", n => n
        .SiQuery("EstCommandeApprouveeQuery").Vers("notification-approbation")
        .Defaut().Vers("notification-refus"))

    // Nœuds finaux (EstFinale implicite car vers: omis)
    .Metier("notification-approbation", "Notifier approbation")
    .Metier("notification-refus", "Notifier refus")

    .Build();

{
    using var conn = OuvrirConnexion(BaseDeDonnees);
    using var tx   = conn.BeginTransaction();
    using var scope = container.BeginLifetimeScope(b =>
        b.RegisterInstance(conn).As<IDbConnection>().ExternallyOwned());

    var serviceFlux = scope.Resolve<IServiceFlux>();
    await serviceFlux.SauvegarderDefinitionAsync(definition);
    await serviceFlux.PublierDefinitionAsync("approbation-commande");
    tx.Commit();
}

Console.WriteLine("  Définition « approbation-commande » v1 publiée.");
Console.WriteLine();

// ── Étape 4 : Scénarios ───────────────────────────────────────────────────────

AfficherSection("4/4", "Exécution des scénarios");
Console.WriteLine();

await ExecuterScenario(container, BaseDeDonnees,
    titre:      "Commande approuvée",
    commandeId: 101L,
    montant:    500.00m,
    decision:   "Approuvee");

Console.WriteLine();

await ExecuterScenario(container, BaseDeDonnees,
    titre:      "Commande refusée",
    commandeId: 102L,
    montant:    15_000.00m,
    decision:   "Refusee");

Console.WriteLine();
AfficherBanniere("Exemple terminé — inspectez bpm_exemple.db pour les données persistées");

// ── Fonctions ─────────────────────────────────────────────────────────────────

static SqliteConnection OuvrirConnexion(string db)
{
    var conn = new SqliteConnection($"Data Source={db}");
    conn.Open();
    return conn;
}

static void AfficherBanniere(string message)
{
    var ligne = new string('=', message.Length + 4);
    Console.WriteLine(ligne);
    Console.WriteLine($"  {message}");
    Console.WriteLine(ligne);
    Console.WriteLine();
}

static void AfficherSection(string etape, string titre)
    => Console.WriteLine($"[Étape {etape}] {titre}");

static async Task ExecuterScenario(
    IContainer container,
    string      db,
    string      titre,
    long        commandeId,
    decimal     montant,
    string      decision)
{
    Console.WriteLine($"  +-- Scénario : {titre}");
    Console.WriteLine($"  |   Commande #{commandeId}  |  Montant : {montant:C}  |  Décision : {decision}");
    Console.WriteLine("  |");

    // 1. Démarrer le processus pour cet agrégat (ici, l'ID de la commande)
    long idInstance;
    {
        using var conn  = OuvrirConnexion(db);
        using var tx    = conn.BeginTransaction();
        using var scope = container.BeginLifetimeScope(b =>
            b.RegisterInstance(conn).As<IDbConnection>().ExternallyOwned());

        idInstance = await scope.Resolve<IServiceFlux>().DemarrerAsync(
            "approbation-commande",
            commandeId,
            new Dictionary<string, object?> { ["montant"] = montant });

        tx.Commit();
    }

    Console.WriteLine($"  |   > Instance démarrée  : id = {idInstance}");

    // 2. Consulter l'état — l'instance est suspendue sur le noeud interactif
    {
        using var conn  = OuvrirConnexion(db);
        using var scope = container.BeginLifetimeScope(b =>
            b.RegisterInstance(conn).As<IDbConnection>().ExternallyOwned());

        var instance = await scope.Resolve<IServiceFlux>().ObtenirAsync(idInstance);
        Console.WriteLine($"  |   > Statut             : {instance.Statut}");
        Console.WriteLine($"  |   > Noeud courant      : {instance.IdNoeudCourant}");
    }

    // 3. Simuler la décision du responsable :
    //    - écrire "statut" dans les variables du processus
    //    - appeler TerminerEtapeAsync (exécute la CommandePost EnregistrerDecisionCommand,
    //      ferme la tâche via IGestionTache, puis reprend l'exécution)
    {
        using var conn  = OuvrirConnexion(db);
        using var tx    = conn.BeginTransaction();
        using var scope = container.BeginLifetimeScope(b =>
            b.RegisterInstance(conn).As<IDbConnection>().ExternallyOwned());

        var serviceFlux = scope.Resolve<IServiceFlux>();
        await serviceFlux.ModifierVariableAsync(idInstance, "statut", decision);
        await serviceFlux.TerminerEtapeAsync(idInstance);
        tx.Commit();
    }

    Console.WriteLine($"  |   > Décision enregistrée : statut = {decision}");

    // 4. Lire le statut final et l'historique complet
    {
        using var conn  = OuvrirConnexion(db);
        using var scope = container.BeginLifetimeScope(b =>
            b.RegisterInstance(conn).As<IDbConnection>().ExternallyOwned());

        var sf             = scope.Resolve<IServiceFlux>();
        var instanceFinale = await sf.ObtenirAsync(idInstance);
        var historique     = await sf.ObtenirHistoriqueAsync(idInstance);

        Console.WriteLine($"  |   > Statut final       : {instanceFinale.Statut}");
        Console.WriteLine("  |");
        Console.WriteLine("  |   Historique :");
        Console.WriteLine("  |   +-----------------------+----------------------+------------------------+");
        Console.WriteLine("  |   | Heure         | Événement            | Noeud                  |");
        Console.WriteLine("  |   +-----------------------+----------------------+------------------------+");

        foreach (var ev in historique)
        {
            var heure = ev.Horodatage.ToString("HH:mm:ss.fff");
            var type  = ev.TypeEvenement.ToString().PadRight(20);
            var noeud = (ev.NomNoeud ?? "-").PadRight(22);
            Console.WriteLine($"  |   | {heure}     | {type} | {noeud} |");
        }

        Console.WriteLine("  |   +-----------------------+----------------------+------------------------+");
    }

    Console.WriteLine($"  +-- Scénario « {titre} » terminé.");
}
