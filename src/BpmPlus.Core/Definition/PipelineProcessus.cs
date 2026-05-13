using BpmPlus.Abstractions;

namespace BpmPlus.Core.Definition;

// ── PipelineProcessus ─────────────────────────────────────────────────────────
//
// Alternative au ProcessusBuilder.
//
// Principe : on écrit le processus comme une recette — étape par étape, de haut
// en bas. Les connexions entre nœuds sont gérées automatiquement ; inutile de
// déclarer des identifiants ou d'appeler .Vers(). Les IDs sont déduits du nom
// de la commande (kebab-case). Les branches de décision sont des sous-séquences
// inline qui partagent le compteur d'IDs du pipeline parent.
//
// Exemple rapide :
//
//   var def = PipelineProcessus.Creer("approbation-commande", "Approbation")
//       .Faire<ValiderCommandeCommand>()
//       .Tache("Approuver la commande", t => t.Post<EnregistrerDecisionCommand>())
//       .SiQuery<EstCommandeApprouveeQuery>(
//           oui: p => p.Faire<NotifierApprobationCommand>().Fin(),
//           non: p => p.Faire<NotifierRefusCommand>().Fin())
//       .Compiler();

/// <summary>
/// Builder séquentiel : chaque appel ajoute une étape et la connecte
/// automatiquement à la précédente. Aucun identifiant de nœud à gérer.
/// </summary>
public sealed class PipelineProcessus
{
    // ── État ──────────────────────────────────────────────────────────────────

    private readonly string _cle;
    private readonly string _nom;
    private readonly List<NoeudProcessus> _noeuds = [];
    private readonly List<string> _tetes = [];          // nœuds dont la sortie est encore libre
    private string? _idPremierNoeud;
    private readonly Dictionary<string, int> _compteurIds; // partagé entre parent et branches

    // ── Constructeurs (privés) ────────────────────────────────────────────────

    private PipelineProcessus(string cle, string nom)
    {
        _cle = cle;
        _nom = nom;
        _compteurIds = [];
    }

    // Constructeur pour les branches : partage le compteur d'IDs avec le parent
    // afin que les IDs restent globalement uniques.
    private PipelineProcessus(Dictionary<string, int> compteurIds)
    {
        _cle          = string.Empty;
        _nom          = string.Empty;
        _compteurIds  = compteurIds;
    }

    // ── Point d'entrée ────────────────────────────────────────────────────────

    /// <summary>Crée un nouveau pipeline pour le processus identifié par <paramref name="cle"/>.</summary>
    public static PipelineProcessus Creer(string cle, string nom = "") => new(cle, nom);

    // ── Étapes métier ─────────────────────────────────────────────────────────

    /// <summary>
    /// Ajoute une étape métier. Le nom de la commande est le nom exact du type
    /// <typeparamref name="TCommande"/> (ex. <c>ValiderCommandeCommand</c>).
    /// L'ID du nœud est dérivé en kebab-case sans le suffixe Command/Query.
    /// </summary>
    public PipelineProcessus Faire<TCommande>(Action<PipelineParametres>? parametres = null)
    {
        var nomCmd = typeof(TCommande).Name;
        var id     = GenererIdUnique(NomEnKebab(SansSuffixeType(nomCmd)));

        AjouterNoeud(new NoeudMetier
        {
            Id           = id,
            Nom          = nomCmd,
            NomCommande  = nomCmd,
            Parametres   = BuildParams(parametres),
            FluxSortants = []
        });
        return this;
    }

    /// <summary>
    /// Ajoute une étape métier avec un nom de commande explicite (chaîne).
    /// Utile pour les commandes non représentées par un type C#.
    /// </summary>
    public PipelineProcessus Faire(string nomCommande, Action<PipelineParametres>? parametres = null)
    {
        var id = GenererIdUnique(NomEnKebab(SansSuffixeType(nomCommande)));

        AjouterNoeud(new NoeudMetier
        {
            Id           = id,
            Nom          = nomCommande,
            NomCommande  = nomCommande,
            Parametres   = BuildParams(parametres),
            FluxSortants = []
        });
        return this;
    }

    // ── Étapes interactives ───────────────────────────────────────────────────

    /// <summary>
    /// Ajoute une étape interactive (tâche humaine). Le processus se suspend
    /// jusqu'à ce que la tâche soit complétée depuis l'extérieur.
    /// </summary>
    public PipelineProcessus Tache(string titre, Action<PipelineTache>? config = null)
    {
        var id     = GenererIdUnique("tache");
        var tache  = new PipelineTache(titre);
        config?.Invoke(tache);
        AjouterNoeud(tache.Build(id));
        return this;
    }

