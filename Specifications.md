# BpmPlus — Spécification technique

> Document de référence destiné à l’implémentation du moteur BPM en tant que NuGet.  
> Version : 1.0 — Statut : Approuvé pour implémentation

-----

## Table des matières

1. [Contexte et objectifs](#1-contexte-et-objectifs)
1. [Structure de la solution](#2-structure-de-la-solution)
1. [Décisions d’architecture](#3-décisions-darchitecture)
1. [Modèle de données](#4-modèle-de-données)
1. [Nœuds du moteur](#5-nœuds-du-moteur)
1. [Définition des processus](#6-définition-des-processus)
1. [Interfaces publiques — Abstractions cliente](#7-interfaces-publiques--abstractions-cliente)
1. [Interfaces publiques — Services BPM](#8-interfaces-publiques--services-bpm)
1. [Comportements transversaux](#9-comportements-transversaux)
1. [Injection de dépendances (Autofac)](#10-injection-de-dépendances-autofac)
1. [Migration d’instances](#11-migration-dinstances)
1. [Historique et logs](#12-historique-et-logs)
1. [Scénarios de tests attendus](#13-scénarios-de-tests-attendus)
1. [Hors périmètre](#14-hors-périmètre)

-----

## 1. Contexte et objectifs

### 1.1 But

`BpmPlus` est un moteur BPM (Business Process Management) distribué sous forme de NuGet. Il permet à une application cliente de définir, exécuter et superviser des processus métier structurés, rattachés à des agrégats du domaine.

Le moteur s’exécute **en mémoire** pour enchaîner les étapes dans une même transaction de base de données, sauf pour les nœuds qui suspendent le processus (nœud interactif, nœud d’attente).

### 1.2 Principes directeurs

- Le moteur est **agnostique au domaine** : toute la logique métier est déléguée à l’application cliente via des handlers.
- La **transaction est fournie par l’application cliente** : le moteur ne crée pas de connexion, il reçoit un `IDbTransaction` actif.
- Les **définitions de processus** sont versionnées et conservées en base de données.
- Les **instances de processus** sont persistées et liées à un agrégat métier identifié par un `long`.
- Le code suit la convention de nommage : **français pour les concepts BPM métier, anglais pour les patrons techniques**.

### 1.3 Environnements de persistance

|Environnement|Usage              |
|-------------|-------------------|
|Oracle       |Production         |
|SQLite       |Tests d’intégration|

-----

## 2. Structure de la solution

```
BpmPlus/
├── src/
│   ├── BpmPlus.Abstractions/          ← Interfaces à implémenter côté application cliente
│   ├── BpmPlus.Core/                  ← Moteur BPM : modèle, exécution, définitions
│   │   ├── Modele/                     ← Entités : InstanceProcessus, NoeudProcessus, etc.
│   │   ├── Execution/                  ← Moteur d'exécution des nœuds
│   │   ├── Definition/                 ← Modèle de définition + Fluent Builder
│   │   ├── Services/                   ← IServiceBpm, IServiceMigration (implémentations)
│   │   └── Persistance/                ← Interfaces Repository
│   ├── BpmPlus.Persistance.Oracle/    ← Implémentation Oracle des repositories
│   ├── BpmPlus.Persistance.Sqlite/    ← Implémentation SQLite des repositories
│   └── BpmPlus.Registration/          ← Module Autofac + configuration
└── tests/
    ├── BpmPlus.UnitTests/             ← Tests unitaires du moteur d'exécution
    └── BpmPlus.IntegrationTests/      ← Tests bout-en-bout sur SQLite en mémoire
```

### 2.1 Responsabilités par projet

**`BpmPlus.Abstractions`**  
Contient uniquement les interfaces et types que l’application cliente doit référencer pour implémenter ses handlers et interagir avec le moteur. Pas de dépendances externes hormis `Microsoft.Extensions.Logging.Abstractions`.

**`BpmPlus.Core`**  
Cœur du moteur. Contient le modèle de domaine BPM, la logique d’exécution des nœuds, les builders de définition (Fluent et JSON), et les interfaces de persistance (Repository). N’a aucune dépendance vers Oracle ou SQLite.

**`BpmPlus.Persistance.Oracle` / `BpmPlus.Persistance.Sqlite`**  
Implémentations concrètes des repositories. Chaque implémentation gère la création des tables (avec préfixe configurable), les requêtes SQL et la sérialisation des entités.

**`BpmPlus.Registration`**  
Module Autofac qui orchestre l’enregistrement de tous les composants du moteur et des handlers découverts dynamiquement dans l’assembly cliente.

-----

## 3. Décisions d’architecture

|Sujet                      |Décision                                                                                        |
|---------------------------|------------------------------------------------------------------------------------------------|
|Framework DI               |Autofac                                                                                         |
|Définition de processus    |Fluent C# + JSON maison (les deux supportés)                                                    |
|Définitions                |Brouillon modifiable → publication explicite → immuable                                         |
|Préfixe tables             |Configurable à l’enregistrement du module                                                       |
|Transaction                |`IDbTransaction` injecté par l’application cliente                                              |
|IDs d’instance             |`long` — séquence Oracle/SQLite (auto-increment), **conservé lors d’une migration**             |
|`AggregateId`              |Colonne dédiée indexée sur la table des instances                                               |
|Agrégat ↔ instance         |1 seul processus actif par clé de définition par agrégat                                        |
|Nœud de début              |Toujours un seul point d’entrée par définition                                                  |
|Nœud de fin                |Tag `EstFinale` sur le nœud — plusieurs fins possibles, pas de nœud de fin séparé               |
|Nœud de décision           |XOR seulement (une seule branche évaluée)                                                       |
|Nœud sous-processus        |Référencé par clé + version d’une définition publiée, même transaction que le parent            |
|Variables du sous-processus|Héritage automatique de toutes les variables du parent + sorties explicites remontées au parent |
|Nœud interactif PRE        |Même transaction que la création de la tâche                                                    |
|Nœud interactif POST       |Même transaction que la fermeture de la tâche                                                   |
|Nœud attente de temps      |Réveil entièrement géré par l’application cliente via scheduler externe                         |
|Variables                  |Scalaires uniquement : `string`, `int`, `decimal`, `DateTime`, `bool`                           |
|Signal                     |Ciblé (une instance) ou broadcast (toutes instances en attente de ce signal)                    |
|Erreur handler             |Rollback complet + exception rethrowée à l’appelant                                             |
|Statuts d’instance         |`Active`, `Suspendue`, `EnErreur`, `Terminee`                                                   |
|Migration                  |Manuelle via `IServiceMigration`, mise à jour en place (ID conservé), mapping optionnel de nœuds|
|Historique                 |Table d’événements d’instance (nœud, horodatage, durée, résultat)                               |
|Concurrence                |Confiance à l’appelant (pas de verrou optimiste)                                                |
|Logs                       |Via `ILogger<T>` (Microsoft.Extensions.Logging)                                                 |
|Nommage                    |Français pour les concepts BPM métier, anglais pour les patrons techniques                      |

-----

## 4. Modèle de données

Toutes les tables sont préfixées par la valeur fournie à la configuration (ex: préfixe `"BPM"` → table `BPM_INSTANCE_PROCESSUS`).

### 4.1 Table : `{PREFIX}_DEFINITION_PROCESSUS`

Stocke les définitions de processus versionnées.

|Colonne           |Type                      |Description                                                       |
|------------------|--------------------------|------------------------------------------------------------------|
|`ID`              |`BIGINT` PK auto-increment|Identifiant unique                                                |
|`CLE`             |`VARCHAR(100)` NOT NULL   |Identifiant métier de la définition (ex: `"approbation-commande"`)|
|`VERSION`         |`INT` NOT NULL            |Numéro de version (commence à 1, incrémenté à chaque publication) |
|`NOM`             |`VARCHAR(200)` NOT NULL   |Nom lisible                                                       |
|`STATUT`          |`VARCHAR(20)` NOT NULL    |`Brouillon` ou `Publiee`                                          |
|`DEFINITION_JSON` |`CLOB` NOT NULL           |Sérialisation JSON complète de la définition                      |
|`DATE_CREATION`   |`TIMESTAMP` NOT NULL      |Date de création                                                  |
|`DATE_PUBLICATION`|`TIMESTAMP` NULL          |Date de publication (null si brouillon)                           |

**Contrainte unique :** `(CLE, VERSION)`

-----

### 4.2 Table : `{PREFIX}_INSTANCE_PROCESSUS`

Stocke les instances actives et terminées.

|Colonne             |Type                      |Description                                                           |
|--------------------|--------------------------|----------------------------------------------------------------------|
|`ID`                |`BIGINT` PK auto-increment|Identifiant de l’instance — conservé lors d’une migration             |
|`CLE_DEFINITION`    |`VARCHAR(100)` NOT NULL   |Clé de la définition associée                                         |
|`VERSION_DEFINITION`|`INT` NOT NULL            |Version de la définition au moment de la création (ou après migration)|
|`AGGREGATE_ID`      |`BIGINT` NOT NULL INDEX   |ID de l’agrégat métier rattaché                                       |
|`STATUT`            |`VARCHAR(20)` NOT NULL    |`Active`, `Suspendue`, `EnErreur`, `Terminee`                         |
|`ID_NOEUD_COURANT`  |`VARCHAR(100)` NULL       |ID du nœud où l’instance est suspendue (null si active ou terminée)   |
|`ID_INSTANCE_PARENT`|`BIGINT` NULL FK          |Référence vers l’instance parente (si sous-processus)                 |
|`DATE_DEBUT`        |`TIMESTAMP` NOT NULL      |Date de démarrage                                                     |
|`DATE_FIN`          |`TIMESTAMP` NULL          |Date de fin (null si en cours)                                        |
|`DATE_CREATION`     |`TIMESTAMP` NOT NULL      |Date de création en base                                              |
|`DATE_MAJ`          |`TIMESTAMP` NOT NULL      |Date de dernière modification                                         |

**Contrainte unique :** `(CLE_DEFINITION, AGGREGATE_ID)` où `STATUT != 'Terminee'`  
→ Un seul processus actif par clé de définition par agrégat.

-----

### 4.3 Table : `{PREFIX}_VARIABLE_PROCESSUS`

Stocke les variables scalaires d’une instance.

|Colonne      |Type                      |Description                                   |
|-------------|--------------------------|----------------------------------------------|
|`ID`         |`BIGINT` PK auto-increment|Identifiant                                   |
|`ID_INSTANCE`|`BIGINT` NOT NULL FK      |Instance propriétaire                         |
|`NOM`        |`VARCHAR(200)` NOT NULL   |Nom de la variable                            |
|`TYPE`       |`VARCHAR(20)` NOT NULL    |`String`, `Int`, `Decimal`, `DateTime`, `Bool`|
|`VALEUR`     |`VARCHAR(2000)` NOT NULL  |Valeur sérialisée en string                   |

**Contrainte unique :** `(ID_INSTANCE, NOM)`

-----

### 4.4 Table : `{PREFIX}_EVENEMENT_INSTANCE`

Historique des transitions et événements d’une instance.

|Colonne         |Type                      |Description                                                  |
|----------------|--------------------------|-------------------------------------------------------------|
|`ID`            |`BIGINT` PK auto-increment|Identifiant                                                  |
|`ID_INSTANCE`   |`BIGINT` NOT NULL FK      |Instance concernée                                           |
|`TYPE_EVENEMENT`|`VARCHAR(50)` NOT NULL    |Voir types ci-dessous                                        |
|`ID_NOEUD`      |`VARCHAR(100)` NULL       |Nœud concerné                                                |
|`NOM_NOEUD`     |`VARCHAR(200)` NULL       |Nom lisible du nœud                                          |
|`HORODATAGE`    |`TIMESTAMP` NOT NULL      |Date et heure de l’événement                                 |
|`DUREE_MS`      |`BIGINT` NULL             |Durée d’exécution en millisecondes                           |
|`RESULTAT`      |`VARCHAR(20)` NULL        |`Succes`, `Erreur`, `Suspendu`                               |
|`DETAIL`        |`CLOB` NULL               |Détails additionnels (ex: message d’erreur, ID tâche externe)|

**Types d’événements :**

- `DebutProcessus` — instance créée et démarrée
- `EntreeNoeud` — moteur entre dans un nœud
- `SortieNoeud` — moteur quitte un nœud avec succès
- `NoeudSuspendu` — nœud interactif ou attente, processus suspendu
- `NoeudRepris` — reprise après suspension
- `ErreurNoeud` — exception levée lors de l’exécution d’un nœud
- `FinProcessus` — instance terminée (avec ID du nœud de fin)
- `MigrationInstance` — migration vers une nouvelle version (détail : ancienne version, ancien nœud, nouveau nœud)
- `SignalRecu` — signal reçu pour débloquer une attente
- `VariableModifiee` — modification manuelle d’une variable via `IServiceBpm`
- `TacheAssignee` — affectation manuelle d’un logon via `AssignerLogonAsync` (Detail = logon)

-----

### 4.5 Table : `{PREFIX}_ATTENTE_SIGNAL`

Registre des instances en attente d’un signal nommé.

|Colonne        |Type                         |Description                       |
|---------------|-----------------------------|----------------------------------|
|`ID`           |`BIGINT` PK auto-increment   |Identifiant                       |
|`ID_INSTANCE`  |`BIGINT` NOT NULL FK         |Instance en attente               |
|`NOM_SIGNAL`   |`VARCHAR(200)` NOT NULL INDEX|Nom du signal attendu             |
|`DATE_CREATION`|`TIMESTAMP` NOT NULL         |Date d’enregistrement de l’attente|

-----

## 5. Nœuds du moteur

### 5.1 Nœud Métier (`NoeudMetier`)

**Rôle :** Exécute une commande métier via un `IBpmHandlerCommande` résolu dynamiquement par `NomCommande`.

**Cycle de vie :**

1. Le moteur résout le handler correspondant à `NomCommande` depuis le conteneur Autofac.
1. Il résout chaque paramètre depuis les variables du processus ou des valeurs statiques définies dans le nœud.
1. Il appelle `ExecuterAsync(contexte.AggregateId, parametres, contexte)`.
1. En cas de succès, il passe au nœud suivant dans la même transaction.
1. En cas d’exception, rollback complet + rethrow.

**Propriétés de définition :**

```
NomCommande         : string                                 // Nom du handler à invoquer
                                                             // Défaut : PascalCase(id) + "Command"
Parametres          : Dictionary<string, ISourceParametre>  // Paramètres additionnels
```

**L’aggregate id** est toujours `IContexteExecution.AggregateId` — il n’est pas défini dans le nœud.

**Résolution de paramètres (`ISourceParametre`) :**

- `SourceVariable(nomVariable)` → lit la valeur depuis les variables du processus
- `SourceValeurStatique(valeur)` → valeur fixe définie dans la définition du nœud

-----

### 5.2 Nœud Interactif (`NoeudInteractif`)

**Rôle :** Suspend le processus et crée une tâche dans le système externe de gestion de tâches de l’application cliente.

**Cycle de vie — Arrivée au nœud (même transaction) :**

1. Si `CommandePre` est définie → exécution de la commande PRE (même mécanique que `NoeudMetier`).
1. Appel à `IGestionTache.CreerTacheAsync(definitionTache, instance)` → retourne un `idTacheExterne` (`long`).
1. Si `DefinitionTache.LogonAuto` est renseigné → appel immédiat à `IGestionTache.AssignerTacheAsync(idTacheExterne, logonAuto)`.
1. L’`idTacheExterne` (et le `logon` si défini) sont enregistrés dans le détail JSON de l’événement `NoeudSuspendu`.
1. L’instance passe au statut `Suspendue`, `IdNoeudCourant` = ID du nœud interactif.
1. Transaction committée par l’appelant.

**Cycle de vie — Complétion de la tâche (`TerminerEtapeAsync`, même transaction) :**

1. Vérification que l’instance est bien suspendue sur ce nœud.
1. Si `CommandePost` est définie → exécution de la commande POST.
1. Appel à `IGestionTache.FermerTacheAsync(idTacheExterne)`.
1. L’instance reprend, passe au nœud suivant.

**Propriétés de définition :**

```
DefinitionTache       : DefinitionTache             // Métadonnées transmises à IGestionTache lors de la création
  ├ Titre             : string                      // Titre de la tâche
  ├ Description       : string?                     // Description détaillée
  ├ Categorie         : string?                     // Catégorie libre
  ├ LogonAuto         : string?                     // Logon assigné automatiquement à la création de la tâche
  ├ CodeRole          : string?                     // Code du rôle requis (ex. "RESPONSABLE", "VALIDATEUR")
  ├ CodeTache         : string?                     // Code identifiant le type de tâche dans le système externe
  ├ NomNoeud          : string?                     // Renseigné automatiquement par le moteur (nom ou id du nœud)
  ├ IndTacheRevision  : bool                        // true si la tâche est une révision d'un élément existant
  └ LogonAuteur       : string?                     // Logon de l'auteur de l'élément soumis à la tâche
CommandePre           : DefinitionCommande?         // Commande optionnelle avant suspension
CommandePost          : DefinitionCommande?         // Commande optionnelle à la complétion
```

> **`NomNoeud` automatique :** le moteur renseigne `DefinitionTache.NomNoeud` lors du `Build()` du builder.
> La valeur est le `Nom` du nœud si non vide, sinon son `Id`.

-----

### 5.3 Nœud Décision (`NoeudDecision`)

**Rôle :** Dirige le flux vers une seule branche (XOR) en évaluant des conditions.

**Cycle de vie :**

1. Le moteur évalue les conditions des flux sortants dans l’ordre de leur définition.
1. La **première condition vraie** détermine le nœud suivant.
1. Un flux peut être marqué `EstParDefaut` pour servir de branche de repli si aucune condition n’est vraie.
1. Si aucune condition n’est vraie et qu’il n’y a pas de branche par défaut → exception.

**Types de conditions :**

- `ConditionVariable(nomVariable, operateur, valeur)` → évalue une variable du processus  
  Opérateurs : `Egal`, `Different`, `Superieur`, `Inferieur`, `SuperieurOuEgal`, `InferieurOuEgal`, `Contient`
- `ConditionQuery(nomQuery, parametres)` → appelle un `IBpmHandlerQuery<bool>` résolu par `NomQuery` ; l'aggregate id vient de `contexte.AggregateId`

**Propriétés de définition :**

```
// Définies sur chaque FluxSortant :
Condition           : ICondition?                   // null = branche par défaut
EstParDefaut        : bool
```

-----

### 5.4 Nœud Attente Temps (`NoeudAttenteTemps`)

**Rôle :** Suspend le processus jusqu’à une date calculée dynamiquement.

**Cycle de vie — Arrivée :**

1. La date d’échéance est résolue depuis une `ISourceParametre` (variable du processus ou appel à un `IBpmHandlerQuery<DateTime>`) ; l’aggregate id vient de `contexte.AggregateId`.
1. La date est stockée dans le détail de l’événement `NoeudSuspendu`.
1. L’instance passe au statut `Suspendue`.

**Réveil :**  
L’application cliente est entièrement responsable du réveil. Le moteur expose `ObtenirInstancesEchuesAsync(dateReference)` qui retourne les instances dont la date d’échéance est dépassée. L’app cliente appelle ensuite `ReprendreAttenteTempsAsync(idInstance)`.

**Propriétés de définition :**

```
SourceDateEcheance  : ISourceParametre              // Variable ou Query retournant un DateTime
```

-----

### 5.5 Nœud Attente Signal (`NoeudAttenteSignal`)

**Rôle :** Suspend le processus jusqu’à la réception d’un signal nommé.

**Cycle de vie — Arrivée :**

1. Une entrée est créée dans `{PREFIX}_ATTENTE_SIGNAL` avec `NomSignal`.
1. L’instance passe au statut `Suspendue`.

**Réveil via `EnvoyerSignalAsync` :**

- **Ciblé** : signal envoyé à une instance précise (`idInstance` + `nomSignal`).
- **Broadcast** : signal envoyé à toutes les instances en attente de ce nom de signal (sans `idInstance`).
- À la réception, l’entrée dans `ATTENTE_SIGNAL` est supprimée et l’instance reprend.

**Propriétés de définition :**

```
NomSignal           : string                        // Nom du signal attendu
```

-----

### 5.6 Nœud Sous-Processus (`NoeudSousProcessus`)

**Rôle :** Exécute un processus enfant défini indépendamment, dans la même transaction que le parent.

**Cycle de vie :**

1. Le moteur charge la définition publiée référencée par `CleDefinition` + `Version`.
1. Une nouvelle `InstanceProcessus` enfant est créée avec `IdInstanceParent` = ID de l’instance parente.
1. L’enfant hérite automatiquement de **toutes** les variables du parent (copie au démarrage).
1. Le sous-processus s’exécute dans la même transaction que le parent.
1. Si le sous-processus atteint un nœud suspensif (interactif, attente), le parent est aussi suspendu.
1. À la fin du sous-processus, les variables marquées comme **sorties** dans la définition du nœud sont remontées et écrasent les variables du parent.
1. Le parent reprend au nœud suivant.

**Propriétés de définition :**

```
CleDefinition       : string                        // Clé de la définition enfant
Version             : int                           // Version publiée à utiliser
VariablesSorties    : List<string>                  // Noms des variables à remonter au parent
```

-----

### 5.7 Nœud de début et nœud de fin

**Début :** Chaque définition possède exactement **un** nœud de début implicite. C’est le premier nœud référencé dans la définition (Fluent : premier appel à `Debuter()`; JSON : propriété `"noeudDebut"`).

**Fin :** Pas de nœud de fin séparé. Tout nœud peut être marqué `EstFinale = true`. Lorsque le moteur arrive sur un nœud `EstFinale`, il termine l’instance (statut `Terminee`, `DateFin` renseignée) après l’exécution du nœud. Plusieurs fins possibles dans un même processus.

-----

## 6. Définition des processus

### 6.1 Approche Fluent (C#)

```csharp
// Clé + nom + nœud de début en une seule instruction
var definition = new DefinitionProcessusBuilder(
        "approbation-commande",
        "Processus d'approbation de commande",
        "valider-commande")

    // Ultra-compact : id → "ValiderCommandeCommand", aggregate depuis l'instance
    .AjouterNoeudMetier("valider-commande", "Valider la commande", vers: "approbation-responsable")

    // Nœud interactif
    .AjouterNoeudInteractif("approbation-responsable", "Approbation responsable", n => n
        .DefinirTache("Approuver la commande", "Veuillez approuver ou refuser la commande")
        .AvecCommandePre("MarquerEnAttenteApprobation")
        .AvecCommandePost("MarquerApprouvee")
        .Vers("decision-approbation"))

    // Nœud décision
    .AjouterNoeudDecision("decision-approbation", "Décision d'approbation", n => n
        .SiEgal("statutApprobation", "Approuvee").Vers("notification-approbation")
        .ParDefaut().Vers("notification-refus"))

    // Fins — ultra-compact (finale implicite)
    .AjouterNoeudMetier("notification-approbation", "Notifier approbation")
    .AjouterNoeudMetier("notification-refus", "Notifier refus")

    .Construire();
```

-----

### 6.2 Approche JSON

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
      "commandePre": {
        "nomCommande": "MarquerEnAttenteApprobation"
      },
      "commandePost": {
        "nomCommande": "MarquerApprouvee"
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
            "nomVariable": "statutApprobation",
            "operateur": "Egal",
            "valeur": "Approuvee"
          }
        },
        {
          "vers": "notification-refus",
          "estParDefaut": true
        }
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

-----

## 7. Interfaces publiques — Abstractions cliente

Ces interfaces sont définies dans `BpmPlus.Abstractions` et doivent être implémentées par l’application cliente.

### 7.1 `IBpmHandlerCommande`

> Le préfixe `Bpm` évite les conflits avec les interfaces `IHandlerCommande` ou `ICommandHandler` que l'application cliente peut déjà exposer pour ses propres besoins CQRS.

```csharp
namespace BpmPlus.Abstractions;

/// <summary>
/// Commande métier exécutable par le moteur BPM lors du traitement d'un NoeudMetier.
/// Chaque implémentation est découverte automatiquement par son NomCommande.
/// Convention : NomCommande = PascalCase(id du nœud) + "Command".
/// </summary>
public interface IBpmHandlerCommande
{
    /// <summary>
    /// Identifiant unique de la commande. Doit correspondre au NomCommande
    /// défini dans le NoeudMetier ou la CommandePre/Post d'un NoeudInteractif.
    /// </summary>
    string NomCommande { get; }

    /// <param name="aggregateId">
    /// ID de l'agrégat métier de l'instance — fourni automatiquement par le moteur
    /// depuis IContexteExecution.AggregateId.
    /// </param>
    /// <param name="parametres">
    /// Paramètres résolus par le moteur depuis les variables du processus
    /// ou les valeurs statiques définies dans le nœud.
    /// </param>
    /// <param name="contexte">
    /// Contexte d'exécution donnant accès à la transaction, aux variables
    /// et aux informations de l'instance en cours.
    /// </param>
    Task ExecuterAsync(
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte);
}
```

-----

### 7.2 `IBpmHandlerQuery<TResultat>`

> Le préfixe `Bpm` évite les conflits avec les interfaces `IHandlerQuery` que l'application cliente peut déjà exposer.

```csharp
namespace BpmPlus.Abstractions;

/// <summary>
/// Query exécutable par le moteur BPM pour prendre une décision (NoeudDecision)
/// ou résoudre une date d'échéance (NoeudAttenteTemps).
/// L'aggregate id est fourni automatiquement depuis IContexteExecution.AggregateId.
/// </summary>
public interface IBpmHandlerQuery<TResultat> : IBpmHandlerQuery
{
    /// <summary>
    /// Identifiant unique de la query. Doit correspondre au NomQuery
    /// défini dans une ConditionQuery ou une SourceQuery.
    /// </summary>
    string NomQuery { get; }

    Task<TResultat> ExecuterAsync(
        long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres,
        IContexteExecution contexte);
}
```

-----

### 7.3 `IGestionTache`

```csharp
namespace BpmPlus.Abstractions;

/// <summary>
/// Gestionnaire de tâches humaines. Fait le lien entre le moteur BPM
/// et le système externe de gestion de tâches de l'application cliente.
/// Implémenté côté application cliente et enregistré via UseGestionTache&lt;T&gt;().
/// </summary>
public interface IGestionTache
{
    /// <summary>
    /// Crée une tâche dans le système externe lors de l'arrivée sur un NoeudInteractif.
    /// Appelé dans la même transaction que la suspension de l'instance.
    /// </summary>
    /// <returns>Identifiant externe de la tâche créée (conservé dans l'historique).</returns>
    Task<long> CreerTacheAsync(
        DefinitionTache definitionTache,
        InstanceProcessus instance,
        CancellationToken ct = default);

    /// <summary>
    /// Ferme la tâche externe lors de la complétion d'un NoeudInteractif.
    /// Appelé dans la même transaction que la reprise de l'instance.
    /// </summary>
    Task FermerTacheAsync(long idTacheExterne, CancellationToken ct = default);

    /// <summary>
    /// Assigne la tâche à un utilisateur ou groupe (logon).
    /// </summary>
    Task AssignerTacheAsync(long idTacheExterne, string logon, CancellationToken ct = default);
}
```

-----

### 7.4 `IContexteExecution`

```csharp
namespace BpmPlus.Abstractions;

/// <summary>
/// Contexte fourni à chaque handler lors de l'exécution d'un nœud.
/// Donne accès à la transaction active, aux variables et aux informations de l'instance.
/// </summary>
public interface IContexteExecution
{
    /// <summary>ID de l'instance de processus en cours d'exécution.</summary>
    long IdInstance { get; }

    /// <summary>Clé de la définition de processus.</summary>
    string CleDefinition { get; }

    /// <summary>Version de la définition utilisée par cette instance.</summary>
    int VersionDefinition { get; }

    /// <summary>ID de l'agrégat métier rattaché à cette instance.</summary>
    long? AggregateId { get; }

    /// <summary>
    /// Transaction de base de données active. Fournie par l'application cliente.
    /// Le moteur et tous les handlers partagent cette transaction.
    /// </summary>
    IDbTransaction Transaction { get; }

    /// <summary>
    /// Accès en lecture et en écriture aux variables scalaires de l'instance.
    /// Les modifications sont persistées automatiquement par le moteur.
    /// </summary>
    IAccesseurVariables Variables { get; }

    /// <summary>Token d'annulation propagé depuis l'appelant.</summary>
    CancellationToken CancellationToken { get; }
}
```

-----

### 7.5 `IAccesseurVariables`

```csharp
namespace BpmPlus.Abstractions;

/// <summary>
/// Accès en lecture et en écriture aux variables scalaires d'une instance de processus.
/// Types supportés : string, int, decimal, DateTime, bool.
/// </summary>
public interface IAccesseurVariables
{
    /// <summary>Retourne la valeur typée de la variable. Lance une exception si absente.</summary>
    T Obtenir<T>(string nom);

    /// <summary>Retourne la valeur typée ou la valeur par défaut si absente.</summary>
    T? ObtenirOuDefaut<T>(string nom);

    /// <summary>Définit ou écrase une variable. La valeur doit être un scalaire supporté.</summary>
    void Definir(string nom, object? valeur);

    /// <summary>Indique si une variable existe.</summary>
    bool Existe(string nom);

    /// <summary>Retourne toutes les variables sous forme de dictionnaire.</summary>
    IReadOnlyDictionary<string, object?> ObtenirToutes();
}
```

-----

## 8. Interfaces publiques — Services BPM

Ces interfaces sont définies dans `BpmPlus.Abstractions` et sont implémentées par `BpmPlus.Core`.

### 8.1 `IServiceBpm`

```csharp
namespace BpmPlus.Abstractions;

/// <summary>
/// Point d'entrée principal pour interagir avec le moteur BPM.
/// La connexion de base de données est fournie via IDbConnection enregistré dans le conteneur IoC.
/// </summary>
public interface IServiceBpm
{
    // ── Instances ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Démarre une nouvelle instance de processus pour un agrégat donné.
    /// </summary>
    /// <param name="cleDefinition">Clé de la définition à instancier.</param>
    /// <param name="aggregateId">ID de l'agrégat métier rattaché.</param>
    /// <param name="variablesInitiales">Variables initiales du processus.</param>
    /// <param name="transaction">Transaction active de l'application cliente.</param>
    /// <returns>ID de la nouvelle instance créée.</returns>
    Task<long> DemarrerAsync(
        string cleDefinition,
        long aggregateId,
        IReadOnlyDictionary<string, object?>? variablesInitiales,
        IDbTransaction transaction,
        CancellationToken ct = default);

    /// <summary>Obtient une instance par son ID.</summary>
    Task<InstanceProcessus> ObtenirAsync(long idInstance, IDbTransaction transaction, CancellationToken ct = default);

    /// <summary>Obtient l'instance active d'un agrégat pour une définition donnée.</summary>
    Task<InstanceProcessus?> ObtenirParAggregateAsync(string cleDefinition, long aggregateId, IDbTransaction transaction, CancellationToken ct = default);

    /// <summary>Recherche des instances par valeur de variable.</summary>
    Task<IReadOnlyList<InstanceProcessus>> RechercherParVariableAsync(
        string nomVariable,
        object valeur,
        IDbTransaction transaction,
        CancellationToken ct = default);

    /// <summary>Obtient les instances enfants d'une instance parente (sous-processus).</summary>
    Task<IReadOnlyList<InstanceProcessus>> ObtenirEnfantsAsync(long idInstanceParent, IDbTransaction transaction, CancellationToken ct = default);

    // ── Étapes et reprise ─────────────────────────────────────────────────────

    /// <summary>
    /// Termine l'étape courante d'un nœud interactif suspendu et passe au nœud suivant.
    /// Exécute la CommandePost si définie, ferme la tâche externe, reprend l'exécution.
    /// </summary>
    Task TerminerEtapeAsync(long idInstance, IDbTransaction transaction, CancellationToken ct = default);

    /// <summary>
    /// Envoie un signal pour débloquer une ou plusieurs instances en attente.
    /// Si idInstance est fourni : signal ciblé. Sinon : broadcast sur nomSignal.
    /// </summary>
    Task EnvoyerSignalAsync(
        string nomSignal,
        IDbTransaction transaction,
        long? idInstance = null,
        CancellationToken ct = default);

    /// <summary>
    /// Reprend une instance suspendue sur un nœud d'attente de temps dont l'échéance est passée.
    /// Appelé par le scheduler de l'application cliente.
    /// </summary>
    Task ReprendreAttenteTempsAsync(long idInstance, IDbTransaction transaction, CancellationToken ct = default);

    /// <summary>
    /// Retourne les instances dont la date d'échéance de l'attente temps est dépassée.
    /// Utilisé par le scheduler de l'application cliente pour déclencher ReprendreAttenteTempsAsync.
    /// </summary>
    Task<IReadOnlyList<InstanceEchue>> ObtenirInstancesEchuesAsync(
        DateTime dateReference,
        IDbTransaction transaction,
        CancellationToken ct = default);

    // ── Variables ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Modifie la valeur d'une variable d'une instance suspendue.
    /// Un événement VariableModifiee est ajouté à l'historique.
    /// </summary>
    Task ModifierVariableAsync(
        long idInstance,
        string nomVariable,
        object? valeur,
        IDbTransaction transaction,
        CancellationToken ct = default);

    // ── Signaux en attente ────────────────────────────────────────────────────

    /// <summary>Retourne la liste des noms de signaux attendus par une instance.</summary>
    Task<IReadOnlyList<string>> ObtenirSignauxEnAttenteAsync(long idInstance, IDbTransaction transaction, CancellationToken ct = default);

    // ── Définitions ───────────────────────────────────────────────────────────

    /// <summary>
    /// Sauvegarde une définition de processus en tant que brouillon.
    /// Si une version brouillon existe déjà pour cette clé, elle est remplacée.
    /// </summary>
    Task SauvegarderDefinitionAsync(DefinitionProcessus definition, IDbTransaction transaction, CancellationToken ct = default);

    /// <summary>
    /// Publie la version brouillon d'une définition.
    /// Une définition publiée est immuable.
    /// </summary>
    Task PublierDefinitionAsync(string cleDefinition, IDbTransaction transaction, CancellationToken ct = default);

    /// <summary>Retourne toutes les définitions (toutes versions, tous statuts).</summary>
    Task<IReadOnlyList<DefinitionProcessus>> ObtenirDefinitionsAsync(IDbTransaction transaction, CancellationToken ct = default);

    // ── Tâches ────────────────────────────────────────────────────────────────

    /// <summary>Retourne l'identifiant externe de la tâche active d'une instance suspendue.</summary>
    Task<long?> ObtenirIdTacheActiveAsync(long idInstance, CancellationToken ct = default);

    /// <summary>
    /// Retourne le logon actif de la tâche : dernière affectation manuelle (TacheAssignee)
    /// si postérieure à la suspension, sinon LogonAuto de la définition, sinon null.
    /// </summary>
    Task<string?> ObtenirLogonTacheActiveAsync(long idInstance, CancellationToken ct = default);

    /// <summary>
    /// Assigne un logon à la tâche active d'une instance suspendue.
    /// Appelle IGestionTache.AssignerTacheAsync et enregistre un événement TacheAssignee.
    /// </summary>
    Task AssignerLogonAsync(long idInstance, string logon, CancellationToken ct = default);

    // ── Historique ────────────────────────────────────────────────────────────

    /// <summary>Retourne l'historique des événements d'une instance.</summary>
    Task<IReadOnlyList<EvenementInstance>> ObtenirHistoriqueAsync(long idInstance, CancellationToken ct = default);
}
```

-----

### 8.2 `IServiceMigration`

```csharp
namespace BpmPlus.Abstractions;

/// <summary>
/// Service de migration manuelle d'instances vers une nouvelle version de définition.
/// La migration met à jour l'instance en place (l'ID est conservé).
/// Un événement MigrationInstance est ajouté à l'historique.
/// </summary>
public interface IServiceMigration
{
    /// <summary>
    /// Migre une instance vers la version cible de sa définition.
    /// </summary>
    /// <param name="idInstance">ID de l'instance à migrer.</param>
    /// <param name="versionCible">Numéro de version publiée vers laquelle migrer.</param>
    /// <param name="mappingNoeuds">
    /// Mapping optionnel { ancienIdNoeud → nouvelIdNoeud }.
    /// Requis si le nœud courant de l'instance n'existe plus dans la version cible.
    /// Si absent et que le nœud courant n'existe plus, la migration échoue.
    /// </param>
    /// <param name="transaction">Transaction active de l'application cliente.</param>
    /// <returns>Résultat de la migration avec le détail des changements appliqués.</returns>
    Task<ResultatMigration> MigrerAsync(
        long idInstance,
        int versionCible,
        IDbTransaction transaction,
        IReadOnlyDictionary<string, string>? mappingNoeuds = null,
        CancellationToken ct = default);

    /// <summary>
    /// Migre toutes les instances actives ou suspendues d'une définition vers une version cible.
    /// </summary>
    Task<IReadOnlyList<ResultatMigration>> MigrerToutesAsync(
        string cleDefinition,
        int versionCible,
        IDbTransaction transaction,
        IReadOnlyDictionary<string, string>? mappingNoeuds = null,
        CancellationToken ct = default);
}
```

-----

## 9. Comportements transversaux

### 9.1 Gestion des transactions

- Le moteur **ne crée jamais** de connexion ni de transaction.
- Chaque méthode de `IServiceBpm` et `IServiceMigration` reçoit un `IDbTransaction` actif.
- Toutes les opérations du moteur (exécution des nœuds, persistance des variables, écriture de l’historique) utilisent cette transaction.
- Les handlers de l’application cliente (`IBpmHandlerCommande`, `IBpmHandlerQuery`) reçoivent la même transaction via `IContexteExecution.Transaction`.
- **L’appelant est responsable du commit ou du rollback.** Le moteur ne committe jamais.
- En cas d’exception dans un handler, le moteur la rethrow sans la capturer — c’est à l’appelant de rollbacker.

### 9.2 Gestion des erreurs

|Situation                                                       |Comportement                                        |
|----------------------------------------------------------------|----------------------------------------------------|
|Exception dans `IBpmHandlerCommande`                            |Rethrow immédiat, rollback à la charge de l’appelant|
|Exception dans `IBpmHandlerQuery`                               |Rethrow immédiat, rollback à la charge de l’appelant|
|Nœud courant introuvable dans la définition                     |`NoeudIntrouvableException`                         |
|Aucune condition vraie sans branche par défaut                  |`AucuneChemin Exception`                            |
|Instance non suspendue lors d’un `TerminerEtapeAsync`           |`EtatInstanceInvalideException`                     |
|Migration vers un nœud inexistant sans mapping                  |`MigrationImpossibleException`                      |
|Tentative de démarrer un 2e processus actif (même clé + agrégat)|`ProcessusDejaActifException`                       |

### 9.3 Logs

Le moteur émet des logs structurés via `ILogger<T>` à chaque transition significative :

|Niveau       |Événement                                                   |
|-------------|------------------------------------------------------------|
|`Information`|Démarrage d’instance, entrée/sortie de nœud, fin d’instance |
|`Information`|Suspension (interactif, attente temps, attente signal)      |
|`Information`|Réception d’un signal, reprise d’une attente                |
|`Warning`    |Aucune branche trouvée, utilisation de la branche par défaut|
|`Error`      |Exception dans un handler, erreur de migration              |
|`Debug`      |Résolution des paramètres, évaluation des conditions        |

### 9.4 Exécution en mémoire

Les nœuds métier enchaînés sans suspension s’exécutent séquentiellement en mémoire dans la même transaction, sans aller-retour en base entre chaque nœud. La persistance de l’état de l’instance n’est effectuée qu’aux points de suspension (nœud interactif, attente temps, attente signal) et à la fin du processus.

-----

## 10. Injection de dépendances (Autofac)

### 10.1 Enregistrement côté application cliente

```csharp
var builder = new ContainerBuilder();

builder.RegisterModule(new BpmModule(config =>
{
    // Découverte automatique de tous les IBpmHandlerCommande et IBpmHandlerQuery<> de l'assembly
    config.ScanHandlers(Assembly.GetExecutingAssembly());

    // Gestionnaire de tâches humaines (optionnel)
    config.UseGestionTache<MaGestionTache>();

    // Persistance Oracle avec préfixe de tables
    config.UseOracle("BPM");

    // OU persistance SQLite (pour les tests)
    // config.UseSqlite("BPM");
}));
```

### 10.2 Ce que le module enregistre automatiquement

- `IServiceBpm` → `ServiceBpm` (scoped)
- `IServiceMigration` → `ServiceMigration` (scoped)
- `IBpmHandlerCommande` → tous les handlers découverts (keyed par `NomCommande`)
- `IBpmHandlerQuery<>` → tous les handlers découverts (keyed par `NomQuery`)
- `IGestionTache` → implémentation fournie par l’app cliente (si configurée)
- Repositories Oracle ou SQLite selon la configuration
- `ILogger<T>` → délégué au conteneur parent (l’app cliente fournit le logging)

-----

## 11. Migration d’instances

### 11.1 Principe

La migration met à jour **en place** l’instance existante. L’ID de l’instance est conservé. Les références dans la base de données cliente restent valides.

### 11.2 Étapes d’une migration

1. Chargement de l’instance et de la définition cible (version publiée).
1. Vérification que l’instance est dans un état migreable (`Active` ou `Suspendue`).
1. Résolution du nœud courant dans la nouvelle version :
- Si le nœud existe à l’identique → pas de changement de position.
- Si un mapping est fourni pour ce nœud → utilisation du nœud cible du mapping.
- Si le nœud n’existe pas et qu’aucun mapping n’est fourni → `MigrationImpossibleException`.
1. Mise à jour de `VERSION_DEFINITION` et `ID_NOEUD_COURANT` sur l’instance.
1. Ajout d’un événement `MigrationInstance` dans l’historique avec : ancienne version, nouvelle version, ancien nœud, nouveau nœud.

### 11.3 Résultat de migration

```csharp
public record ResultatMigration(
    long IdInstance,
    bool Succes,
    int AncienneVersion,
    int NouvelleVersion,
    string? AncienNoeudId,
    string? NouveauNoeudId,
    string? MessageErreur
);
```

-----

## 12. Historique et logs

### 12.1 Événements automatiquement enregistrés

Toutes les transitions importantes sont enregistrées dans `{PREFIX}_EVENEMENT_INSTANCE` sans intervention des handlers. L’application cliente peut consulter l’historique via `IServiceBpm.ObtenirHistoriqueAsync`.

### 12.2 Accès à l’historique

```csharp
var evenements = await serviceBpm.ObtenirHistoriqueAsync(idInstance);

foreach (var evt in evenements)
{
    Console.WriteLine($"{evt.Horodatage} | {evt.TypeEvenement} | {evt.NomNoeud} | {evt.Resultat} | {evt.DureeMs}ms");
}
```

-----

## 13. Scénarios de tests attendus

### 13.1 Tests unitaires (`BpmPlus.UnitTests`)

- **Résolution de paramètres** : `SourceVariable` et `SourceValeurStatique` résolvent correctement vers les bons types.
- **Évaluation de conditions** : chaque opérateur de `ConditionVariable` avec tous les types scalaires.
- **Builder Fluent** : la définition construite correspond au JSON attendu.
- **Désérialisation JSON** : un JSON valide produit une `DefinitionProcessus` correcte.
- **JSON invalide** : les erreurs de validation sont claires et précises.
- **Sélection de branche XOR** : la première condition vraie est choisie, la branche par défaut est utilisée en repli.

### 13.2 Tests d’intégration (`BpmPlus.IntegrationTests`)

Tous les tests d’intégration tournent sur SQLite en mémoire. Chaque test crée sa propre base, ses tables et sa transaction.

**Scénario 1 — Processus linéaire bout-en-bout**  
Définition : 3 nœuds métier enchaînés → fin.  
Vérifications : instance `Terminee`, 3 événements `SortieNoeud`, `DateFin` renseignée, handlers appelés dans le bon ordre.

**Scénario 2 — Nœud interactif**  
Définition : nœud métier → nœud interactif → nœud métier → fin.  
Vérifications : après le 1er nœud, instance `Suspendue`; `IGestionTache.CreerTacheAsync` appelé; après `TerminerEtapeAsync`, `IGestionTache.FermerTacheAsync` appelé; instance `Terminee`.

**Scénario 3 — Nœud de décision XOR**  
Définition : nœud métier → décision (variable = “A” → chemin A, défaut → chemin B).  
Test A : variable = “A” → chemin A emprunté.  
Test B : variable = “B” → branche par défaut empruntée.

**Scénario 4 — Attente signal ciblé**  
Instance suspendue sur `NoeudAttenteSignal`. `EnvoyerSignalAsync` ciblé → instance reprend et se termine.

**Scénario 5 — Attente signal broadcast**  
3 instances suspendues sur le même signal. `EnvoyerSignalAsync` broadcast → les 3 instances reprennent.

**Scénario 6 — Attente de temps**  
Instance suspendue. `ObtenirInstancesEchuesAsync` retourne l’instance après la date. `ReprendreAttenteTempsAsync` → instance reprend.

**Scénario 7 — Sous-processus**  
Processus parent → nœud sous-processus → fin parent.  
Vérifications : instance enfant créée avec `IdInstanceParent`; variables héritées du parent; variables sorties remontées au parent; instance enfant `Terminee` avant la fin du parent.

**Scénario 8 — Erreur dans un handler**  
Handler qui lève une exception. Vérification : exception propagée à l’appelant, aucune modification persistée (rollback).

**Scénario 9 — Migration d’instance**  
Instance suspendue sur `noeud-A` (version 1). Publication version 2 avec mapping `noeud-A → noeud-A-v2`. Migration → instance mise à jour, événement `MigrationInstance` dans l’historique, version = 2, nœud courant = `noeud-A-v2`.

**Scénario 10 — Contrainte unicité processus actif**  
Tentative de démarrer un 2e processus actif pour le même agrégat et la même clé → `ProcessusDejaActifException`.

**Scénario 11 — Processus avec fins multiples**  
Décision → chemin succès (fin A) ou chemin échec (fin B). Vérifications sur les deux chemins.

**Scénario 12 — Variables du processus**  
Modification d’une variable via `ModifierVariableAsync`. Vérification de la valeur relue et de l’événement `VariableModifiee` dans l’historique.

-----

## 14. Hors périmètre

Les éléments suivants sont explicitement **hors périmètre** de `BpmPlus` :

- **Scheduler intégré** : le réveil des nœuds d’attente de temps est entièrement géré par l’application cliente (Hangfire, Quartz, etc.).
- **Interface utilisateur** : aucune UI de supervision ou de modélisation de processus.
- **Parallélisme (AND-split / AND-join)** : uniquement XOR sur les décisions.
- **Événements de domaine** : le moteur ne publie pas d’événements sur un bus de messages. C’est aux handlers clients de le faire si nécessaire.
- **Authentification / autorisation** : la sécurité d’accès aux méthodes de `IServiceBpm` est à la charge de l’application cliente.
- **Format BPMN 2.0** : le format JSON de définition est un format maison adapté aux besoins du projet.
- **Rollback partiel / retry** : en cas d’erreur, c’est toujours un rollback complet. Aucune logique de retry intégrée.
