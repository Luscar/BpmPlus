# Guide d'utilisation — BpmPlus (application cliente)

> Version : 2.0 — Destiné aux développeurs intégrant BpmPlus dans une application .NET 8

---

## Table des matières

1. [Vue d'ensemble](#1-vue-densemble)
2. [Installation](#2-installation)
3. [Configuration (Autofac)](#3-configuration-autofac)
4. [Implémenter les handlers](#4-implémenter-les-handlers)
5. [Définir un processus — ProcessusV2Builder](#5-définir-un-processus--processusvbuilder)
   - [5.1 Structure de base](#51-structure-de-base)
   - [5.2 Organisation par phases](#52-organisation-par-phases)
   - [5.3 Nœud métier](#53-nœud-métier)
   - [5.4 Nœud interactif (tâche humaine)](#54-nœud-interactif--tâche-humaine)
   - [5.5 Nœud décision](#55-nœud-décision)
   - [5.6 Nœud attente de temps](#56-nœud-attente-de-temps)
   - [5.7 Nœud attente de signal](#57-nœud-attente-de-signal)
   - [5.8 Nœud sous-processus](#58-nœud-sous-processus)
   - [5.9 Patron d'approbation (PatternApprobation)](#59-patron-dapprobation--patternapprobation)
   - [5.10 Options de Build](#510-options-de-build)
   - [5.11 Définition complète commentée](#511-définition-complète-commentée)
   - [5.12 Approche JSON](#512-approche-json)
   - [5.13 Builder V1 — maintenu pour compatibilité](#513-builder-v1--maintenu-pour-compatibilité)
6. [Gérer les définitions](#6-gérer-les-définitions)
7. [Démarrer et suivre une instance](#7-démarrer-et-suivre-une-instance)
8. [Tâches interactives et affectation](#8-tâches-interactives-et-affectation)
9. [Signaux](#9-signaux)
10. [Attentes de temps](#10-attentes-de-temps)
11. [Variables de processus](#11-variables-de-processus)
12. [Historique et audit](#12-historique-et-audit)
13. [Migration de version](#13-migration-de-version)
14. [Gestion des erreurs](#14-gestion-des-erreurs)
15. [Export Mermaid](#15-export-mermaid)
16. [Validation stricte (BuildStrict)](#16-validation-stricte--buildstrict)
17. [Référence des types de nœuds](#17-référence-des-types-de-nœuds)
18. [Guide de migration V1 → V2](#18-guide-de-migration-v1--v2)

---

## 1. Vue d'ensemble

BpmPlus est un moteur BPM distribué sous forme de NuGet .NET 8. Il s'intègre dans une application existante et orchestre des processus métier structurés attachés à des agrégats du domaine.

**Principes clés :**

- Le moteur est **agnostique au domaine** : toute la logique métier reste dans votre application via des handlers.
- La **transaction est fournie par votre application** : BpmPlus ne crée jamais de connexion.
- Les nœuds enchaînés s'exécutent **en mémoire** dans une même transaction, sans aller-retour base de données.
- Les instances ne sont persistées qu'aux **points de suspension** (nœud interactif, attente signal, attente temps) et à la fin.

**Bases de données supportées :**

| Environnement | Usage               |
|---------------|---------------------|
| Oracle        | Production          |
| SQLite        | Tests d'intégration |

---

## 2. Installation

Ajoutez les packages NuGet selon votre base de données :

```bash
# Moteur + abstractions (toujours requis)
dotnet add package BpmPlus.Abstractions
dotnet add package BpmPlus.Core
dotnet add package BpmPlus.Registration

# Pour Oracle (production)
dotnet add package BpmPlus.Persistance.Oracle

# Pour SQLite (tests)
dotnet add package BpmPlus.Persistance.Sqlite
```

Les tables sont créées automatiquement au démarrage avec le préfixe configuré (ex : `BPM_INSTANCE_PROCESSUS`).

---

## 3. Configuration (Autofac)

Enregistrez le module `BpmModule` dans votre conteneur Autofac :

```csharp
var builder = new ContainerBuilder();

builder.RegisterModule(new BpmModule(config =>
{
    // Découverte automatique de tous les IBpmHandlerCommande et IBpmHandlerQuery<>
    config.ScanHandlers(Assembly.GetExecutingAssembly());

    // Gestionnaire de tâches humaines (requis si vous utilisez des nœuds interactifs)
    config.UseGestionTache<MaGestionTache>();

    // Persistance Oracle avec préfixe de tables
    config.UseOracle("BPM");

    // OU persistance SQLite (tests)
    // config.UseSqlite("BPM");
}));

var container = builder.Build();
```

Le module enregistre automatiquement :
- `IServiceBpm` — service principal (scoped)
- `IServiceMigration` — migration de versions (scoped)
- Tous les handlers trouvés via `ScanHandlers`

### 3.1 Fournir `IDbConnection` par unité de travail

`BpmModule` **n'enregistre pas `IDbConnection`**. Votre application est responsable de fournir une instance `IDbConnection` dans chaque lifetime scope Autofac avant d'utiliser les services BPM.

Pattern recommandé :

```csharp
using var connexion = _connectionFactory.Creer();
using var transaction = connexion.BeginTransaction();

// Créer un sous-scope qui fournit IDbConnection au moteur et à tous les handlers
using var scope = _container.BeginLifetimeScope(b =>
    b.RegisterInstance(connexion)
     .As<IDbConnection>()
     .ExternallyOwned());  // BpmPlus ne dispose pas la connexion

var serviceBpm = scope.Resolve<IServiceBpm>();

// Appels au service — aucun paramètre de transaction requis
await serviceBpm.DemarrerAsync("approbation-commande", aggregateId, variables);

transaction.Commit();
```

> Dans une application web, ce pattern est généralement encapsulé dans un middleware ou une factory qui crée le scope pour chaque requête HTTP.

### 3.2 Démarrage complet — de l'installation au premier processus

#### Étape 1 — Enregistrer le module

```csharp
var builder = new ContainerBuilder();

builder.RegisterModule(new BpmModule(config =>
{
    config.ScanHandlers(Assembly.GetExecutingAssembly());
    config.UseSqlite("BPM");   // tests / développement
    // config.UseOracle("BPM"); // production
}));

_container = builder.Build();
```

#### Étape 2 — Créer les tables (SQLite uniquement)

En production Oracle, les tables sont créées via des scripts DDL fournis séparément. Pour SQLite, utilisez le `SchemaCreator` enregistré automatiquement par `UseSqlite` :

```csharp
using var connexion = new SqliteConnection("Data Source=bpm.db");
connexion.Open();

using var scope = _container.BeginLifetimeScope(b =>
    b.RegisterInstance(connexion).As<IDbConnection>().ExternallyOwned());

await scope.Resolve<SchemaCreator>().CreerToutesLesTablesAsync(connexion);
```

#### Étape 3 — Définir et publier un processus

Utilisez `ProcessusV2.Definir(…)` pour construire la définition, puis publiez-la :

```csharp
// Définition du processus avec le builder V2 (voir §5 pour tous les détails)
var definition = ProcessusV2
    .Definir("approbation-commande")
    .Intitule("Processus d'approbation de commande")
    .Commence("valider-commande")
    .Metier("valider-commande", "Valider la commande", "notifier")
    .Metier("notifier", "Notifier le résultat")
    .Build();

using var connexion = _connectionFactory.Creer();
using var transaction = connexion.BeginTransaction();

using var scope = _container.BeginLifetimeScope(b =>
    b.RegisterInstance(connexion).As<IDbConnection>().ExternallyOwned());

var serviceBpm = scope.Resolve<IServiceBpm>();
await serviceBpm.SauvegarderDefinitionAsync(definition);
await serviceBpm.PublierDefinitionAsync("approbation-commande");

transaction.Commit();
```

#### Étape 4 — Démarrer une instance

```csharp
using var connexion = _connectionFactory.Creer();
using var transaction = connexion.BeginTransaction();

using var scope = _container.BeginLifetimeScope(b =>
    b.RegisterInstance(connexion).As<IDbConnection>().ExternallyOwned());

var serviceBpm = scope.Resolve<IServiceBpm>();
long idInstance = await serviceBpm.DemarrerAsync(
    cleDefinition:      "approbation-commande",
    aggregateId:        42L,
    variablesInitiales: new Dictionary<string, object?> { ["montant"] = 1500m });

transaction.Commit();
```

---

## 4. Implémenter les handlers

### 4.1 Handler de commande (`IBpmHandlerCommande`)

Utilisé par les nœuds métier et les commandes `AuDemarrage`/`AuRetour` des nœuds interactifs.

```csharp
public class ValiderCommandeCommand : IBpmHandlerCommande
{
    // IDbConnection est injecté dans le même scope Autofac que le moteur.
    private readonly IDbConnection _connection;

    public ValiderCommandeCommand(IDbConnection connection) => _connection = connection;

    // Convention : NomCommande = PascalCase(id du nœud) + "Command"
    // Le nœud "valider-commande" résout automatiquement ce handler.
    public string NomCommande => "ValiderCommandeCommand";

    public async Task ExecuterAsync(
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte)
    {
        // aggregateId : ID de l'agrégat de l'instance (fourni automatiquement par le moteur)
        // parametres  : valeurs résolues depuis les variables du processus
        // contexte    : accès aux variables et à l'ID d'instance

        using var repo = new CommandeRepository(_connection);
        await repo.ValiderAsync(aggregateId!.Value);

        // Écrire une variable de processus si nécessaire
        contexte.Variables.Definir("statut", "Validee");
    }
}
```

> **Convention de nommage :** le moteur calcule `NomCommande` à partir de l'id du nœud :
> `PascalCase(id) + "Command"` — ex. `"valider-commande"` → `"ValiderCommandeCommand"`.
> Utilisez `.Commande("NomExplicite")` dans le builder pour déroger à cette convention.

### 4.2 Handler de query (`IBpmHandlerQuery<T>`)

Utilisé pour les conditions des nœuds décision (`SiQuery`) et les dates d'échéance des nœuds attente de temps (`EcheanceQuery`).

```csharp
// Condition booléenne (nœud décision)
public class EstCommandeApprouveeQuery : IBpmHandlerQuery<bool>
{
    private readonly IDbConnection _connection;

    public EstCommandeApprouveeQuery(IDbConnection connection) => _connection = connection;

    public string NomQuery => "EstCommandeApprouveeQuery";

    public async Task<bool> ExecuterAsync(
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte)
    {
        using var repo = new CommandeRepository(_connection);
        var statut = await repo.ObtenirStatutAsync(aggregateId!.Value);
        return statut == "Approuvee";
    }
}

// Date d'échéance dynamique (nœud attente de temps)
public class DateLivraisonPrevueQuery : IBpmHandlerQuery<DateTime>
{
    public string NomQuery => "DateLivraisonPrevueQuery";

    public async Task<DateTime> ExecuterAsync(
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte)
    {
        // Retourner la date de livraison attendue pour cet agrégat
        return DateTime.UtcNow.AddDays(14);
    }
}
```

### 4.3 Gestionnaire de tâches (`IGestionTache`)

Requis uniquement si votre processus contient des nœuds interactifs.

```csharp
public class MaGestionTache : IGestionTache
{
    public async Task<long> CreerTacheAsync(
        DefinitionTache definitionTache,
        InstanceProcessus instance,
        CancellationToken ct = default)
    {
        var idTache = await _tacheService.CreerAsync(new Tache
        {
            Titre            = definitionTache.Titre,
            Description      = definitionTache.Description,
            NomNoeud         = definitionTache.NomNoeud,        // renseigné automatiquement
            CodeRole         = definitionTache.CodeRole,
            CodeTache        = definitionTache.CodeTache,
            IndTacheRevision = definitionTache.IndTacheRevision,
            LogonAuteur      = definitionTache.LogonAuteur,
            AggregateId      = instance.AggregateId
        }, ct);

        return idTache;
    }

    public async Task FermerTacheAsync(
        long idTacheExterne,
        InstanceProcessus instance,
        IReadOnlyDictionary<string, object?> variables,
        CancellationToken ct = default)
    {
        var statut = variables.TryGetValue("statut", out var v) ? v?.ToString() : null;
        await _tacheService.FermerAsync(idTacheExterne, statut, ct);
    }

    public async Task AssignerTacheAsync(
        long idTacheExterne,
        string assignee,
        CancellationToken ct = default)
    {
        await _tacheService.AssignerAsync(idTacheExterne, assignee, ct);
    }
}
```

---

## 5. Définir un processus — ProcessusV2Builder

Le `ProcessusV2Builder` est le point d'entrée recommandé pour créer des définitions de processus. Il offre :

- **Métadonnées enrichies** sur le processus (description, auteur, étiquettes)
- **Groupement par phases** pour structurer visuellement les définitions complexes
- **DSL de conditions fluides** pour les nœuds décision
- **Sous-builder de tâche** regroupant toutes les propriétés en un seul bloc
- **Patron d'approbation** prêt à l'emploi
- **Validation stricte** et **export Mermaid** intégrés

> Le `ProcessusBuilder` (V1) reste disponible et fonctionnel. Voir [§5.13](#513-builder-v1--maintenu-pour-compatibilité) et [§18](#18-guide-de-migration-v1--v2).

---

### 5.1 Structure de base

```csharp
using BpmPlus.Core.Definition;

var definition = ProcessusV2
    .Definir("ma-cle-processus")            // clé unique (kebab-case recommandé)
    .Intitule("Mon processus")              // nom affiché dans l'interface
    .Description("Ce processus gère…")     // documentation embarquée
    .Auteur("Équipe Métier")               // propriétaire de la définition
    .Etiquettes("commandes", "finance")    // catégorisation libre
    .Commence("premier-noeud")             // id du nœud de départ

    // … déclaration des nœuds …

    .Build();                              // produit un DefinitionProcessus
```

**Règles de base :**

| Règle | Explication |
|-------|-------------|
| La clé est **obligatoire** | Identifiant unique de la définition |
| `.Commence(id)` est **obligatoire** | Le nœud de début doit exister dans la définition |
| Un nœud sans flux sortant ni `.Final()` est une **impasse** | Utilisez `.BuildStrict()` pour le détecter |
| Les nœuds sont évalués **dans l'ordre de déclaration** | L'ordre n'a pas d'effet sur le routage |

---

### 5.2 Organisation par phases

Les phases sont des **regroupements purement visuels** — elles n'ont aucun effet sur l'exécution. Elles sont indispensables dès qu'un processus dépasse une dizaine de nœuds.

```csharp
var definition = ProcessusV2
    .Definir("gestion-dossier")
    .Intitule("Gestion de dossier")
    .Commence("ouvrir-dossier")

    // ── Phase 1 ──────────────────────────────────────────────────────────────
    .Phase("Ouverture", phase => phase
        .Metier("ouvrir-dossier",   "Ouvrir le dossier",   "valider-pieces")
        .Metier("valider-pieces",   "Valider les pièces",  "decision-completude"))

    // ── Phase 2 ──────────────────────────────────────────────────────────────
    .Phase("Instruction", phase => phase
        .Decision("decision-completude", "Dossier complet ?", d => d
            .SiVariable("complet").EstEgalA(true).Aller("instruction")
            .Sinon.Aller("demande-complementaire"))
        .Interactif("instruction", "Instruction du dossier", n => n
            .TacheHumaine(t => t.Titre("Instruire le dossier").Role("INSTRUCTEUR"))
            .AuRetour("EnregistrerDecisionInstructionCommand")
            .Puis("cloturer-dossier"))
        .Metier("demande-complementaire", "Demander des pièces complémentaires"))

    // ── Phase 3 ──────────────────────────────────────────────────────────────
    .Phase("Clôture", phase => phase
        .Metier("cloturer-dossier", "Clôturer le dossier"))

    .BuildStrict();
```

> **Astuce :** les séparateurs de commentaires (`// ── Phase N ───`) reproduisent la lisibilité des phases directement dans les fichiers C# même sans sous-bloc `Phase(…)`.

---

### 5.3 Nœud métier

Un nœud métier exécute un `IBpmHandlerCommande`. Le nom de commande est déduit automatiquement de l'id du nœud : `PascalCase(id) + "Command"`.

#### Forme courte — nœud final

```csharp
// EstFinale = true implicite car aucun vers n'est fourni
.Metier("notification-refus", "Notifier le refus")
```

#### Forme courte — nœud séquentiel

```csharp
// 3e argument = id du nœud suivant
.Metier("valider-commande", "Valider la commande", "approbation-responsable")
```

#### Forme complète — paramètres et commande personnalisée

```csharp
.Metier("verifier-budget", "Vérifier le budget", b => b
    // Surcharge le NomCommande déduit automatiquement (optionnel)
    .Commande("VerifierBudgetDisponibleCommand")

    // Paramètre dont le nom = nom de la variable du processus (raccourci)
    .Param("montant")

    // Paramètre avec source explicite
    .Param("centreCoût", Src.Var("centreCoûtDemandeur"))

    // Paramètre avec valeur fixe
    .ParamFixe("devise", "EUR")

    // Nœud suivant (.Puis est un alias lisible de .Vers)
    .Puis("decision-budget"))
```

**Sources de paramètres disponibles (`Src`) :**

| Source | Description |
|--------|-------------|
| `Src.Var("nomVar")` | Valeur lue depuis une variable du processus |
| `Src.Val(valeur)` | Valeur statique fixée à la conception |
| `Src.Query("NomQuery")` | Valeur calculée par un `IBpmHandlerQuery<T>` à l'exécution |

---

### 5.4 Nœud interactif — tâche humaine

Un nœud interactif **suspend** l'instance, crée une tâche via `IGestionTache`, puis reprend l'exécution quand `TerminerEtapeAsync` est appelé.

#### Sous-builder `TacheHumaine` — forme recommandée

```csharp
.Interactif("approbation-responsable", "Approbation responsable", n => n

    // Toutes les propriétés de la tâche dans un seul bloc
    .TacheHumaine(t => t
        .Titre("Approuver la commande")
        .Description("Vérifiez les justificatifs avant de valider ou refuser.")
        .Role("RESPONSABLE_ACHAT")         // code de rôle requis
        .AssignerA("chef@corp.com")        // assignation automatique à l'arrivée
        .TypeTache("APPROBATION")          // code de type dans le système externe
        .EstUneRevision()                  // marque la tâche comme révision
        .LogonAuteur("demandeur@corp.com") // auteur de l'élément soumis
    )

    // Commande exécutée à la suspension (avant que la tâche ne soit créée)
    .AuDemarrage("PreparerContexteApprobationCommand", c => c
        .Param("commandeId"))

    // Commande exécutée à la reprise (même transaction que TerminerEtapeAsync)
    .AuRetour("EnregistrerDecisionCommand", c => c
        .Param("decision")
        .Param("commentaire"))

    .Puis("decision-approbation"))
```

#### Raccourcis pour les cas simples

```csharp
// Titre seul — sans sous-builder
.Interactif("validation-simple", n => n
    .Tache("Valider le dossier", "Vérifier les pièces justificatives")
    .Role("VALIDATEUR")
    .AssignerA("responsable@corp.com")
    .AuRetour("EnregistrerValidationCommand")
    .Puis("archiver"))
```

**Récapitulatif des méthodes de `TacheHumaine` :**

| Méthode | Propriété modifiée | Description |
|---------|-------------------|-------------|
| `.Titre(string)` | `DefinitionTache.Titre` | Intitulé affiché à l'assigné |
| `.Description(string)` | `DefinitionTache.Description` | Instructions détaillées |
| `.Role(string)` | `DefinitionTache.CodeRole` | Code de rôle requis (ex. `"RESPONSABLE"`) |
| `.AssignerA(string)` | `DefinitionTache.LogonAuto` | Logon assigné automatiquement à l'arrivée |
| `.TypeTache(string)` | `DefinitionTache.CodeTache` | Code type dans le système externe |
| `.EstUneRevision()` | `DefinitionTache.IndTacheRevision` | Marque la tâche comme révision |
| `.LogonAuteur(string)` | `DefinitionTache.LogonAuteur` | Logon de l'auteur de l'élément soumis |

> **`NomNoeud` automatique :** le moteur renseigne `DefinitionTache.NomNoeud` à partir du nom du nœud. Il n'est pas nécessaire de le spécifier.

---

### 5.5 Nœud décision

Un nœud décision évalue ses flux sortants **dans l'ordre de déclaration**. Le premier flux dont la condition est vraie est emprunté. Le flux `.Sinon` est emprunté si aucune autre condition n'est satisfaite.

#### DSL de conditions sur variables

```csharp
.Decision("decision-montant", "Seuil de montant", d => d

    // Condition sur variable : SiVariable("nom").Comparateur(valeur).Aller("noeud")
    .SiVariable("montant").EstSuperieurA(50_000m).Aller("approbation-directeur")
    .SiVariable("montant").EstSuperieurA(5_000m).Aller("approbation-responsable")

    // Branche par défaut (obligatoire si les conditions ne couvrent pas tous les cas)
    .Sinon.Aller("validation-automatique"))
```

**Opérateurs disponibles :**

| Méthode | Opérateur | Équivalent |
|---------|-----------|-----------|
| `.EstEgalA(valeur)` | `Egal` | `==` |
| `.EstDifferentDe(valeur)` | `Different` | `!=` |
| `.EstSuperieurA(valeur)` | `Superieur` | `>` |
| `.EstInferieurA(valeur)` | `Inferieur` | `<` |
| `.EstSuperieurOuEgalA(valeur)` | `SuperieurOuEgal` | `>=` |
| `.EstInferieurOuEgalA(valeur)` | `InferieurOuEgal` | `<=` |
| `.Contient(valeur)` | `Contient` | sous-chaîne (`string` uniquement) |

#### Condition query — évaluation par handler

```csharp
.Decision("decision-approbation", "Décision d'approbation", d => d

    // Condition booléenne déléguée à un IBpmHandlerQuery<bool>
    // Nom par défaut : PascalCase(id du nœud) + "Query"
    .SiQuery("EstCommandeApprouveeQuery").Aller("notification-approbation")

    // Query avec paramètres explicites
    .SiQuery("EstClientPremiumQuery", q => q
        .Param("clientId"))
    .Aller("offre-premium")

    .Sinon.Aller("notification-refus"))
```

> **`.Aller(id)` et `.Vers(id)`** sont deux alias identiques. Utilisez celui qui rend votre code le plus lisible.

---

### 5.6 Nœud attente de temps

Un nœud attente de temps **suspend** l'instance jusqu'à une date d'échéance. Le réveil est déclenché par votre scheduler (voir [§10](#10-attentes-de-temps)).

```csharp
// Échéance depuis une variable du processus (DateTime)
.AttenteTemps("attente-relance", "Attente avant relance", t => t
    .EcheanceVariable("dateRelance")
    .Puis("envoyer-relance"))

// Échéance fixe (connue à la conception)
.AttenteTemps("gel-temporaire", "Période de gel", t => t
    .EcheanceFixe(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))
    .Puis("reprendre-traitement"))

// Échéance calculée à l'exécution par un IBpmHandlerQuery<DateTime>
.AttenteTemps("attente-livraison", "Attente de la livraison", t => t
    .EcheanceQuery("DateLivraisonPrevueQuery", q => q
        .Param("commandeId")
        .ParamFixe("majoration", 2))  // +2 jours de marge
    .Puis("confirmer-reception"))
```

---

### 5.7 Nœud attente de signal

Un nœud attente de signal **suspend** l'instance jusqu'à la réception d'un signal nommé (voir [§9](#9-signaux)).

```csharp
// Forme courte : id, nom du signal, nœud suivant
.AttenteSignal("attente-paiement", "PaiementRecu", vers: "confirmer-commande")

// Avec nom d'affichage
.AttenteSignal("attente-paiement", "Attendre le paiement", "PaiementRecu", vers: "confirmer-commande")

// Nœud final si le signal met fin au processus
.AttenteSignal("attente-annulation", "AnnulationDemandee")
```

---

### 5.8 Nœud sous-processus

Un nœud sous-processus exécute un processus enfant dans la même transaction et peut remonter des variables au processus parent.

```csharp
.SousProcessus("verif-credit", "Vérification du crédit", s => s
    // Clé et version de la définition enfant
    .Definition("verification-credit", version: 1)

    // Variables remontées depuis le processus enfant vers le parent
    .Sortie("scoringCredit")
    .Sorties("limiteAutorisee", "risqueEvalue")  // plusieurs en un appel

    .Puis("decision-credit"))
```

---

### 5.9 Patron d'approbation — `PatternApprobation`

`PatternApprobation` crée automatiquement **deux nœuds** — un nœud interactif + un nœud décision — câblés l'un à l'autre. C'est le raccourci idéal pour le pattern d'approbation humaine, omniprésent en BPM.

```csharp
.PatternApprobation(
    idTache:          "approbation-responsable",  // id du nœud interactif
    intituleTache:    "Approbation responsable",  // titre affiché dans la tâche
    idDecision:       "decision-approbation",     // id du nœud décision
    commandeDecision: "EnregistrerDecisionCommand", // commande AuRetour
    queryDecision:    "EstCommandeApprouveeQuery",  // query de branchement
    versApprouve:     "notification-approbation",  // destination si query = true
    versRefuse:       "notification-refus")         // destination si query = false
```

Cela équivaut exactement à :

```csharp
.Interactif("approbation-responsable", "Approbation responsable", n => n
    .TacheHumaine(t => t.Titre("Approbation responsable"))
    .AuRetour("EnregistrerDecisionCommand")
    .Puis("decision-approbation"))

.Decision("decision-approbation", n => n
    .SiQuery("EstCommandeApprouveeQuery").Aller("notification-approbation")
    .Sinon.Aller("notification-refus"))
```

---

### 5.10 Options de Build

#### `.Build()` — standard

```csharp
var definition = ProcessusV2.Definir("ma-cle")
    // …
    .Build();
```

Valide uniquement que la clé, le nœud de début et au moins un nœud sont présents.

#### `.BuildStrict()` — validation approfondie

```csharp
var definition = ProcessusV2.Definir("ma-cle")
    // …
    .BuildStrict();
// Lève InvalidOperationException si la définition contient des incohérences.
```

`BuildStrict` détecte (voir aussi [§16](#16-validation-stricte--buildstrict)) :

| Problème détecté | Description |
|-----------------|-------------|
| **Impasse** | Nœud non final sans aucune sortie |
| **Référence pendante** | Flux pointant vers un id de nœud inexistant |
| **Nœud orphelin** | Nœud inaccessible (aucun flux ne pointe vers lui) |

#### `.Build(out string mermaid)` — avec export diagramme

```csharp
var definition = ProcessusV2.Definir("ma-cle")
    // …
    .Build(out string mermaid);

// mermaid contient un diagramme Mermaid flowchart prêt à être collé sur https://mermaid.live
Console.WriteLine(mermaid);
```

Le diagramme inclut automatiquement les métadonnées (description, auteur, étiquettes) en commentaires, et applique des couleurs différentes selon le type de nœud. Voir [§15](#15-export-mermaid) pour les détails.

---

### 5.11 Définition complète commentée

Exemple d'un processus d'approbation de commande utilisant toutes les fonctionnalités V2 :

```csharp
var definition = ProcessusV2
    .Definir("approbation-commande")
    .Intitule("Processus d'approbation de commande")
    .Description("Validation budgétaire, approbation hiérarchique et notification.")
    .Auteur("Équipe Finance")
    .Etiquettes("commandes", "approbation")
    .Commence("verifier-budget")

    // ══ Phase 1 : Vérification budgétaire ═════════════════════════════════════
    .Phase("Vérification budgétaire", phase => phase

        .Metier("verifier-budget", "Vérifier le budget", b => b
            .Param("montant")
            .Param("centreCoût")
            .Puis("decision-budget"))

        .Decision("decision-budget", "Budget suffisant ?", d => d
            .SiVariable("budgetDisponible").EstEgalA(false).Aller("refus-budget")
            .SiVariable("montant").EstSuperieurA(50_000m).Aller("approbation-directeur")
            .Sinon.Aller("approbation-responsable"))

        .Metier("refus-budget", "Refus : budget insuffisant"))

    // ══ Phase 2 : Approbation hiérarchique ════════════════════════════════════
    .Phase("Approbation", phase => phase

        .Interactif("approbation-responsable", "Approbation du responsable", n => n
            .TacheHumaine(t => t
                .Titre("Valider la demande d'achat")
                .Description("Vérifiez les justificatifs et les devis avant de valider.")
                .Role("RESPONSABLE_ACHAT"))
            .AuRetour("EnregistrerDecisionResponsableCommand")
            .Puis("decision-responsable"))

        .Decision("decision-responsable", "Décision responsable", d => d
            .SiVariable("décision").EstEgalA("Approuvé").Aller("traiter-commande")
            .Sinon.Aller("notifier-refus"))

        .Interactif("approbation-directeur", "Approbation du directeur", n => n
            .TacheHumaine(t => t
                .Titre("Valider la demande (montant élevé)")
                .Description("Cette demande dépasse 50 000 € et nécessite votre approbation.")
                .Role("DIRECTEUR")
                .EstUneRevision())
            .AuRetour("EnregistrerDecisionDirecteurCommand")
            .Puis("decision-responsable")))

    // ══ Phase 3 : Traitement et notifications ══════════════════════════════════
    .Phase("Finalisation", phase => phase

        .Metier("traiter-commande", "Traiter la commande", "notifier-approbation")

        .Metier("notifier-approbation", "Notifier l'approbation")
        .Metier("notifier-refus",       "Notifier le refus"))

    .BuildStrict();   // valide les références, orphelins et impasses
```

---

### 5.12 Approche JSON

La définition peut aussi être chargée depuis une chaîne JSON (utile pour les définitions stockées en base de données ou configurées externellement) :

```json
{
  "cle": "approbation-commande",
  "nom": "Processus d'approbation de commande",
  "noeudDebut": "valider-commande",
  "noeuds": [
    {
      "id": "valider-commande",
      "type": "NoeudMetier",
      "nom": "Valider la commande",
      "nomCommande": "ValiderCommandeCommand",
      "fluxSortants": [{ "vers": "approbation-responsable" }]
    },
    {
      "id": "approbation-responsable",
      "type": "NoeudInteractif",
      "nom": "Approbation responsable",
      "definitionTache": {
        "titre": "Approuver la commande",
        "description": "Veuillez approuver ou refuser la commande",
        "codeRole": "RESPONSABLE_ACHAT"
      },
      "commandePost": { "nomCommande": "EnregistrerDecisionCommand" },
      "fluxSortants": [{ "vers": "decision-approbation" }]
    },
    {
      "id": "decision-approbation",
      "type": "NoeudDecision",
      "nom": "Décision d'approbation",
      "fluxSortants": [
        {
          "vers": "notification-approbation",
          "condition": {
            "type": "ConditionVariable",
            "nomVariable": "statut",
            "operateur": "egal",
            "valeur": "Approuvee"
          }
        },
        { "vers": "notification-refus", "estParDefaut": true }
      ]
    },
    {
      "id": "notification-approbation",
      "type": "NoeudMetier",
      "nom": "Notifier approbation",
      "nomCommande": "NotificationApprobationCommand",
      "estFinale": true
    },
    {
      "id": "notification-refus",
      "type": "NoeudMetier",
      "nom": "Notifier refus",
      "nomCommande": "NotificationRefusCommand",
      "estFinale": true
    }
  ]
}
```

**Chargement depuis JSON :**

```csharp
var json = await File.ReadAllTextAsync("definition.json");
var definition = JsonDefinitionParser.Deserialiser(json);

// JsonDefinitionParser.Deserialiser valide automatiquement la cohérence interne.
```

**Sérialisation vers JSON :**

```csharp
var definition = ProcessusV2.Definir(…).Build();
var json = JsonDefinitionParser.Serialiser(definition);
```

---

### 5.13 Builder V1 — maintenu pour compatibilité

Le `ProcessusBuilder` (V1) est conservé et fonctionnel. Il est adapté aux processus simples (moins d'une dizaine de nœuds) où les fonctionnalités V2 ne sont pas nécessaires.

```csharp
// V1 — toujours valide
var definition = new ProcessusBuilder(
        "approbation-commande",
        "Processus d'approbation de commande",
        "valider-commande")
    .Metier("valider-commande", "Valider la commande", "approbation-responsable")
    .Interactif("approbation-responsable", "Approbation responsable", n => n
        .Tache("Approuver la commande")
        .CommandePost("EnregistrerDecisionCommand")
        .Vers("decision-approbation"))
    .Decision("decision-approbation", "Décision d'approbation", n => n
        .SiQuery("EstCommandeApprouveeQuery").Vers("notification-approbation")
        .Defaut().Vers("notification-refus"))
    .Metier("notification-approbation", "Notifier approbation")
    .Metier("notification-refus",       "Notifier refus")
    .Build();
```

Voir [§18](#18-guide-de-migration-v1--v2) pour la correspondance complète entre les APIs V1 et V2.

---

## 6. Gérer les définitions

Les définitions suivent le cycle : **Brouillon → Publiée (immuable)**.

```csharp
// IDbConnection est fourni via le scope Autofac (voir §3.1)

// 1. Sauvegarder un brouillon (peut être écrasé)
await _serviceBpm.SauvegarderDefinitionAsync(definition);

// 2. Publier (rend la définition immuable et utilisable)
await _serviceBpm.PublierDefinitionAsync("approbation-commande");

// Lister toutes les définitions (toutes versions, tous statuts)
var definitions = await _serviceBpm.ObtenirDefinitionsAsync();
```

> Une définition publiée ne peut plus être modifiée. Pour une nouvelle version, sauvegardez un nouveau brouillon avec la même clé, puis publiez-le. Les instances actives continuent sur leur version d'origine jusqu'à une migration explicite (voir [§13](#13-migration-de-version)).

---

## 7. Démarrer et suivre une instance

### Démarrer

```csharp
var variables = new Dictionary<string, object?>
{
    ["commandeId"] = 42L,
    ["montant"]    = 1500.00m
};

long idInstance = await _serviceBpm.DemarrerAsync(
    cleDefinition:      "approbation-commande",
    aggregateId:        42L,
    variablesInitiales: variables);
```

### Obtenir l'état d'une instance

```csharp
// Par ID d'instance
var instance = await _serviceBpm.ObtenirAsync(idInstance);

// Par agrégat (retourne null si aucune instance active)
var instance = await _serviceBpm.ObtenirParAggregateAsync(
    "approbation-commande", aggregateId: 42L);

// Recherche par valeur de variable
var instances = await _serviceBpm.RechercherParVariableAsync("statut", "EnAttente");
```

**Statuts possibles d'une instance :**

| Statut      | Description                                    |
|-------------|------------------------------------------------|
| `Active`    | En cours d'exécution                           |
| `Suspendue` | Bloquée sur un nœud interactif ou d'attente    |
| `EnErreur`  | Exception non récupérée lors de l'exécution    |
| `Terminee`  | Processus arrivé sur un nœud `EstFinale`       |

---

## 8. Tâches interactives et affectation

### Compléter une étape

Lorsqu'une instance est suspendue sur un nœud interactif (après qu'un utilisateur a traité la tâche), appelez `TerminerEtapeAsync` :

```csharp
// Optionnel : écrire le résultat dans les variables avant de reprendre
await _serviceBpm.ModifierVariableAsync(idInstance, "statut", "Approuvee");

// Reprendre (exécute la commande AuRetour si définie, ferme la tâche externe)
await _serviceBpm.TerminerEtapeAsync(idInstance);
```

### Affectation automatique (`.AssignerA`)

Un nœud interactif peut être pré-assigné à un logon **dès la conception** :

```csharp
.Interactif("validation-responsable", n => n
    .TacheHumaine(t => t
        .Titre("Valider le dossier")
        .AssignerA("chef.service@corp.com")   // assignation automatique à l'arrivée
        .Role("RESPONSABLE"))
    .AuRetour("EnregistrerValidationCommand")
    .Puis("archivage"))
```

À l'arrivée sur ce nœud, le moteur appelle automatiquement `IGestionTache.AssignerTacheAsync` et enregistre un événement `TacheAssignee` dans l'historique.

### Affectation manuelle (dynamique)

Pour affecter ou réaffecter un logon sur une instance suspendue :

```csharp
// Assigne le logon et appelle IGestionTache.AssignerTacheAsync
await _serviceBpm.AssignerLogonAsync(idInstance, "collaborateur@corp.com");
```

### Consulter le logon actif

```csharp
// Retourne :
//   1. Dernière affectation manuelle si postérieure à la suspension
//   2. LogonAuto de la définition sinon
//   3. null si aucun logon défini
string? logon = await _serviceBpm.ObtenirLogonTacheActiveAsync(idInstance);
```

---

## 9. Signaux

Les signaux permettent de débloquer une ou plusieurs instances suspendues sur un `NoeudAttenteSignal`.

### Signal ciblé (une seule instance)

```csharp
await _serviceBpm.EnvoyerSignalAsync(
    nomSignal:  "PaiementRecu",
    idInstance: idInstance);
```

### Signal broadcast (toutes les instances en attente)

```csharp
// idInstance omis → toutes les instances attendant "ValidationLot" sont débloquées
await _serviceBpm.EnvoyerSignalAsync("ValidationLot");
```

### Vérifier les signaux attendus

```csharp
var signaux = await _serviceBpm.ObtenirSignauxEnAttenteAsync(idInstance);
// ex. ["PaiementRecu", "ConfirmationLivraison"]
```

---

## 10. Attentes de temps

Le réveil des instances suspendues sur un `NoeudAttenteTemps` est **entièrement géré par votre application** via un scheduler (ex : Hangfire, Quartz, hosted service).

### Pattern recommandé pour le scheduler

```csharp
// Exécuté périodiquement (ex : toutes les minutes)
public async Task ReveilllerInstancesEchuesAsync()
{
    var instancesEchues = await _serviceBpm.ObtenirInstancesEchuesAsync(DateTime.UtcNow);

    foreach (var instance in instancesEchues)
    {
        await _serviceBpm.ReprendreAttenteTempsAsync(instance.IdInstance);
    }
}
```

---

## 11. Variables de processus

Les variables sont des scalaires typés (`string`, `int`, `decimal`, `DateTime`, `bool`).

### Depuis un handler (via `IContexteExecution`)

```csharp
// Lire
string statut   = contexte.Variables.Obtenir<string>("statut");
decimal montant = contexte.Variables.ObtenirOuDefaut<decimal>("montant");

// Écrire
contexte.Variables.Definir("statut", "Approuvee");
contexte.Variables.Definir("dateTraitement", DateTime.UtcNow);

// Vérifier l'existence
bool existe = contexte.Variables.Existe("codePromo");

// Lire toutes les variables
var toutes = contexte.Variables.ObtenirToutes();
```

### Depuis l'application (instance suspendue)

```csharp
// Modification externe (enregistrée dans l'historique)
await _serviceBpm.ModifierVariableAsync(idInstance, "priorite", "Haute");
```

---

## 12. Historique et audit

Chaque transition de l'instance génère automatiquement un événement d'audit.

```csharp
var historique = await _serviceBpm.ObtenirHistoriqueAsync(idInstance);

foreach (var ev in historique)
{
    Console.WriteLine(
        $"{ev.Horodatage:u} | {ev.TypeEvenement,-25} | {ev.NomNoeud} | {ev.Resultat}");
}
```

**Types d'événements enregistrés :**

| Type                | Déclencheur                                            |
|---------------------|--------------------------------------------------------|
| `DebutProcessus`    | Démarrage d'une instance                               |
| `EntreeNoeud`       | Entrée dans un nœud                                    |
| `SortieNoeud`       | Sortie d'un nœud avec succès                           |
| `NoeudSuspendu`     | Suspension (interactif, attente temps, attente signal) |
| `NoeudRepris`       | Reprise après suspension                               |
| `ErreurNoeud`       | Exception dans un handler                              |
| `FinProcessus`      | Instance terminée                                      |
| `SignalRecu`        | Signal reçu pour débloquer une attente                 |
| `VariableModifiee`  | Modification externe d'une variable                    |
| `TacheAssignee`     | Affectation manuelle via `AssignerLogonAsync`          |
| `MigrationInstance` | Migration vers une nouvelle version                    |

---

## 13. Migration de version

La migration permet de faire passer des instances actives vers une nouvelle version publiée d'une définition, sans interruption.

```csharp
// Migrer une seule instance
var resultat = await _serviceMigration.MigrerAsync(
    idInstance:   idInstance,
    versionCible: 2);

// Migrer toutes les instances actives/suspendues d'une définition
var resultats = await _serviceMigration.MigrerToutesAsync(
    cleDefinition: "approbation-commande",
    versionCible:  2);
```

### Mapping de nœuds (si des nœuds ont été renommés ou supprimés)

```csharp
var mapping = new Dictionary<string, string>
{
    ["ancien-noeud-id"] = "nouveau-noeud-id"
};

var resultat = await _serviceMigration.MigrerAsync(
    idInstance:    idInstance,
    versionCible:  2,
    mappingNoeuds: mapping);
```

> Si une instance est suspendue sur un nœud absent de la version cible et qu'aucun mapping n'est fourni, la migration lève une `MigrationImpossibleException`.

---

## 14. Gestion des erreurs

### Exceptions du moteur

| Exception                      | Cause                                                               |
|--------------------------------|---------------------------------------------------------------------|
| `NoeudIntrouvableException`    | Le nœud courant n'existe pas dans la définition                     |
| `AucuneCheminException`        | Nœud décision : aucune condition vraie et pas de branche par défaut |
| `EtatInstanceInvalideException`| `TerminerEtapeAsync` appelé sur une instance non suspendue          |
| `MigrationImpossibleException` | Nœud courant absent de la version cible sans mapping               |
| `ProcessusDejaActifException`  | Tentative de démarrer un 2e processus actif (même clé + agrégat)   |

### Responsabilité de la transaction

Le moteur **ne committe et ne rollbacke jamais**. En cas d'exception, votre application est responsable du rollback :

```csharp
using var connexion = _connectionFactory.Creer();
using var transaction = connexion.BeginTransaction();

using var scope = _container.BeginLifetimeScope(b =>
    b.RegisterInstance(connexion).As<IDbConnection>().ExternallyOwned());

var serviceBpm = scope.Resolve<IServiceBpm>();

try
{
    await serviceBpm.DemarrerAsync("approbation-commande", aggregateId, variables);
    transaction.Commit();
}
catch (Exception ex)
{
    transaction.Rollback();
    _logger.LogError(ex,
        "Erreur lors du démarrage du processus pour l'agrégat {AggregateId}", aggregateId);
    throw;
}
```

---

## 15. Export Mermaid

Tout `DefinitionProcessus` peut être converti en diagramme Mermaid flowchart.

### Via le builder (recommandé — inclut les métadonnées)

```csharp
var definition = ProcessusV2
    .Definir("approbation-commande")
    .Description("Cycle d'approbation d'une commande.")
    .Auteur("Équipe Finance")
    .Etiquettes("commandes", "approbation")
    // …nœuds…
    .Build(out string mermaid);

// mermaid est un string Mermaid flowchart TD prêt à l'emploi
```

### Via l'extension `.ToMermaid()` (sur n'importe quelle définition)

```csharp
// Fonctionne sur toute DefinitionProcessus, qu'elle soit construite par V1, V2 ou chargée depuis JSON
var mermaid = definition.ToMermaid();
Console.WriteLine(mermaid);

// Ou dans un test pour vérifier visuellement la structure
File.WriteAllText("processus.md", $"```mermaid\n{mermaid}\n```");
```

**Rendu visuel :**

Collez le contenu sur **https://mermaid.live** pour visualiser le diagramme interactivement.

**Codes couleurs par type de nœud :**

| Type de nœud | Forme Mermaid | Couleur |
|---|---|---|
| `NoeudMetier` | Rectangle | Bleu clair |
| `NoeudMetier` (final) | Stade | Vert |
| `NoeudInteractif` | Parallélogramme | Violet |
| `NoeudDecision` | Losange | Jaune |
| `NoeudAttenteTemps` | Asymétrique | Orange |
| `NoeudAttenteSignal` | Cercle | Rose |
| `NoeudSousProcessus` | Sous-routine | Gris |

**Exemple de sortie pour le processus d'approbation :**

```
flowchart TD
    %% Processus d'approbation de commande
    %% Cycle d'approbation d'une commande.
    %% Auteur : Équipe Finance
    %% Étiquettes : commandes, approbation

    classDef metier      fill:#dbeafe,stroke:#3b82f6,color:#1e3a5f
    classDef terminal    fill:#dcfce7,stroke:#16a34a,color:#14532d
    classDef interactif  fill:#ede9fe,stroke:#7c3aed,color:#3b0764
    classDef decision    fill:#fef9c3,stroke:#ca8a04,color:#713f12
    …

    valider_commande["Valider la commande"]
    class valider_commande metier
    approbation_responsable[/"👤 Approbation responsable"/]
    class approbation_responsable interactif
    decision_approbation{"◇ Décision d'approbation"}
    class decision_approbation decision
    notification_approbation(["⏹ Notifier approbation"])
    class notification_approbation terminal
    …

    debut((▶)) --> valider_commande
    valider_commande --> approbation_responsable
    approbation_responsable --> decision_approbation
    decision_approbation -->|"EstCommandeApprouveeQuery"| notification_approbation
    decision_approbation -->|"défaut"| notification_refus
```

---

## 16. Validation stricte — `BuildStrict`

`BuildStrict()` remplace `Build()` pour obtenir des garanties supplémentaires au moment de la compilation de la définition.

```csharp
var definition = ProcessusV2.Definir("ma-cle")
    // …
    .BuildStrict();
// Si des erreurs sont détectées, une InvalidOperationException liste tous les problèmes.
```

### Erreurs détectées

| Code | Problème | Exemple |
|------|----------|---------|
| `[impasse]` | Nœud non final sans flux sortant | Un nœud métier sans `.Puis()` ni `.Final()` |
| `[référence]` | Flux pointant vers un id inexistant | `.Puis("id-qui-nexiste-pas")` |
| `[orphelin]` | Nœud inaccessible (aucun flux entrant) | Nœud déclaré mais jamais référencé |

### Exemple de message d'erreur

```
La définition 'gestion-dossier' contient 2 erreur(s) :
  • [impasse]    Nœud 'traiter-demande' n'est pas final et n'a aucune sortie.
  • [référence]  Nœud 'decision-validation' → 'archiver-dossierr' introuvable dans la définition.
```

### Quand utiliser `BuildStrict` ?

- **Toujours en test** : ajoutez un test unitaire qui appelle `BuildStrict()` sur chaque définition enregistrée.
- **En CI/CD** : faites échouer le pipeline si une définition ne passe pas la validation stricte.
- **En production** : `Build()` est suffisant si les tests couvrent déjà cette vérification.

```csharp
// Exemple de test unitaire
[Fact]
public void DefinitionApprobation_EstValide()
{
    // Ne doit pas lever d'exception
    var act = () => Definitions.ApprobationCommande().BuildStrict();
    act.Should().NotThrow();
}
```

---

## 17. Référence des types de nœuds

### Vue d'ensemble

| Type | Rôle | Suspend ? |
|---|---|---|
| `NoeudMetier` | Exécute un `IBpmHandlerCommande` | Non |
| `NoeudInteractif` | Crée une tâche humaine via `IGestionTache` | Oui |
| `NoeudDecision` | Branchement XOR selon conditions | Non |
| `NoeudAttenteTemps` | Suspend jusqu'à une date calculée | Oui |
| `NoeudAttenteSignal` | Suspend jusqu'à la réception d'un signal nommé | Oui |
| `NoeudSousProcessus` | Exécute un processus enfant dans la même transaction | Si l'enfant suspend |

---

### `NoeudMetier` — méthodes du builder

| Méthode | Description |
|---------|-------------|
| `.Commande("NomExplicite")` | Surcharge le `NomCommande` déduit automatiquement (`PascalCase(id) + "Command"`) |
| `.Param("nom")` | Paramètre résolu depuis la variable de même nom |
| `.Param("nom", Src.Var("autreVar"))` | Paramètre résolu depuis une autre variable |
| `.Param("nom", Src.Val(valeur))` | Paramètre avec valeur fixe |
| `.Param("nom", Src.Query("NomQuery"))` | Paramètre résolu par une query à l'exécution |
| `.ParamFixe("nom", valeur)` | Raccourci pour `Param("nom", Src.Val(valeur))` |
| `.Puis("id")` | Nœud suivant (alias lisible de `.Vers`) |
| `.Vers("id")` | Nœud suivant |
| `.Final()` | Marque le nœud comme terminal (`EstFinale = true`) |

---

### `NoeudInteractif` — méthodes du builder

| Méthode | Description |
|---------|-------------|
| `.TacheHumaine(t => t…)` | Configure la tâche via le sous-builder `TacheV2Builder` |
| `.Tache("titre", "desc?")` | Raccourci : définit uniquement le titre (et description optionnelle) |
| `.Role("CODE")` | Code de rôle requis (inline, sans `TacheHumaine`) |
| `.AssignerA("logon")` | Logon assigné automatiquement à l'arrivée (inline) |
| `.AuDemarrage("NomCmd?", c => …)` | Commande exécutée à la suspension |
| `.AuRetour("NomCmd?", c => …)` | Commande exécutée à la reprise (`TerminerEtapeAsync`) |
| `.Puis("id")` / `.Vers("id")` | Nœud suivant |
| `.Final()` | Marque le nœud comme terminal |

**Sous-builder `TacheHumaine` (`TacheV2Builder`) :**

| Méthode | Propriété | Description |
|---------|-----------|-------------|
| `.Titre("texte")` | `Titre` | Intitulé de la tâche |
| `.Description("texte")` | `Description` | Instructions pour l'assigné |
| `.Role("CODE")` | `CodeRole` | Rôle requis |
| `.AssignerA("logon")` | `LogonAuto` | Assignation auto |
| `.TypeTache("CODE")` | `CodeTache` | Code type dans le système externe |
| `.EstUneRevision()` | `IndTacheRevision` | Marque comme révision |
| `.LogonAuteur("logon")` | `LogonAuteur` | Auteur de l'élément soumis |

---

### `NoeudDecision` — méthodes du builder

| Méthode | Description |
|---------|-------------|
| `.SiVariable("nom").Opérateur(val).Aller("id")` | Condition sur variable |
| `.SiQuery("NomQuery?", q => …).Aller("id")` | Condition query (retourne `bool`) |
| `.Sinon.Aller("id")` | Branche par défaut (aucune condition) |
| `.Aller("id")` / `.Vers("id")` | Cible d'un flux (après condition) |

**Opérateurs `SiVariable` :**

| Méthode | Opérateur | Symbole |
|---------|-----------|---------|
| `.EstEgalA(val)` | `Egal` | `=` |
| `.EstDifferentDe(val)` | `Different` | `≠` |
| `.EstSuperieurA(val)` | `Superieur` | `>` |
| `.EstInferieurA(val)` | `Inferieur` | `<` |
| `.EstSuperieurOuEgalA(val)` | `SuperieurOuEgal` | `≥` |
| `.EstInferieurOuEgalA(val)` | `InferieurOuEgal` | `≤` |
| `.Contient(val)` | `Contient` | sous-chaîne |

---

### `NoeudAttenteTemps` — méthodes du builder

| Méthode | Description |
|---------|-------------|
| `.EcheanceVariable("nomVar")` | Date lue depuis une variable du processus (`DateTime`) |
| `.EcheanceFixe(date)` | Date statique fixée à la conception |
| `.EcheanceQuery("NomQuery", q => …)` | Date calculée par `IBpmHandlerQuery<DateTime>` |
| `.Puis("id")` / `.Vers("id")` | Nœud suivant |
| `.Final()` | Nœud terminal |

---

### `NoeudAttenteSignal` — méthodes du builder

| Paramètre / Méthode | Description |
|---|---|
| `id` (1er argument) | Identifiant du nœud |
| `nom` (optionnel) | Nom d'affichage |
| `signal` | Nom du signal attendu |
| `vers:` (optionnel) | Nœud suivant ; omis → nœud terminal |

---

### `NoeudSousProcessus` — méthodes du builder

| Méthode | Description |
|---------|-------------|
| `.Definition("cle", version?)` | Clé et version de la définition enfant (`version` défaut = `1`) |
| `.Sortie("variable")` | Variable remontée du processus enfant vers le parent |
| `.Sorties("a", "b", …)` | Plusieurs variables de sortie en un appel |
| `.Puis("id")` / `.Vers("id")` | Nœud suivant |
| `.Final()` | Nœud terminal |

---

## 18. Guide de migration V1 → V2

Ce tableau liste la correspondance entre chaque méthode V1 (`ProcessusBuilder`) et son équivalent V2 (`ProcessusV2Builder`).

### Point d'entrée et métadonnées

| V1 | V2 |
|----|-----|
| `new ProcessusBuilder("cle", "nom", "debut")` | `ProcessusV2.Definir("cle").Intitule("nom").Commence("debut")` |
| *(pas d'équivalent)* | `.Description("texte")` |
| *(pas d'équivalent)* | `.Auteur("texte")` |
| *(pas d'équivalent)* | `.Etiquettes("a", "b")` |
| *(pas d'équivalent)* | `.Phase("nom", p => p…)` |
| `.Build()` | `.Build()` / `.BuildStrict()` / `.Build(out mermaid)` |

### Nœud métier

| V1 | V2 |
|----|-----|
| `.Metier("id", "nom")` | `.Metier("id", "nom")` |
| `.Metier("id", "nom", "vers")` | `.Metier("id", "nom", "vers")` |
| `.Metier("id", "nom", n => n…)` | `.Metier("id", "nom", b => b…)` |
| `.Metier("id", n => n…)` | `.Metier("id", b => b…)` |
| `b.Commande("nom")` | `b.Commande("nom")` |
| `b.Param("nom")` | `b.Param("nom")` |
| `b.Param("nom", Src.Var("v"))` | `b.Param("nom", Src.Var("v"))` |
| `b.Param("nom", Src.Val(val))` | `b.ParamFixe("nom", val)` |
| `b.Vers("id")` | `b.Puis("id")` ou `b.Vers("id")` |
| `b.Final()` | `b.Final()` |

### Nœud interactif

| V1 | V2 |
|----|-----|
| `.Interactif("id", "nom", n => n…)` | `.Interactif("id", "nom", n => n…)` |
| `n.Tache("titre", "desc?")` | `n.Tache("titre", "desc?")` ou `n.TacheHumaine(t => t…)` |
| `n.LogonAuto("logon")` | `n.AssignerA("logon")` ou `t.AssignerA("logon")` dans `TacheHumaine` |
| `n.CodeRole("CODE")` | `n.Role("CODE")` ou `t.Role("CODE")` dans `TacheHumaine` |
| `n.CodeTache("CODE")` | `t.TypeTache("CODE")` dans `TacheHumaine` |
| `n.TacheRevision()` | `t.EstUneRevision()` dans `TacheHumaine` |
| `n.LogonAuteur("logon")` | `t.LogonAuteur("logon")` dans `TacheHumaine` |
| `n.CommandePre("Nom?", c => …)` | `n.AuDemarrage("Nom?", c => …)` |
| `n.CommandePost("Nom?", c => …)` | `n.AuRetour("Nom?", c => …)` |
| `n.Vers("id")` | `n.Puis("id")` ou `n.Vers("id")` |
| `n.Final()` | `n.Final()` |

### Nœud décision

| V1 | V2 |
|----|-----|
| `.Decision("id", "nom", n => n…)` | `.Decision("id", "nom", d => d…)` |
| `n.SiEgal("var", val).Vers("id")` | `d.SiVariable("var").EstEgalA(val).Aller("id")` |
| `n.SiDiff("var", val).Vers("id")` | `d.SiVariable("var").EstDifferentDe(val).Aller("id")` |
| `n.SiSup("var", val).Vers("id")` | `d.SiVariable("var").EstSuperieurA(val).Aller("id")` |
| `n.SiInf("var", val).Vers("id")` | `d.SiVariable("var").EstInferieurA(val).Aller("id")` |
| `n.SiSupEgal("var", val).Vers("id")` | `d.SiVariable("var").EstSuperieurOuEgalA(val).Aller("id")` |
| `n.SiInfEgal("var", val).Vers("id")` | `d.SiVariable("var").EstInferieurOuEgalA(val).Aller("id")` |
| `n.SiContient("var", val).Vers("id")` | `d.SiVariable("var").Contient(val).Aller("id")` |
| `n.SiQuery("NomQuery").Vers("id")` | `d.SiQuery("NomQuery").Aller("id")` |
| `n.Defaut().Vers("id")` | `d.Sinon.Aller("id")` |

### Nœud attente de temps

| V1 | V2 |
|----|-----|
| `.AttenteTemps("id", "nom", t => t…)` | `.AttenteTemps("id", "nom", t => t…)` |
| `t.Echeance("variable")` | `t.EcheanceVariable("variable")` |
| `t.Echeance(date)` | `t.EcheanceFixe(date)` |
| `t.EcheanceQuery("NomQuery", c => …)` | `t.EcheanceQuery("NomQuery", c => …)` |
| `t.Vers("id")` | `t.Puis("id")` ou `t.Vers("id")` |

### Nœud attente de signal

| V1 | V2 |
|----|-----|
| `.AttenteSignal("id", "signal", "vers?")` | `.AttenteSignal("id", "signal", vers: "vers?")` |
| `.AttenteSignal("id", "nom", "signal", "vers?")` | `.AttenteSignal("id", "nom", "signal", vers: "vers?")` |

### Nœud sous-processus

| V1 | V2 |
|----|-----|
| `.SousProcessus("id", "nom", s => s…)` | `.SousProcessus("id", "nom", s => s…)` |
| `s.Definition("cle", version)` | `s.Definition("cle", version)` |
| `s.Sortie("var")` | `s.Sortie("var")` |
| `s.Sorties("a", "b")` | `s.Sorties("a", "b")` |
| `s.Vers("id")` | `s.Puis("id")` ou `s.Vers("id")` |