    // ── Décisions sur variable ────────────────────────────────────────────────

    /// <summary>Branche si <paramref name="variable"/> == <paramref name="valeur"/>.</summary>
    public PipelineProcessus SiEgal(string variable, object? valeur,
        Action<PipelineProcessus> oui, Action<PipelineProcessus>? non = null)
        => AjouterDecision(new ConditionVariable(variable, Operateur.Egal, valeur), oui, non);

    /// <summary>Branche si <paramref name="variable"/> ≠ <paramref name="valeur"/>.</summary>
    public PipelineProcessus SiDiff(string variable, object? valeur,
        Action<PipelineProcessus> oui, Action<PipelineProcessus>? non = null)
        => AjouterDecision(new ConditionVariable(variable, Operateur.Different, valeur), oui, non);

    /// <summary>Branche si <paramref name="variable"/> > <paramref name="valeur"/>.</summary>
    public PipelineProcessus SiSup(string variable, object? valeur,
        Action<PipelineProcessus> oui, Action<PipelineProcessus>? non = null)
        => AjouterDecision(new ConditionVariable(variable, Operateur.Superieur, valeur), oui, non);

    /// <summary>Branche si <paramref name="variable"/> &lt; <paramref name="valeur"/>.</summary>
    public PipelineProcessus SiInf(string variable, object? valeur,
        Action<PipelineProcessus> oui, Action<PipelineProcessus>? non = null)
        => AjouterDecision(new ConditionVariable(variable, Operateur.Inferieur, valeur), oui, non);

    /// <summary>Branche si <paramref name="variable"/> >= <paramref name="valeur"/>.</summary>
    public PipelineProcessus SiSupEgal(string variable, object? valeur,
        Action<PipelineProcessus> oui, Action<PipelineProcessus>? non = null)
        => AjouterDecision(new ConditionVariable(variable, Operateur.SuperieurOuEgal, valeur), oui, non);

    /// <summary>Branche si <paramref name="variable"/> &lt;= <paramref name="valeur"/>.</summary>
    public PipelineProcessus SiInfEgal(string variable, object? valeur,
        Action<PipelineProcessus> oui, Action<PipelineProcessus>? non = null)
        => AjouterDecision(new ConditionVariable(variable, Operateur.InferieurOuEgal, valeur), oui, non);

    /// <summary>Branche si <paramref name="variable"/> contient <paramref name="valeur"/>.</summary>
    public PipelineProcessus SiContient(string variable, string valeur,
        Action<PipelineProcessus> oui, Action<PipelineProcessus>? non = null)
        => AjouterDecision(new ConditionVariable(variable, Operateur.Contient, valeur), oui, non);

    // ── Décisions sur query ───────────────────────────────────────────────────

    /// <summary>
    /// Branche selon le résultat d'une <c>IBpmHandlerQuery&lt;bool&gt;</c>.
    /// Le nom de la query est le nom exact du type <typeparamref name="TQuery"/>.
    /// </summary>
    public PipelineProcessus SiQuery<TQuery>(
        Action<PipelineProcessus> oui,
        Action<PipelineProcessus>? non = null,
        Action<PipelineParametres>? parametres = null)
    {
        var nomQuery  = typeof(TQuery).Name;
        var p         = BuildParams(parametres);
        ICondition condition = new ConditionQuery(nomQuery, p.Count > 0 ? p : null);
        return AjouterDecision(condition, oui, non);
    }

    /// <summary>Branche selon le résultat d'une query identifiée par son nom.</summary>
    public PipelineProcessus SiQuery(string nomQuery,
        Action<PipelineProcessus> oui,
        Action<PipelineProcessus>? non = null,
        Action<PipelineParametres>? parametres = null)
    {
        var p = BuildParams(parametres);
        ICondition condition = new ConditionQuery(nomQuery, p.Count > 0 ? p : null);
        return AjouterDecision(condition, oui, non);
    }

    // ── Attentes ──────────────────────────────────────────────────────────────

    /// <summary>Le processus se suspend jusqu'à réception du signal <paramref name="signal"/>.</summary>
    public PipelineProcessus AttendreSignal(string signal)
    {
        var id = GenererIdUnique("attente-signal");
        AjouterNoeud(new NoeudAttenteSignal
        {
            Id           = id,
            Nom          = $"Attente signal : {signal}",
            NomSignal    = signal,
            FluxSortants = []
        });
        return this;
    }

