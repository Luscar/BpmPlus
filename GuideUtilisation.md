# Guide d'utilisation — BpmPlus (application cliente)

> Version : 1.1 — Destiné aux développeurs intégrant BpmPlus dans une application .NET 8

---

## Table des matières

1. [Vue d'ensemble](#1-vue-densemble)
2. [Installation](#2-installation)
3. [Configuration (Autofac)](#3-configuration-autofac)
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
    // Découverte automatique de tous les IHandlerCommande et IHandlerQuery<>
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
- `IServiceFlux` — service principal (scoped)
- `IServiceMigration` — migration de versions (scoped)
- Tous les handlers trouvés via `ScanHandlers`

---

## 4. Implémenter les handlers

### 4.1 Handler de commande (`IHandlerCommande`)

Utilisé par les nœuds métier et les commandes PRE/POST des nœuds interactifs.

```csharp
public class ValiderCommandeHandler : IHandlerCommande
{
    public string NomCommande => "ValiderCommande";

    public async Task ExecuterAsync(
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte)
    {
        // aggregateId : ID de l'agrégat métier (votre commande, facture, etc.)
        // parametres  : valeurs résolues depuis les variables du processus
        // contexte    : accès à la transaction, aux variables, à l'ID d'instance

        using var repo = new CommandeRepository(contexte.Transaction);
        await repo.ValiderAsync(aggregateId!.Value);

        // Écrire une variable de processus si nécessaire
        contexte.Variables.Definir("statut", "Validee");
    }
}
```

> Le `NomCommande` doit correspondre exactement à la valeur déclarée dans le nœud de la définition de processus.

### 4.2 Handler de query (`IHandlerQuery<T>`)

Utilisé pour les conditions de nœud décision (`ConditionQuery`) et les dates d'échéance (`NoeudAttenteTemps`).

```csharp
// Pour une condition booléenne
public class EstCommandeUrgente : IHandlerQuery<bool>
{
    public string NomQuery => "EstCommandeUrgente";

    public async Task<bool> ExecuterAsync(
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte)
    {
        using var repo = new CommandeRepository(contexte.Transaction);
        var commande = await repo.ObtenirAsync(aggregateId!.Value);
        return commande.MontantTotal > 10_000;
    }
}

// Pour résoudre une date d'échéance
public class CalculerDateRelance : IHandlerQuery<DateTime>
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
    public async Task<string> CreerTacheAsync(
        DefinitionTache definitionTache,
        InstanceProcessus instance,
        CancellationToken ct = default)
    {
        // Créer la tâche dans votre système (base de données, API externe, etc.)
        var idTache = await _tacheService.CreerAsync(new Tache
        {
            Titre       = definitionTache.Titre,
            Description = definitionTache.Description,
            AggregateId = instance.AggregateId
        }, ct);

        return idTache.ToString();
    }

    public async Task FermerTacheAsync(string idTacheExterne, CancellationToken ct = default)
    {
        await _tacheService.FermerAsync(long.Parse(idTacheExterne), ct);
    }

    public async Task AssignerTacheAsync(string idTacheExterne, string assignee, CancellationToken ct = default)
    {
        await _tacheService.AssignerAsync(long.Parse(idTacheExterne), assignee, ct);
    }
}
```

---

## 5. Définir un processus

Deux approches sont disponibles : **Fluent C#** (recommandée pour les définitions statiques) et **JSON** (utile pour les définitions dynamiques ou stockées).

### 5.1 Approche Fluent C#

Le nom du nœud peut être passé directement en deuxième argument de `Ajouter*`, et les attributs courants se combinent en un seul appel :

```csharp
var definition = new DefinitionProcessusBuilder("approbation-commande")
    .Nommer("Processus d'approbation de commande")
    .Debuter("valider-commande")

    // Nom en 2e argument — CommandeNommee(commande, aggregateIdVariable) en un appel
    .AjouterNoeudMetier("valider-commande", "Valider la commande", n => n
        .CommandeNommee("ValiderCommande", "commandeId")
        .Vers("approbation-responsable"))

    // DefinirTache(titre, description) sans sous-builder
    // AvecCommandePost(commande, aggregateIdVariable) en un appel
    .AjouterNoeudInteractif("approbation-responsable", "Approbation responsable", n => n
        .DefinirTache("Approuver la commande", "Veuillez approuver ou refuser la commande")
        .AvecCommandePost("EnregistrerDecision", "commandeId")
        .Vers("decision-approbation"))

    .AjouterNoeudDecision("decision-approbation", "Décision d'approbation", n => n
        .SiVariable("statut", Operateur.Egal, "Approuvee").Vers("notification-approbation")
        .ParDefaut().Vers("notification-refus"))

    .AjouterNoeudMetier("notification-approbation", "Notifier approbation", n => n
        .CommandeNommee("NotifierApprobation", "commandeId")
        .EstFinale())

    .AjouterNoeudMetier("notification-refus", "Notifier refus", n => n
        .CommandeNommee("NotifierRefus", "commandeId")
        .EstFinale())

    .Construire();
```

**Récapitulatif des raccourcis disponibles :**

| Avant | Après |
|-------|-------|
| `AjouterNoeudMetier("id", n => n.Nommer("X")...)` | `AjouterNoeudMetier("id", "X", n => n...)` |
| `.CommandeNommee("X").AggregateIdDepuisVariable("y")` | `.CommandeNommee("X", "y")` |
| `.DefinirTache(t => t.Titre("X").Description("Y"))` | `.DefinirTache("X", "Y")` |
| `.AvecCommandePre("X", p => p.AggregateIdDepuisVariable("y"))` | `.AvecCommandePre("X", "y")` |
| `.AvecCommandePost("X", p => p.AggregateIdDepuisVariable("y"))` | `.AvecCommandePost("X", "y")` |
| `.SortieVariable("a").SortieVariable("b").SortieVariable("c")` | `.SortiesVariables("a", "b", "c")` |

### 5.1.1 Nœud attente de temps

```csharp
// Échéance depuis une variable
.AjouterNoeudAttenteTemps("attente-relance", "Attente avant relance", n => n
    .EcheanceDepuisVariable("dateRelance")
    .Vers("envoyer-relance"))

// Échéance calculée par une query, avec paramètres
.AjouterNoeudAttenteTemps("attente-validation", "Attente validation", n => n
    .EcheanceDepuisQuery("CalculerDelaiValidation", "commandeId")
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
      "nomCommande": "ValiderCommande",
      "sourceAggregateId": { "type": "Variable", "nom": "commandeId" },
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
        "nomCommande": "EnregistrerDecision",
        "sourceAggregateId": { "type": "Variable", "nom": "commandeId" }
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
      "nomCommande": "NotifierApprobation",
      "sourceAggregateId": { "type": "Variable", "nom": "commandeId" },
      "estFinale": true
    },
    {
      "id": "notification-refus",
      "type": "NoeudMetier",
      "nom": "Notifier refus",
      "nomCommande": "NotifierRefus",
      "sourceAggregateId": { "type": "Variable", "nom": "commandeId" },
      "estFinale": true
    }
  ]
}
```

---

## 6. Gérer les définitions

Les définitions suivent le cycle : **Brouillon → Publiée (immuable)**.

```csharp
// Votre application fournit toujours la transaction
using var connexion = _connectionFactory.Creer();
using var transaction = connexion.BeginTransaction();

// 1. Sauvegarder un brouillon (peut être écrasé)
await _serviceFlux.SauvegarderDefinitionAsync(definition, transaction);

// 2. Publier (rend la définition immuable et utilisable)
await _serviceFlux.PublierDefinitionAsync("approbation-commande", transaction);

transaction.Commit();

// Lister toutes les définitions (toutes versions, tous statuts)
var definitions = await _serviceFlux.ObtenirDefinitionsAsync(transaction);
```

> Une définition publiée ne peut plus être modifiée. Pour une nouvelle version, sauvegardez un nouveau brouillon avec la même clé, puis publiez-le.

---

## 7. Démarrer et suivre une instance

### Démarrer

```csharp
using var connexion = _connectionFactory.Creer();
using var transaction = connexion.BeginTransaction();

var variables = new Dictionary<string, object?>
{
    ["commandeId"] = 42L,
    ["montant"]    = 1500.00m
};

long idInstance = await _serviceFlux.DemarrerAsync(
    cleDefinition:      "approbation-commande",
    aggregateId:        42L,
    variablesInitiales: variables,
    transaction:        transaction);

transaction.Commit();
```

### Obtenir l'état d'une instance

```csharp
// Par ID d'instance
var instance = await _serviceFlux.ObtenirAsync(idInstance, transaction);

// Par agrégat (retourne null si aucune instance active)
var instance = await _serviceFlux.ObtenirParAggregateAsync(
    "approbation-commande", aggregateId: 42L, transaction);

// Recherche par valeur de variable
var instances = await _serviceFlux.RechercherParVariableAsync(
    "statut", "EnAttente", transaction);
```

**Statuts possibles d'une instance :**

| Statut      | Description                                    |
|-------------|------------------------------------------------|
| `Active`    | En cours d'exécution                           |
| `Suspendue` | Bloquée sur un nœud interactif ou d'attente    |
| `EnErreur`  | Exception non récupérée lors de l'exécution    |
| `Terminee`  | Processus arrivé sur un nœud `EstFinale`       |

---

## 8. Compléter une étape interactive

Lorsqu'une instance est suspendue sur un nœud interactif (après qu'un utilisateur a traité la tâche), appelez `TerminerEtapeAsync` :

```csharp
using var connexion = _connectionFactory.Creer();
using var transaction = connexion.BeginTransaction();

// Optionnel : écrire le résultat de la tâche dans les variables avant de reprendre
await _serviceFlux.ModifierVariableAsync(idInstance, "statut", "Approuvee", transaction);

// Reprendre l'exécution (exécute la CommandePost si définie, ferme la tâche externe)
await _serviceFlux.TerminerEtapeAsync(idInstance, transaction);

transaction.Commit();
```

---

## 9. Signaux

Les signaux permettent de débloquer une ou plusieurs instances suspendues sur un `NoeudAttenteSignal`.

### Signal ciblé (une seule instance)

```csharp
await _serviceFlux.EnvoyerSignalAsync(
    nomSignal:   "PaiementRecu",
    transaction: transaction,
    idInstance:  idInstance);
```

### Signal broadcast (toutes les instances en attente de ce signal)

```csharp
await _serviceFlux.EnvoyerSignalAsync(
    nomSignal:   "ValidationLot",
    transaction: transaction);
// idInstance omis → toutes les instances attendant "ValidationLot" sont débloquées
```

### Vérifier les signaux attendus par une instance

```csharp
var signaux = await _serviceFlux.ObtenirSignauxEnAttenteAsync(idInstance, transaction);
// Retourne ex : ["PaiementRecu", "ConfirmationLivraison"]
```

---

## 10. Attentes de temps

Le réveil des instances suspendues sur un `NoeudAttenteTemps` est **entièrement géré par votre application** via un scheduler (ex : Hangfire, Quartz, hosted service).

### Pattern recommandé pour le scheduler

```csharp
// Exécuté périodiquement (ex : toutes les minutes)
public async Task ReveilllerInstancesEchuesAsync()
{
    using var connexion = _connectionFactory.Creer();
    using var transaction = connexion.BeginTransaction();

    var instancesEchues = await _serviceFlux.ObtenirInstancesEchuesAsync(
        dateReference: DateTime.UtcNow,
        transaction:   transaction);

    foreach (var instance in instancesEchues)
    {
        await _serviceFlux.ReprendreAttenteTempsAsync(instance.IdInstance, transaction);
    }

    transaction.Commit();
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
await _serviceFlux.ModifierVariableAsync(
    idInstance:  idInstance,
    nomVariable: "priorite",
    valeur:      "Haute",
    transaction: transaction);
```

---

## 12. Historique et audit

Chaque transition de l'instance génère automatiquement un événement d'audit.

```csharp
var historique = await _serviceFlux.ObtenirHistoriqueAsync(idInstance, transaction);

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
| `MigrationInstance` | Migration vers une nouvelle version                   |

---

## 13. Migration de version

La migration permet de faire passer des instances actives vers une nouvelle version publiée d'une définition, sans interruption.

```csharp
// Migrer une seule instance
var resultat = await _serviceMigration.MigrerAsync(
    idInstance:    idInstance,
    versionCible:  2,
    transaction:   transaction);

// Migrer toutes les instances actives/suspendues d'une définition
var resultats = await _serviceMigration.MigrerToutesAsync(
    cleDefinition: "approbation-commande",
    versionCible:  2,
    transaction:   transaction);
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
    transaction:   transaction,
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
using var connexion = _connectionFactory.Creer();
using var transaction = connexion.BeginTransaction();
try
{
    await _serviceFlux.DemarrerAsync("approbation-commande", aggregateId, variables, transaction);
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
| `NoeudMetier`         | Exécute un `IHandlerCommande`                           | Non       |
| `NoeudInteractif`     | Crée une tâche humaine via `IGestionTache`              | Oui       |
| `NoeudDecision`       | Branchement XOR selon conditions                        | Non       |
| `NoeudAttenteTemps`   | Suspend jusqu'à une date calculée                       | Oui       |
| `NoeudAttenteSignal`  | Suspend jusqu'à la réception d'un signal nommé          | Oui       |
| `NoeudSousProcessus`  | Exécute un processus enfant dans la même transaction    | Si enfant suspend |

### Sources de paramètres par contexte

**`NoeudMetier` — aggregate id :**

| Méthode | Description |
|---------|-------------|
| `.CommandeNommee("X", "varId")` | Raccourci : commande + aggregate depuis une variable |
| `.AggregateIdDepuisVariable("varId")` | Aggregate id depuis une variable du processus |
| `.AggregateIdStatique(42L)` | Aggregate id fixe |

**`NoeudMetier` — paramètres additionnels :**

| Méthode | Description |
|---------|-------------|
| `.ParametreDepuisVariable("nomParam", "nomVar")` | Paramètre résolu depuis une variable |
| `.ParametreStatique("nomParam", valeur)` | Paramètre à valeur fixe |

**`DefinitionCommande` (PRE/POST d'un `NoeudInteractif`) — via `CommandeBuilder` :**

| Méthode | Description |
|---------|-------------|
| `.AvecCommandePre("X", "varId")` | Raccourci aggregate id depuis une variable |
| `.AvecCommandePre("X", p => p.ParametreDepuisVariable("n", "v"))` | Paramètre depuis variable |
| `.AvecCommandePre("X", p => p.ParametreStatique("n", val))` | Paramètre statique |
| *(idem pour `.AvecCommandePost`)* | |

**`NoeudAttenteTemps` — date d'échéance :**

| Méthode | Description |
|---------|-------------|
| `.EcheanceDepuisVariable("nomVar")` | Date lue depuis une variable (`DateTime`) |
| `.EcheanceStatique(date)` | Date fixe dans la définition |
| `.EcheanceDepuisQuery("NomQuery")` | Date calculée par un `IHandlerQuery<DateTime>` |
| `.EcheanceDepuisQuery("NomQuery", "varId")` | Idem avec aggregate id depuis une variable |
| `.ParametreQueryDepuisVariable("nomParam", "nomVar")` | Paramètre query depuis une variable |
| `.ParametreQueryStatique("nomParam", valeur)` | Paramètre query à valeur fixe |

**`NoeudDecision` — conditions :**

| Méthode | Description |
|---------|-------------|
| `.SiVariable("nomVar", Operateur.X, valeur)` | Condition sur une variable du processus |
| `.SiQuery("NomQuery")` | Condition évaluée par un `IHandlerQuery<bool>` |
| `.SiQuery("NomQuery", "varId")` | Idem avec aggregate id depuis une variable |
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
