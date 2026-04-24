# Guide d'utilisation — BpmPlus (application cliente)

> Version : 1.3 — Destiné aux développeurs intégrant BpmPlus dans une application .NET 8

---

## Table des matières

1. [Vue d'ensemble](#1-vue-densemble)
2. [Installation](#2-installation)
3. [Configuration (Autofac)](#3-configuration-autofac)
   - [3.2 Démarrage complet](#32-démarrage-complet--de-linstallation-au-premier-processus)
4. [Implémenter les handlers](#4-implémenter-les-handlers)
5. [Définir un processus](#5-définir-un-processus)
6. [Gérer les définitions](#6-gérer-les-définitions)
7. [Démarrer et suivre une instance](#7-démarrer-et-suivre-une-instance)
8. [Compléter une étape interactive](#8-compléter-une-étape-interactive)
9. [Signaux](#9-signaux)
10. [Attentes de temps](#10-attentes-de-temps)
11. [Variables de processus](#11-variables-de-processus)
12. [Historique et audit](#12-historique-et-audit)
13. [Migration de version](#13-migration-de-version)
14. [Gestion des erreurs](#14-gestion-des-erreurs)
15. [Référence des types de nœuds](#15-référence-des-types-de-nœuds)

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

Les quatre étapes ci-dessous constituent le setup minimal pour intégrer BpmPlus dans une application.

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

#### Étape 3 — Enregistrer et publier une définition

À faire une fois par définition (ou à chaque nouvelle version) :

```csharp
var definition = new DefinitionProcessusBuilder("approbation-commande",
        "Processus d'approbation", "valider-commande")
    .AjouterNoeudMetier("valider-commande", "Valider la commande", vers: "notifier")
    .AjouterNoeudMetier("notifier", "Notifier le résultat")
    .Construire();

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

À chaque fois qu'un processus doit être lancé pour un agrégat :

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

Les sections suivantes détaillent chaque aspect individuellement.

---

## 4. Implémenter les handlers

### 4.1 Handler de commande (`IBpmHandlerCommande`)

Utilisé par les nœuds métier et les commandes PRE/POST des nœuds interactifs.

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

> **Convention de nommage :** le moteur calcule le `NomCommande` par défaut à partir de l'id du nœud :
> `PascalCase(id) + "Command"` — ex. `"valider-commande"` → `"ValiderCommandeCommand"`.
> Pour nommer différemment, utilisez `.CommandeNommee("AutreNom")` dans le builder.

### 4.2 Handler de query (`IBpmHandlerQuery<T>`)

Utilisé pour les conditions de nœud décision (`ConditionQuery`) et les dates d'échéance (`NoeudAttenteTemps`).

```csharp
// Pour une condition booléenne
public class EstCommandeUrgente : IBpmHandlerQuery<bool>
{
    private readonly IDbConnection _connection;

    public EstCommandeUrgente(IDbConnection connection) => _connection = connection;

    public string NomQuery => "EstCommandeUrgente";

    public async Task<bool> ExecuterAsync(
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte)
    {
        // aggregateId vient automatiquement de l'instance (contexte.AggregateId)
        using var repo = new CommandeRepository(_connection);
        var commande = await repo.ObtenirAsync(aggregateId!.Value);
        return commande.MontantTotal > 10_000;
    }
}

// Pour résoudre une date d'échéance
public class CalculerDateRelance : IBpmHandlerQuery<DateTime>
{
    public string NomQuery => "CalculerDateRelance";

    public async Task<DateTime> ExecuterAsync(
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte)
    {
        return DateTime.UtcNow.AddDays(3);
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
        // Créer la tâche dans votre système (base de données, API externe, etc.)
        var idTache = await _tacheService.CreerAsync(new Tache
        {
            Titre            = definitionTache.Titre,
            Description      = definitionTache.Description,
            Categorie        = definitionTache.Categorie,
            NomNoeud         = definitionTache.NomNoeud,         // renseigné automatiquement par le moteur
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
        // variables contient le snapshot des variables de l'instance au moment de la complétion
        var statut = variables.TryGetValue("statut", out var v) ? v?.ToString() : null;

        await _tacheService.FermerAsync(idTacheExterne, statut, ct);
    }

    public async Task AssignerTacheAsync(long idTacheExterne, string assignee, CancellationToken ct = default)
    {
        await _tacheService.AssignerAsync(idTacheExterne, assignee, ct);
    }
}
```

---

## 5. Définir un processus

Deux approches sont disponibles : **Fluent C#** (recommandée pour les définitions statiques) et **JSON** (utile pour les définitions dynamiques ou stockées).

### 5.1 Approche Fluent C#

```csharp
// Clé + nom + nœud de début en une seule ligne
var definition = new DefinitionProcessusBuilder("approbation-commande",
        "Processus d'approbation de commande", "valider-commande")

    // Nœud métier ultra-compact : id = commande (ValiderCommandeCommand), aggregate depuis l'instance
    .AjouterNoeudMetier("valider-commande", "Valider la commande", vers: "approbation-responsable")

    // Nœud interactif : DefinirTache(titre, desc) + AvecCommandePost(nomCommande)
    .AjouterNoeudInteractif("approbation-responsable", "Approbation responsable", n => n
        .DefinirTache("Approuver la commande", "Veuillez approuver ou refuser la commande")
        .AvecCommandePost("EnregistrerDecisionCommand")
        .Vers("decision-approbation"))

    // Nœud décision : SiEgal / SiDifferent à la place de SiVariable(..., Operateur.Egal, ...)
    .AjouterNoeudDecision("decision-approbation", "Décision d'approbation", n => n
        .SiEgal("statut", "Approuvee").Vers("notification-approbation")
        .ParDefaut().Vers("notification-refus"))

    // Nœud final compact : vers omis → EstFinale implicite
    .AjouterNoeudMetier("notification-approbation", "Notifier approbation")
    .AjouterNoeudMetier("notification-refus", "Notifier refus")

    .Construire();
```

> **Nommage des commandes :** par convention, le `NomCommande` d'un nœud est `PascalCase(id) + "Command"`.
> Le nœud `"valider-commande"` résout automatiquement le handler dont `NomCommande == "ValiderCommandeCommand"`.
> Utilisez `.CommandeNommee("NomExplicite")` dans la forme lambda pour déroger à cette convention.

**Récapitulatif des raccourcis disponibles :**

| Raccourci | Description |
|-----------|-------------|
| `new Builder("cle", "nom", "id")` | Constructeur 3 paramètres |
| `AjouterNoeudMetier("id", "nom", vers: "suivant")` | Compact : id → commande, aggregate depuis l'instance |
| `AjouterNoeudMetier("id", "nom")` | Idem, nœud finale |
| `AjouterNoeudMetier("id", "nom", n => n...)` | Lambda pour paramètres additionnels |
| `.CommandeNommee("NomExplicite")` | Surcharge le nom de commande (déroge à la convention) |
| `.DefinirTache("titre", "description")` | Raccourci sans sous-builder |
| `.LogonAuto("logon")` | Assignation automatique du logon à l'arrivée sur le nœud interactif |
| `.CodeRole("CODE")` | Code de rôle requis pour la tâche |
| `.CodeTache("CODE")` | Code identifiant le type de tâche dans le système externe |
| `.TacheRevision()` | Marque la tâche comme révision (`IndTacheRevision = true`) |
| `.LogonAuteur("logon")` | Logon de l'auteur de l'élément soumis à la tâche |
| `.SortiesVariables("a", "b")` | Plusieurs variables de sortie en un appel |
| `.SiEgal("x", val)` | `SiVariable(..., Operateur.Egal, ...)` |
| `.SiDifferent("x", val)` | `SiVariable(..., Operateur.Different, ...)` |
| `.SiSuperieur("x", val)` | `SiVariable(..., Operateur.Superieur, ...)` |
| `.SiInferieur("x", val)` | `SiVariable(..., Operateur.Inferieur, ...)` |
| `.SiContient("x", val)` | `SiVariable(..., Operateur.Contient, ...)` |

### 5.1.1 Nœud attente de temps

```csharp
// Échéance depuis une variable
.AjouterNoeudAttenteTemps("attente-relance", "Attente avant relance", n => n
    .EcheanceDepuisVariable("dateRelance")
    .Vers("envoyer-relance"))

// Échéance calculée par une query, avec paramètres (aggregate depuis l'instance)
.AjouterNoeudAttenteTemps("attente-validation", "Attente validation", n => n
    .EcheanceDepuisQuery("CalculerDelaiValidation")
    .ParametreQueryStatique("delaiJours", 7)
    .ParametreQueryDepuisVariable("priorite", "prioriteCommande")
    .Vers("valider"))
```

### 5.1.2 Nœud attente de signal

```csharp
.AjouterNoeudAttenteSignal("attente-paiement", "Attendre paiement", n => n
    .Signal("PaiementRecu")
    .Vers("confirmer-commande"))
```

### 5.1.3 Nœud sous-processus

```csharp
// SortiesVariables(params) pour remonter plusieurs variables en un appel
.AjouterNoeudSousProcessus("verif-credit", "Vérification crédit", n => n
    .DefinitionEnfant("verification-credit", version: 1)
    .SortiesVariables("scoringCredit", "limiteAutorisee")
    .Vers("decision-credit"))
```

### 5.2 Approche JSON

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
        "description": "Veuillez approuver ou refuser la commande"
      },
      "commandePost": {
        "nomCommande": "EnregistrerDecisionCommand"
      },
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
            "operateur": "Egal",
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

> Une définition publiée ne peut plus être modifiée. Pour une nouvelle version, sauvegardez un nouveau brouillon avec la même clé, puis publiez-le.

---

## 7. Démarrer et suivre une instance

### Démarrer

```csharp
// IDbConnection est fourni via le scope Autofac (voir §3.1)

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
// IDbConnection est fourni via le scope Autofac (voir §3.1)

// Optionnel : écrire le résultat de la tâche dans les variables avant de reprendre
await _serviceBpm.ModifierVariableAsync(idInstance, "statut", "Approuvee");

// Reprendre l'exécution (exécute la CommandePost si définie, ferme la tâche externe)
await _serviceBpm.TerminerEtapeAsync(idInstance);
```

### Affectation automatique (LogonAuto)

Un nœud interactif peut se voir attribuer un logon **à la conception** via `LogonAuto`. Lors de l'arrivée sur ce nœud, le moteur appelle automatiquement `IGestionTache.AssignerTacheAsync` et enregistre le logon dans le détail de l'événement `NoeudSuspendu`.

```csharp
// Dans la définition du processus (ProcessusBuilder)
.Interactif("validation-responsable", b => b
    .Tache("Valider le dossier", "Vérifier les pièces justificatives")
    .LogonAuto("chef.service@corp.com")   // assignation auto à l'arrivée sur le nœud
    .CodeRole("RESPONSABLE")              // rôle requis pour la tâche
    .CodeTache("VALID-DOSSIER")           // type de tâche dans le système externe
    .Vers("archivage"))
```

```json
// Équivalent JSON
{
  "id": "validation-responsable",
  "type": "NoeudInteractif",
  "nom": "Validation responsable",
  "definitionTache": {
    "titre": "Valider le dossier",
    "description": "Vérifier les pièces justificatives",
    "logonAuto": "chef.service@corp.com",
    "codeRole": "RESPONSABLE",
    "codeTache": "VALID-DOSSIER"
  }
}
```

> **`NomNoeud` automatique :** le moteur renseigne automatiquement `DefinitionTache.NomNoeud` à partir du nom du nœud (ou de son id si aucun nom n'est défini). Il n'est pas nécessaire de le préciser manuellement.

### Affectation manuelle

Pour affecter (ou réaffecter) dynamiquement un logon sur une instance suspendue :

```csharp
// Assigne le logon et appelle IGestionTache.AssignerTacheAsync
// Enregistre un événement TacheAssignee dans l'historique
await _serviceBpm.AssignerLogonAsync(idInstance, "collaborateur@corp.com");
```

### Consulter le logon actif

```csharp
// Retourne le logon le plus récent :
// 1. Dernière affectation manuelle (TacheAssignee) si postérieure à la suspension
// 2. LogonAuto de la définition sinon
// 3. null si aucun logon n'a été défini
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

### Signal broadcast (toutes les instances en attente de ce signal)

```csharp
await _serviceBpm.EnvoyerSignalAsync("ValidationLot");
// idInstance omis → toutes les instances attendant "ValidationLot" sont débloquées
```

### Vérifier les signaux attendus par une instance

```csharp
var signaux = await _serviceBpm.ObtenirSignauxEnAttenteAsync(idInstance);
// Retourne ex : ["PaiementRecu", "ConfirmationLivraison"]
```

---

## 10. Attentes de temps

Le réveil des instances suspendues sur un `NoeudAttenteTemps` est **entièrement géré par votre application** via un scheduler (ex : Hangfire, Quartz, hosted service).

### Pattern recommandé pour le scheduler

```csharp
// Exécuté périodiquement (ex : toutes les minutes)
// IDbConnection est fourni via le scope Autofac (voir §3.1)
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
decimal montant = contexte.Variables.ObtenirOuDefaut<decimal>("montant"); // null si absent

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
// Modification externe d'une variable (enregistrée dans l'historique)
await _serviceBpm.ModifierVariableAsync(
    idInstance:  idInstance,
    nomVariable: "priorite",
    valeur:      "Haute");
```

---

## 12. Historique et audit

Chaque transition de l'instance génère automatiquement un événement d'audit.

```csharp
var historique = await _serviceBpm.ObtenirHistoriqueAsync(idInstance);

foreach (var evenement in historique)
{
    Console.WriteLine($"{evenement.Horodatage:u} | {evenement.TypeEvenement,-25} | {evenement.NomNoeud} | {evenement.Resultat}");
}
```

**Types d'événements enregistrés :**

| Type                | Déclencheur                                           |
|---------------------|-------------------------------------------------------|
| `DebutProcessus`    | Démarrage d'une instance                              |
| `EntreeNoeud`       | Entrée dans un nœud                                   |
| `SortieNoeud`       | Sortie d'un nœud avec succès                          |
| `NoeudSuspendu`     | Suspension (interactif, attente temps, attente signal) |
| `NoeudRepris`       | Reprise après suspension                              |
| `ErreurNoeud`       | Exception dans un handler                             |
| `FinProcessus`      | Instance terminée                                     |
| `SignalRecu`        | Signal reçu pour débloquer une attente                |
| `VariableModifiee`  | Modification externe d'une variable                   |
| `TacheAssignee`     | Affectation manuelle d'un logon via `AssignerLogonAsync` |
| `MigrationInstance` | Migration vers une nouvelle version                   |

---

## 13. Migration de version

La migration permet de faire passer des instances actives vers une nouvelle version publiée d'une définition, sans interruption.

```csharp
// IDbConnection est fourni via le scope Autofac (voir §3.1)

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

> Si une instance est suspendue sur un nœud qui n'existe plus dans la version cible et qu'aucun mapping n'est fourni, la migration lève une `MigrationImpossibleException`.

---

## 14. Gestion des erreurs

### Exceptions du moteur

| Exception                      | Cause                                                              |
|--------------------------------|--------------------------------------------------------------------|
| `NoeudIntrouvableException`    | Le nœud courant n'existe pas dans la définition                    |
| `AucuneCheminException`        | Nœud décision : aucune condition vraie et pas de branche par défaut|
| `EtatInstanceInvalideException`| `TerminerEtapeAsync` appelé sur une instance non suspendue         |
| `MigrationImpossibleException` | Nœud courant absent de la version cible sans mapping              |
| `ProcessusDejaActifException`  | Tentative de démarrer un 2e processus actif (même clé + agrégat)  |

### Responsabilité de la transaction

Le moteur **ne committe et ne rollbacke jamais**. En cas d'exception, votre application est responsable du rollback :

```csharp
// Le moteur ne committe et ne rollbacke jamais — c'est la responsabilité de l'application.
using var connexion = _connectionFactory.Creer();
using var transaction = connexion.BeginTransaction();

using var scope = _container.BeginLifetimeScope(b =>
    b.RegisterInstance(connexion)
     .As<IDbConnection>()
     .ExternallyOwned());

var serviceBpm = scope.Resolve<IServiceBpm>();

try
{
    await serviceBpm.DemarrerAsync("approbation-commande", aggregateId, variables);
    transaction.Commit();
}
catch (Exception ex)
{
    transaction.Rollback();
    _logger.LogError(ex, "Erreur lors du démarrage du processus pour l'agrégat {AggregateId}", aggregateId);
    throw;
}
```

---

## 15. Référence des types de nœuds

| Type                  | Rôle                                                    | Suspend ? |
|-----------------------|---------------------------------------------------------|-----------|
| `NoeudMetier`         | Exécute un `IBpmHandlerCommande`                        | Non       |
| `NoeudInteractif`     | Crée une tâche humaine via `IGestionTache`              | Oui       |
| `NoeudDecision`       | Branchement XOR selon conditions                        | Non       |
| `NoeudAttenteTemps`   | Suspend jusqu'à une date calculée                       | Oui       |
| `NoeudAttenteSignal`  | Suspend jusqu'à la réception d'un signal nommé          | Oui       |
| `NoeudSousProcessus`  | Exécute un processus enfant dans la même transaction    | Si enfant suspend |

### Sources de paramètres par contexte

> **Aggregate id :** toujours fourni automatiquement depuis `IContexteExecution.AggregateId` (transmis au démarrage de l'instance). Il n'est jamais à spécifier dans la définition du nœud.

**`NoeudMetier` — paramètres additionnels :**

| Méthode | Description |
|---------|-------------|
| `.CommandeNommee("NomExplicite")` | Surcharge le nom de commande (défaut : `PascalCase(id) + "Command"`) |
| `.ParametreDepuisVariable("nomParam", "nomVar")` | Paramètre résolu depuis une variable |
| `.ParametreStatique("nomParam", valeur)` | Paramètre à valeur fixe |

**`DefinitionCommande` (PRE/POST d'un `NoeudInteractif`) — via `CommandeBuilder` :**

| Méthode | Description |
|---------|-------------|
| `.AvecCommandePre("NomCommande")` | Commande PRE sans paramètre (aggregate depuis l'instance) |
| `.AvecCommandePre("NomCommande", p => p.ParametreDepuisVariable("n", "v"))` | Avec paramètre depuis variable |
| `.AvecCommandePre("NomCommande", p => p.ParametreStatique("n", val))` | Avec paramètre statique |
| *(idem pour `.AvecCommandePost`)* | |

**`NoeudAttenteTemps` — date d'échéance :**

| Méthode | Description |
|---------|-------------|
| `.EcheanceDepuisVariable("nomVar")` | Date lue depuis une variable (`DateTime`) |
| `.EcheanceStatique(date)` | Date fixe dans la définition |
| `.EcheanceDepuisQuery("NomQuery")` | Date calculée par un `IBpmHandlerQuery<DateTime>` (aggregate depuis l'instance) |
| `.ParametreQueryDepuisVariable("nomParam", "nomVar")` | Paramètre query depuis une variable |
| `.ParametreQueryStatique("nomParam", valeur)` | Paramètre query à valeur fixe |

**`NoeudDecision` — conditions :**

| Méthode | Description |
|---------|-------------|
| `.SiVariable("nomVar", Operateur.X, valeur)` | Condition sur une variable du processus |
| `.SiQuery("NomQuery")` | Condition évaluée par un `IBpmHandlerQuery<bool>` (aggregate depuis l'instance) |
| `.ParametreQueryDepuisVariable("nomParam", "nomVar")` | Paramètre query depuis une variable |
| `.ParametreQueryStatique("nomParam", valeur)` | Paramètre query à valeur fixe |
| `.ParDefaut()` | Branche de repli si aucune condition n'est vraie |

### Opérateurs de condition (`ConditionVariable`)

| Opérateur           | Description         |
|---------------------|---------------------|
| `Egal`              | `==`                |
| `Different`         | `!=`                |
| `Superieur`         | `>`                 |
| `Inferieur`         | `<`                 |
| `SuperieurOuEgal`   | `>=`                |
| `InferieurOuEgal`   | `<=`                |
| `Contient`          | Sous-chaîne (`string` uniquement) |