    /// <summary>Le processus se suspend jusqu'à la date lue dans la variable <paramref name="nomVariable"/>.</summary>
    public PipelineProcessus AttendreDate(string nomVariable)
    {
        var id = GenererIdUnique("attente-date");
        AjouterNoeud(new NoeudAttenteTemps
        {
            Id                 = id,
            Nom                = $"Attente date ({nomVariable})",
            SourceDateEcheance = new SourceVariable(nomVariable),
            FluxSortants       = []
        });
        return this;
    }

    /// <summary>Le processus se suspend jusqu'à la date statique <paramref name="date"/>.</summary>
    public PipelineProcessus AttendreDate(DateTime date)
    {
        var id = GenererIdUnique("attente-date");
        AjouterNoeud(new NoeudAttenteTemps
        {
            Id                 = id,
            Nom                = "Attente date",
            SourceDateEcheance = new SourceValeurStatique(date),
            FluxSortants       = []
        });
        return this;
    }

    /// <summary>
    /// Le processus se suspend jusqu'à la date calculée par la query
    /// <typeparamref name="TQuery"/> (<c>IBpmHandlerQuery&lt;DateTime&gt;</c>).
    /// </summary>
    public PipelineProcessus AttendreDate<TQuery>(Action<PipelineParametres>? parametres = null)
    {
        var nomQuery = typeof(TQuery).Name;
        var p        = BuildParams(parametres);
        var id       = GenererIdUnique("attente-date");
        AjouterNoeud(new NoeudAttenteTemps
        {
            Id                 = id,
            Nom                = $"Attente date ({nomQuery})",
            SourceDateEcheance = new SourceQuery(nomQuery, p.Count > 0 ? p : null),
            FluxSortants       = []
        });
        return this;
    }

    // ── Sous-processus ────────────────────────────────────────────────────────

    /// <summary>
    /// Lance un sous-processus dans la même transaction.
    /// Les variables listées dans <paramref name="sorties"/> sont remontées au processus parent.
    /// </summary>
    public PipelineProcessus SousProcessus(string cle, int version = 1, params string[] sorties)
    {
        var id = GenererIdUnique("sous-processus");
        AjouterNoeud(new NoeudSousProcessus
        {
            Id               = id,
            Nom              = cle,
            CleDefinition    = cle,
            Version          = version,
            VariablesSorties = [.. sorties],
            FluxSortants     = []
        });
        return this;
    }

    // ── Finalisation et compilation ───────────────────────────────────────────

    /// <summary>
    /// Marque le(s) dernier(s) nœud(s) comme terminaux (EstFinale = true).
    /// À appeler à la fin d'une branche qui ne doit pas rejoindre la suite.
    /// </summary>
    public PipelineProcessus Fin()
    {
        foreach (var id in _tetes)
        {
            var n = _noeuds.FirstOrDefault(n => n.Id == id);
            if (n is not null) n.EstFinale = true;
        }
        _tetes.Clear();
        return this;
    }

    /// <summary>
    /// Construit et retourne la <see cref="DefinitionProcessus"/>.
    /// Les nœuds dont la sortie est encore libre sont automatiquement marqués terminaux.
    /// </summary>
    public DefinitionProcessus Compiler()
    {
        if (string.IsNullOrWhiteSpace(_cle))
            throw new InvalidOperationException("La clé du processus est obligatoire.");
        if (_noeuds.Count == 0)
            throw new InvalidOperationException("Le pipeline ne contient aucune étape.");

        foreach (var id in _tetes)
        {
            var n = _noeuds.FirstOrDefault(n => n.Id == id);
            if (n is not null) n.EstFinale = true;
        }

        return new DefinitionProcessus
        {
            Cle          = _cle,
            Nom          = _nom,
            NoeudDebutId = _idPremierNoeud!,
            Noeuds       = [.. _noeuds],
            DateCreation = DateTime.UtcNow
        };
    }

    // ── Méthodes internes ─────────────────────────────────────────────────────

    private void AjouterNoeud(NoeudProcessus noeud)
    {
        if (_idPremierNoeud is not null && _tetes.Count == 0)
            throw new InvalidOperationException(
                $"Impossible d'ajouter l'étape '{noeud.Id}' : toutes les branches précédentes " +
                "sont terminées (.Fin() appelé). Supprimez .Fin() sur les branches qui doivent continuer.");

        foreach (var idTete in _tetes)
            _noeuds.First(n => n.Id == idTete).FluxSortants.Add(new FluxSortant { Vers = noeud.Id });
        _tetes.Clear();

        _idPremierNoeud ??= noeud.Id;
        _noeuds.Add(noeud);
        _tetes.Add(noeud.Id);
    }

    private PipelineProcessus AjouterDecision(
        ICondition conditionOui,
        Action<PipelineProcessus> oui,
        Action<PipelineProcessus>? non)
    {
        var id       = GenererIdUnique("decision");
        var decision = new NoeudDecision { Id = id, Nom = "Décision", FluxSortants = [] };

        if (_idPremierNoeud is not null && _tetes.Count == 0)
            throw new InvalidOperationException(
                $"Impossible d'ajouter la décision '{id}' : toutes les branches précédentes sont terminées.");

        foreach (var idTete in _tetes)
            _noeuds.First(n => n.Id == idTete).FluxSortants.Add(new FluxSortant { Vers = id });
        _tetes.Clear();

        _idPremierNoeud ??= id;
        _noeuds.Add(decision);

        // Branche "oui" (condition respectée)
        var brancheOui = new PipelineProcessus(_compteurIds);
        oui(brancheOui);
        if (brancheOui._idPremierNoeud is not null)
        {
            decision.FluxSortants.Add(new FluxSortant { Condition = conditionOui, Vers = brancheOui._idPremierNoeud });
            _noeuds.AddRange(brancheOui._noeuds);
            _tetes.AddRange(brancheOui._tetes);
        }

        // Branche "non" (défaut)
        if (non is not null)
        {
            var brancheNon = new PipelineProcessus(_compteurIds);
            non(brancheNon);
            if (brancheNon._idPremierNoeud is not null)
            {
                decision.FluxSortants.Add(new FluxSortant { EstParDefaut = true, Vers = brancheNon._idPremierNoeud });
                _noeuds.AddRange(brancheNon._noeuds);
                _tetes.AddRange(brancheNon._tetes);
            }
        }

        return this;
    }

    private string GenererIdUnique(string base_)
    {
        var cle = base_.ToLowerInvariant();
        if (!_compteurIds.TryGetValue(cle, out var count))
        {
            _compteurIds[cle] = 1;
            return cle;
        }
        _compteurIds[cle] = count + 1;
        return $"{cle}-{count + 1}";
    }

    private static Dictionary<string, ISourceParametre> BuildParams(Action<PipelineParametres>? config)
    {
        if (config is null) return [];
        var b = new PipelineParametres();
        config(b);
        return b.Build();
    }

    // Supprime le suffixe Command / Query / Handler pour la génération de l'ID.
    private static string SansSuffixeType(string nom)
    {
        foreach (var suffixe in new[] { "Command", "Query", "Handler" })
            if (nom.EndsWith(suffixe, StringComparison.Ordinal) && nom.Length > suffixe.Length)
                return nom[..^suffixe.Length];
        return nom;
    }

    // PascalCase → kebab-case  (ValiderCommande → valider-commande)
    private static string NomEnKebab(string pascal)
    {
        if (string.IsNullOrEmpty(pascal)) return pascal;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < pascal.Length; i++)
        {
            var c = pascal[i];
            if (i > 0 && char.IsUpper(c) && char.IsLower(pascal[i - 1]))
                sb.Append('-');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}

// ── PipelineParametres ────────────────────────────────────────────────────────

/// <summary>
/// Déclaration des paramètres d'une étape ou d'une query.
/// Trois sources possibles : variable du processus, valeur statique, ou query.
/// </summary>
public sealed class PipelineParametres
{
    private readonly Dictionary<string, ISourceParametre> _params = [];

    /// <summary>Paramètre lu depuis la variable du processus portant le même nom.</summary>
    public PipelineParametres Var(string nom)
        { _params[nom] = new SourceVariable(nom); return this; }

    /// <summary>Paramètre lu depuis la variable <paramref name="nomVariable"/> et exposé sous <paramref name="nomParam"/>.</summary>
    public PipelineParametres Var(string nomParam, string nomVariable)
        { _params[nomParam] = new SourceVariable(nomVariable); return this; }

    /// <summary>Paramètre avec valeur statique.</summary>
    public PipelineParametres Val(string nom, object? valeur)
        { _params[nom] = new SourceValeurStatique(valeur); return this; }

    /// <summary>
    /// Paramètre calculé à l'exécution par la query <typeparamref name="TQuery"/>
    /// (<c>IBpmHandlerQuery&lt;T&gt;</c>). Les éventuels sous-paramètres de la query
    /// sont déclarés via <paramref name="sousParams"/>.
    /// </summary>
    public PipelineParametres Requete<TQuery>(string nomParam, Action<PipelineParametres>? sousParams = null)
    {
        var nomQuery = typeof(TQuery).Name;
        _params[nomParam] = new SourceQuery(nomQuery, BuildSousParams(sousParams));
        return this;
    }

    /// <summary>Paramètre calculé par la query identifiée par <paramref name="nomQuery"/>.</summary>
    public PipelineParametres Requete(string nomParam, string nomQuery, Action<PipelineParametres>? sousParams = null)
    {
        _params[nomParam] = new SourceQuery(nomQuery, BuildSousParams(sousParams));
        return this;
    }

    internal Dictionary<string, ISourceParametre> Build() => _params;

    private static Dictionary<string, ISourceParametre>? BuildSousParams(Action<PipelineParametres>? config)
    {
        if (config is null) return null;
        var b = new PipelineParametres();
        config(b);
        return b._params.Count > 0 ? b._params : null;
    }
}

// ── PipelineTache ─────────────────────────────────────────────────────────────

/// <summary>
/// Configuration d'une étape interactive (tâche humaine).
/// Accessible via le lambda de <see cref="PipelineProcessus.Tache"/>.
/// </summary>
public sealed class PipelineTache
{
    private readonly string _titre;
    private string?           _description;
    private string?           _codeRole;
    private string?           _codeTache;
    private string?           _logonAuto;
    private string?           _logonAuteur;
    private bool              _estRevision;
    private DefinitionCommande? _commandePre;
    private DefinitionCommande? _commandePost;

    internal PipelineTache(string titre) => _titre = titre;

    /// <summary>Description affichée dans la tâche.</summary>
    public PipelineTache Description(string description) { _description = description; return this; }

    /// <summary>Code de rôle requis pour traiter la tâche (ex. "RESPONSABLE").</summary>
    public PipelineTache Role(string codeRole) { _codeRole = codeRole; return this; }

    /// <summary>Code identifiant le type de tâche dans le système externe.</summary>
    public PipelineTache CodeTache(string code) { _codeTache = code; return this; }

    /// <summary>Logon de l'utilisateur auquel la tâche est automatiquement assignée.</summary>
    public PipelineTache AssignerA(string logon) { _logonAuto = logon; return this; }

    /// <summary>Logon de l'auteur de l'élément soumis à validation.</summary>
    public PipelineTache Auteur(string logon) { _logonAuteur = logon; return this; }

    /// <summary>Indique que la tâche est une révision d'une tâche existante.</summary>
    public PipelineTache EstRevision(bool valeur = true) { _estRevision = valeur; return this; }

    /// <summary>Commande exécutée à la suspension du processus (avant que l'utilisateur agisse).</summary>
    public PipelineTache Pre<TCommand>(Action<PipelineParametres>? parametres = null)
    {
        _commandePre = new DefinitionCommande
        {
            NomCommande = typeof(TCommand).Name,
            Parametres  = BuildParams(parametres)
        };
        return this;
    }

    /// <summary>Commande exécutée à la reprise du processus (après action de l'utilisateur).</summary>
    public PipelineTache Post<TCommand>(Action<PipelineParametres>? parametres = null)
    {
        _commandePost = new DefinitionCommande
        {
            NomCommande = typeof(TCommand).Name,
            Parametres  = BuildParams(parametres)
        };
        return this;
    }

    internal NoeudInteractif Build(string id) => new()
    {
        Id              = id,
        Nom             = _titre,
        EstFinale       = false,
        FluxSortants    = [],
        DefinitionTache = new DefinitionTache
        {
            Titre           = _titre,
            Description     = _description,
            CodeRole        = _codeRole,
            CodeTache       = _codeTache,
            LogonAuto       = _logonAuto,
            LogonAuteur     = _logonAuteur,
            IndTacheRevision = _estRevision,
            NomNoeud        = id
        },
        CommandePre  = _commandePre,
        CommandePost = _commandePost
    };

    private static Dictionary<string, ISourceParametre> BuildParams(Action<PipelineParametres>? config)
    {
        if (config is null) return [];
        var b = new PipelineParametres();
        config(b);
        return b.Build();
    }
}
