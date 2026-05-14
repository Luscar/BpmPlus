using System.Text;
using BpmPlus.Abstractions;

namespace BpmPlus.Core.Definition;

// ═══════════════════════════════════════════════════════════════════════════════
//  DefinitionBuilder  —  New-generation process definition DSL
//
//  Design goals
//  ────────────
//  • Complex definitions stay readable: phases group related nodes visually.
//  • Every parameter is tweakable: nothing is hidden behind implicit defaults.
//  • Rich condition DSL: SiVariable("x").EstEgalA("y").Aller("node")
//  • Rich task DSL:      TacheHumaine(t => t.Titre("…").Role("…").AssignerA("…"))
//  • Approval pattern:   PatternApprobation(…) wires 2 nodes automatically.
//  • Strict build:       BuildStrict() catches dead-ends and dangling refs.
//  • Mermaid export:     Build(out string mermaid) — works on any definition.
//
//  Quick example
//  ─────────────
//  var def = DefinitionBuilder
//      .Definir("approbation-commande")
//      .Intitule("Processus d'approbation de commande")
//      .Description("Du bon de commande jusqu'à la notification.")
//      .Auteur("Équipe Finance").Etiquettes("commandes", "approbation")
//      .Commence("valider-commande")
//
//      .Phase("Validation", phase => phase
//          .Metier("valider-commande", "Valider la commande", "approbation-responsable"))
//
//      .PatternApprobation(
//          idTache:          "approbation-responsable",
//          intituleTache:    "Approbation responsable",
//          idDecision:       "decision-approbation",
//          commandeDecision: "EnregistrerDecisionCommand",
//          queryDecision:    "EstCommandeApprouveeQuery",
//          versApprouve:     "notification-approbation",
//          versRefuse:       "notification-refus")
//
//      .Phase("Résultat", phase => phase
//          .Metier("notification-approbation", "Notifier approbation")
//          .Metier("notification-refus",       "Notifier refus"))
//
//      .BuildStrict();   // validates dead-ends and orphan nodes
// ═══════════════════════════════════════════════════════════════════════════════

// ── DefinitionBuilder ──────────────────────────────────────────────────────────

public sealed class DefinitionBuilder
{
    private readonly string _cle;
    private string _nom         = string.Empty;
    private string _description = string.Empty;
    private string _auteur      = string.Empty;
    private string _debut       = string.Empty;
    private readonly List<string>         _etiquettes = new();
    private readonly List<NoeudProcessus> _noeuds     = new();

    public static DefinitionBuilder Definir(string cle) => new(cle);

    internal DefinitionBuilder(string cle) => _cle = cle;

    // ── Process-level metadata ────────────────────────────────────────────────

    public DefinitionBuilder Intitule(string nom)              { _nom         = nom;    return this; }
    public DefinitionBuilder Description(string description)   { _description = description; return this; }
    public DefinitionBuilder Auteur(string auteur)             { _auteur      = auteur; return this; }
    public DefinitionBuilder Etiquettes(params string[] tags)  { _etiquettes.AddRange(tags); return this; }

    /// <summary>Identifies the start node of the process.</summary>
    public DefinitionBuilder Commence(string noeudId)          { _debut = noeudId; return this; }

    // ── Phase grouping ────────────────────────────────────────────────────────

    /// <summary>
    /// Organises nodes into a named phase. Phases are purely cosmetic —
    /// they have no runtime effect but greatly improve readability of complex
    /// definitions.
    /// </summary>
    public DefinitionBuilder Phase(string nom, Action<PhaseV2Builder> configure)
    {
        configure(new PhaseV2Builder(nom, this));
        return this;
    }

    // ── Internal registration (called by PhaseV2Builder and by pattern helpers) ─

    internal DefinitionBuilder AjouterNoeud(NoeudProcessus noeud)
    {
        _noeuds.Add(noeud);
        return this;
    }

    // ── Métier ────────────────────────────────────────────────────────────────

    /// <summary>Final business node (no next node ⇒ EstFinale = true).</summary>
    public DefinitionBuilder Metier(string id, string nom = "")
    {
        var b = new MetierV2Builder(id, nom); b.Final();
        return AjouterNoeud(b.Build());
    }

    /// <summary>Business node with an explicit next node.</summary>
    public DefinitionBuilder Metier(string id, string nom, string vers)
    {
        var b = new MetierV2Builder(id, nom); b.Puis(vers);
        return AjouterNoeud(b.Build());
    }

    /// <summary>Business node with advanced configuration.</summary>
    public DefinitionBuilder Metier(string id, string nom, Action<MetierV2Builder> configure)
    {
        var b = new MetierV2Builder(id, nom);
        configure(b);
        return AjouterNoeud(b.Build());
    }

    public DefinitionBuilder Metier(string id, Action<MetierV2Builder> configure)
        => Metier(id, string.Empty, configure);

    // ── Interactif ────────────────────────────────────────────────────────────

    public DefinitionBuilder Interactif(string id, string nom, Action<InteractifV2Builder> configure)
    {
        var b = new InteractifV2Builder(id, nom);
        configure(b);
        return AjouterNoeud(b.Build());
    }

    public DefinitionBuilder Interactif(string id, Action<InteractifV2Builder> configure)
        => Interactif(id, string.Empty, configure);

    // ── Décision ──────────────────────────────────────────────────────────────

    public DefinitionBuilder Decision(string id, string nom, Action<DecisionV2Builder> configure)
    {
        var b = new DecisionV2Builder(id, nom);
        configure(b);
        return AjouterNoeud(b.Build());
    }

    public DefinitionBuilder Decision(string id, Action<DecisionV2Builder> configure)
        => Decision(id, string.Empty, configure);

    // ── AttenteTemps ──────────────────────────────────────────────────────────

    public DefinitionBuilder AttenteTemps(string id, string nom, Action<AttenteTempsV2Builder> configure)
    {
        var b = new AttenteTempsV2Builder(id, nom);
        configure(b);
        return AjouterNoeud(b.Build());
    }

    public DefinitionBuilder AttenteTemps(string id, Action<AttenteTempsV2Builder> configure)
        => AttenteTemps(id, string.Empty, configure);

    // ── AttenteSignal ─────────────────────────────────────────────────────────

    public DefinitionBuilder AttenteSignal(string id, string signal, string? vers = null)
        => AttenteSignal(id, string.Empty, signal, vers);

    public DefinitionBuilder AttenteSignal(string id, string nom, string signal, string? vers = null)
    {
        return AjouterNoeud(new NoeudAttenteSignal
        {
            Id           = id,
            Nom          = nom,
            NomSignal    = signal,
            EstFinale    = vers is null,
            FluxSortants = vers is not null ? [new FluxSortant { Vers = vers }] : []
        });
    }

    // ── SousProcessus ─────────────────────────────────────────────────────────

    public DefinitionBuilder SousProcessus(string id, string nom, Action<SousDefinitionBuilder> configure)
    {
        var b = new SousDefinitionBuilder(id, nom);
        configure(b);
        return AjouterNoeud(b.Build());
    }

    public DefinitionBuilder SousProcessus(string id, Action<SousDefinitionBuilder> configure)
        => SousProcessus(id, string.Empty, configure);

    // ── Pattern templates ─────────────────────────────────────────────────────

    /// <summary>
    /// Inserts a complete human-approval pattern (interactive node + decision gateway)
    /// directly wired to the provided approve / reject destinations.
    /// <para>
    /// Creates two nodes: <paramref name="idTache"/> and <paramref name="idDecision"/>.
    /// </para>
    /// </summary>
    public DefinitionBuilder PatternApprobation(
        string idTache,
        string intituleTache,
        string idDecision,
        string commandeDecision,
        string queryDecision,
        string versApprouve,
        string versRefuse)
    {
        Interactif(idTache, intituleTache, n => n
            .TacheHumaine(t => t.Titre(intituleTache))
            .AuRetour(commandeDecision)
            .Puis(idDecision));

        Decision(idDecision, n => n
            .SiQuery(queryDecision).Aller(versApprouve)
            .Sinon.Aller(versRefuse));

        return this;
    }

    // ── Build ─────────────────────────────────────────────────────────────────

    /// <summary>Builds the process definition.</summary>
    public DefinitionProcessus Build() => Assembler();

    /// <summary>
    /// Builds the definition with strict validation:
    /// catches dangling references, dead-end non-final nodes, and orphan nodes.
    /// </summary>
    public DefinitionProcessus BuildStrict()
    {
        var def = Assembler();
        ValiderStrict(def);
        return def;
    }

    /// <summary>
    /// Builds the definition and generates a Mermaid flowchart string.
    /// Process metadata (description, author, tags) appears as comments in the diagram.
    /// The same diagram can also be produced later via <see cref="DefinitionProcessusExtensions.ToMermaid"/>.
    /// </summary>
    public DefinitionProcessus Build(out string mermaid)
    {
        var def = Assembler();
        mermaid = MermaidExporter.Generer(def, _description, _auteur,
            _etiquettes.Count > 0 ? _etiquettes : null);
        return def;
    }

    // ── Assembly ──────────────────────────────────────────────────────────────

    private DefinitionProcessus Assembler()
    {
        if (string.IsNullOrWhiteSpace(_cle))
            throw new InvalidOperationException("La clé du processus est obligatoire.");
        if (string.IsNullOrWhiteSpace(_debut))
            throw new InvalidOperationException(
                "Le nœud de début est obligatoire — appelez .Commence(id) sur le builder.");
        if (_noeuds.Count == 0)
            throw new InvalidOperationException("La définition doit contenir au moins un nœud.");
        if (_noeuds.All(n => n.Id != _debut))
            throw new InvalidOperationException(
                $"Le nœud de début '{_debut}' est introuvable dans la définition.");

        return new DefinitionProcessus
        {
            Cle          = _cle,
            Nom          = _nom,
            NoeudDebutId = _debut,
            Noeuds       = new List<NoeudProcessus>(_noeuds),
            DateCreation = DateTime.UtcNow
        };
    }

    // ── Strict validation ─────────────────────────────────────────────────────

    private static void ValiderStrict(DefinitionProcessus def)
    {
        var ids     = def.Noeuds.Select(n => n.Id).ToHashSet();
        var erreurs = new List<string>();

        foreach (var noeud in def.Noeuds)
        {
            // Dead-end: not final but has no outgoing flow
            if (!noeud.EstFinale && noeud.FluxSortants.Count == 0)
                erreurs.Add($"[impasse]  Nœud '{noeud.Id}' n'est pas final et n'a aucune sortie.");

            // Dangling reference: a flow targets a node that doesn't exist
            foreach (var flux in noeud.FluxSortants)
            {
                if (!string.IsNullOrWhiteSpace(flux.Vers) && !ids.Contains(flux.Vers))
                    erreurs.Add(
                        $"[référence]  Nœud '{noeud.Id}' → '{flux.Vers}' introuvable dans la définition.");
            }
        }

        // Orphan detection: nodes not reachable from the start node or from any other node
        var cibles = def.Noeuds
            .SelectMany(n => n.FluxSortants.Select(f => f.Vers))
            .ToHashSet();
        cibles.Add(def.NoeudDebutId);

        foreach (var noeud in def.Noeuds)
        {
            if (!cibles.Contains(noeud.Id))
                erreurs.Add($"[orphelin]  Nœud '{noeud.Id}' n'est référencé par aucun flux (inaccessible).");
        }

        if (erreurs.Count > 0)
            throw new InvalidOperationException(
                $"La définition '{def.Cle}' contient {erreurs.Count} erreur(s) :\n"
                + string.Join("\n", erreurs.Select(e => "  • " + e)));
    }
}

// ── PhaseV2Builder ─────────────────────────────────────────────────────────────

/// <summary>
/// Scoped builder for a named phase. Delegates node registration to the parent
/// <see cref="DefinitionBuilder"/>. Phases carry no runtime meaning —
/// they exist solely to organise code.
/// </summary>
public sealed class PhaseV2Builder
{
    private readonly string               _nom;
    private readonly DefinitionBuilder   _parent;

    internal PhaseV2Builder(string nom, DefinitionBuilder parent)
    {
        _nom    = nom;
        _parent = parent;
    }

    public PhaseV2Builder Metier(string id, string nom = "")
    {
        var b = new MetierV2Builder(id, nom); b.Final();
        _parent.AjouterNoeud(b.Build()); return this;
    }

    public PhaseV2Builder Metier(string id, string nom, string vers)
    {
        var b = new MetierV2Builder(id, nom); b.Puis(vers);
        _parent.AjouterNoeud(b.Build()); return this;
    }

    public PhaseV2Builder Metier(string id, string nom, Action<MetierV2Builder> configure)
    {
        var b = new MetierV2Builder(id, nom); configure(b);
        _parent.AjouterNoeud(b.Build()); return this;
    }

    public PhaseV2Builder Metier(string id, Action<MetierV2Builder> configure)
        => Metier(id, string.Empty, configure);

    public PhaseV2Builder Interactif(string id, string nom, Action<InteractifV2Builder> configure)
    {
        var b = new InteractifV2Builder(id, nom); configure(b);
        _parent.AjouterNoeud(b.Build()); return this;
    }

    public PhaseV2Builder Interactif(string id, Action<InteractifV2Builder> configure)
        => Interactif(id, string.Empty, configure);

    public PhaseV2Builder Decision(string id, string nom, Action<DecisionV2Builder> configure)
    {
        var b = new DecisionV2Builder(id, nom); configure(b);
        _parent.AjouterNoeud(b.Build()); return this;
    }

    public PhaseV2Builder Decision(string id, Action<DecisionV2Builder> configure)
        => Decision(id, string.Empty, configure);

    public PhaseV2Builder AttenteTemps(string id, string nom, Action<AttenteTempsV2Builder> configure)
    {
        var b = new AttenteTempsV2Builder(id, nom); configure(b);
        _parent.AjouterNoeud(b.Build()); return this;
    }

    public PhaseV2Builder AttenteTemps(string id, Action<AttenteTempsV2Builder> configure)
        => AttenteTemps(id, string.Empty, configure);

    public PhaseV2Builder AttenteSignal(string id, string signal, string? vers = null)
    {
        _parent.AjouterNoeud(new NoeudAttenteSignal
        {
            Id           = id,
            NomSignal    = signal,
            EstFinale    = vers is null,
            FluxSortants = vers is not null ? [new FluxSortant { Vers = vers }] : []
        });
        return this;
    }

    public PhaseV2Builder AttenteSignal(string id, string nom, string signal, string? vers = null)
    {
        _parent.AjouterNoeud(new NoeudAttenteSignal
        {
            Id           = id,
            Nom          = nom,
            NomSignal    = signal,
            EstFinale    = vers is null,
            FluxSortants = vers is not null ? [new FluxSortant { Vers = vers }] : []
        });
        return this;
    }

    public PhaseV2Builder SousProcessus(string id, string nom, Action<SousDefinitionBuilder> configure)
    {
        var b = new SousDefinitionBuilder(id, nom); configure(b);
        _parent.AjouterNoeud(b.Build()); return this;
    }

    public PhaseV2Builder SousProcessus(string id, Action<SousDefinitionBuilder> configure)
        => SousProcessus(id, string.Empty, configure);
}

// ── MetierV2Builder ────────────────────────────────────────────────────────────

public sealed class MetierV2Builder
{
    private readonly string                              _id;
    private string                                      _nom;
    private bool                                        _final;
    private string                                      _commande = string.Empty;
    private readonly Dictionary<string, ISourceParametre> _params = new();
    private readonly List<FluxSortant>                  _flux    = new();

    public MetierV2Builder(string id, string nom) { _id = id; _nom = nom; }

    /// <summary>Overrides the command name. Default: PascalCase(id) + "Command".</summary>
    public MetierV2Builder Commande(string nom)              { _commande    = nom; return this; }

    /// <summary>Parameter sourced from the same-named process variable.</summary>
    public MetierV2Builder Param(string nom)                 => Param(nom, Src.Var(nom));

    /// <summary>Parameter with an explicit source (Src.Var / Src.Val / Src.Query).</summary>
    public MetierV2Builder Param(string nom, ISourceParametre src) { _params[nom] = src; return this; }

    /// <summary>Parameter with a hardcoded value.</summary>
    public MetierV2Builder ParamFixe(string nom, object? valeur) => Param(nom, Src.Val(valeur));

    /// <summary>Sets the next node (readable sequential alias of Vers).</summary>
    public MetierV2Builder Puis(string id)  { _flux.Add(new FluxSortant { Vers = id }); return this; }

    /// <summary>Sets the next node.</summary>
    public MetierV2Builder Vers(string id)  { _flux.Add(new FluxSortant { Vers = id }); return this; }

    public MetierV2Builder Final()          { _final = true; return this; }

    internal NoeudMetier Build() => new()
    {
        Id           = _id,
        Nom          = _nom,
        EstFinale    = _final,
        FluxSortants = _flux,
        NomCommande  = string.IsNullOrEmpty(_commande) ? V2Utils.NomParDefaut(_id, "Command") : _commande,
        Parametres   = _params
    };
}

// ── InteractifV2Builder ────────────────────────────────────────────────────────

public sealed class InteractifV2Builder
{
    private readonly string            _id;
    private readonly string            _nom;
    private bool                       _final;
    private DefinitionTache            _tache       = new();
    private DefinitionCommande?        _commandePre;
    private DefinitionCommande?        _commandePost;
    private readonly List<FluxSortant> _flux        = new();

    public InteractifV2Builder(string id, string nom) { _id = id; _nom = nom; }

    // ── Task configuration ────────────────────────────────────────────────────

    /// <summary>
    /// Configures the human task via a rich sub-builder with all task properties
    /// accessible in a single, discoverable fluent chain.
    /// </summary>
    public InteractifV2Builder TacheHumaine(Action<TacheV2Builder> configure)
    {
        var b = new TacheV2Builder();
        configure(b);
        _tache = b.Build();
        return this;
    }

    // ── Shortcut: title + optional description (v1 parity) ───────────────────

    public InteractifV2Builder Tache(string titre, string? description = null)
    {
        _tache = new DefinitionTache { Titre = titre, Description = description };
        return this;
    }

    // ── Inline task shortcuts (no sub-builder needed for simple cases) ────────

    public InteractifV2Builder Role(string codeRole)      { _tache.CodeRole  = codeRole; return this; }
    public InteractifV2Builder AssignerA(string logon)    { _tache.LogonAuto = logon;    return this; }

    // ── Commands ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Command executed at node entry (suspension).
    /// Default name: PascalCase(id) + "PreCommand".
    /// </summary>
    public InteractifV2Builder AuDemarrage(string? nom = null, Action<CommandeV2Builder>? configure = null)
    {
        var b = new CommandeV2Builder(nom ?? V2Utils.NomParDefaut(_id, "PreCommand"));
        configure?.Invoke(b);
        _commandePre = b.Build();
        return this;
    }

    /// <summary>
    /// Command executed at node exit (resume).
    /// Default name: PascalCase(id) + "PostCommand".
    /// </summary>
    public InteractifV2Builder AuRetour(string? nom = null, Action<CommandeV2Builder>? configure = null)
    {
        var b = new CommandeV2Builder(nom ?? V2Utils.NomParDefaut(_id, "PostCommand"));
        configure?.Invoke(b);
        _commandePost = b.Build();
        return this;
    }

    // ── Flow ──────────────────────────────────────────────────────────────────

    public InteractifV2Builder Puis(string id)  { _flux.Add(new FluxSortant { Vers = id }); return this; }
    public InteractifV2Builder Vers(string id)  { _flux.Add(new FluxSortant { Vers = id }); return this; }
    public InteractifV2Builder Final()          { _final = true; return this; }

    internal NoeudInteractif Build()
    {
        _tache.NomNoeud = string.IsNullOrWhiteSpace(_nom) ? _id : _nom;
        return new NoeudInteractif
        {
            Id              = _id,
            Nom             = _nom,
            EstFinale       = _final,
            FluxSortants    = _flux,
            DefinitionTache = _tache,
            CommandePre     = _commandePre,
            CommandePost    = _commandePost
        };
    }
}

// ── TacheV2Builder ─────────────────────────────────────────────────────────────

/// <summary>
/// Rich sub-builder for human task configuration. Groups all task properties
/// together so they're discoverable in one place.
/// </summary>
public sealed class TacheV2Builder
{
    private readonly DefinitionTache _tache = new();

    /// <summary>Title shown to the task assignee.</summary>
    public TacheV2Builder Titre(string titre)              { _tache.Titre            = titre;     return this; }

    /// <summary>Detailed description or instructions for the task.</summary>
    public TacheV2Builder Description(string description)  { _tache.Description      = description; return this; }

    /// <summary>Role code required to execute this task (e.g. "RESPONSABLE").</summary>
    public TacheV2Builder Role(string codeRole)            { _tache.CodeRole         = codeRole;  return this; }

    /// <summary>Auto-assigns the task to this specific user.</summary>
    public TacheV2Builder AssignerA(string logon)          { _tache.LogonAuto        = logon;     return this; }

    /// <summary>External task-type code for integration with the task management system.</summary>
    public TacheV2Builder TypeTache(string codeTache)      { _tache.CodeTache        = codeTache; return this; }

    /// <summary>Marks this as a revision task (IndTacheRevision = true).</summary>
    public TacheV2Builder EstUneRevision(bool valeur = true) { _tache.IndTacheRevision = valeur;  return this; }

    /// <summary>Logon of the author of the item being reviewed.</summary>
    public TacheV2Builder LogonAuteur(string logon)        { _tache.LogonAuteur      = logon;     return this; }

    internal DefinitionTache Build() => _tache;
}

// ── CommandeV2Builder ──────────────────────────────────────────────────────────

/// <summary>Builder for command / query parameter sets.</summary>
public sealed class CommandeV2Builder
{
    private readonly string                                _nom;
    private readonly Dictionary<string, ISourceParametre> _params = new();

    public CommandeV2Builder(string nom) => _nom = nom;

    /// <summary>Parameter sourced from the same-named process variable.</summary>
    public CommandeV2Builder Param(string nom)                         => Param(nom, Src.Var(nom));

    /// <summary>Parameter with an explicit source (Src.Var / Src.Val / Src.Query).</summary>
    public CommandeV2Builder Param(string nom, ISourceParametre src)   { _params[nom] = src; return this; }

    /// <summary>Parameter with a hardcoded value.</summary>
    public CommandeV2Builder ParamFixe(string nom, object? valeur)     => Param(nom, Src.Val(valeur));

    internal DefinitionCommande Build() => new() { NomCommande = _nom, Parametres = _params };
}

// ── DecisionV2Builder ──────────────────────────────────────────────────────────

public sealed class DecisionV2Builder
{
    private readonly string            _id;
    private readonly string            _nom;
    private bool                       _final;
    private readonly List<FluxSortant> _flux = new();

    public DecisionV2Builder(string id, string nom) { _id = id; _nom = nom; }

    // ── Condition DSL ─────────────────────────────────────────────────────────

    /// <summary>
    /// Starts a fluent condition on a process variable.
    /// Chain: <c>.SiVariable("x").EstEgalA("y").Aller("node-id")</c>
    /// </summary>
    public ConditionVariableV2Builder SiVariable(string variable)
        => new(variable, this);

    /// <summary>
    /// Query-based condition. Default name: PascalCase(id) + "Query".
    /// </summary>
    public FluxV2Builder SiQuery(string? nom = null, Action<CommandeV2Builder>? configure = null)
    {
        var nomQuery = nom ?? V2Utils.NomParDefaut(_id, "Query");
        Dictionary<string, ISourceParametre>? parametres = null;
        if (configure is not null)
        {
            var b = new CommandeV2Builder(nomQuery);
            configure(b);
            var built = b.Build();
            parametres = built.Parametres.Count > 0 ? built.Parametres : null;
        }
        return AjouterFlux(new ConditionQuery(nomQuery, parametres));
    }

    /// <summary>
    /// Default (catch-all) branch — no condition evaluated.
    /// Usage: <c>.Sinon.Aller("node-id")</c>
    /// </summary>
    public FluxV2Builder Sinon
    {
        get
        {
            var flux = new FluxSortant { EstParDefaut = true };
            _flux.Add(flux);
            return new FluxV2Builder(this, flux);
        }
    }

    public DecisionV2Builder Final() { _final = true; return this; }

    internal FluxV2Builder AjouterFlux(ICondition condition)
    {
        var flux = new FluxSortant { Condition = condition };
        _flux.Add(flux);
        return new FluxV2Builder(this, flux);
    }

    internal NoeudDecision Build() => new()
    {
        Id           = _id,
        Nom          = _nom,
        EstFinale    = _final,
        FluxSortants = _flux
    };
}

// ── ConditionVariableV2Builder ─────────────────────────────────────────────────

/// <summary>
/// Fluent variable-condition builder returned by <see cref="DecisionV2Builder.SiVariable"/>.
/// <para>Example: <c>.SiVariable("montant").EstSuperieurA(10_000m).Aller("haute-valeur")</c></para>
/// </summary>
public sealed class ConditionVariableV2Builder
{
    private readonly string              _variable;
    private readonly DecisionV2Builder   _parent;

    internal ConditionVariableV2Builder(string variable, DecisionV2Builder parent)
    {
        _variable = variable;
        _parent   = parent;
    }

    public FluxV2Builder EstEgalA(object? valeur)              => Cond(Operateur.Egal,            valeur);
    public FluxV2Builder EstDifferentDe(object? valeur)        => Cond(Operateur.Different,       valeur);
    public FluxV2Builder EstSuperieurA(object? valeur)         => Cond(Operateur.Superieur,       valeur);
    public FluxV2Builder EstInferieurA(object? valeur)         => Cond(Operateur.Inferieur,       valeur);
    public FluxV2Builder EstSuperieurOuEgalA(object? valeur)   => Cond(Operateur.SuperieurOuEgal, valeur);
    public FluxV2Builder EstInferieurOuEgalA(object? valeur)   => Cond(Operateur.InferieurOuEgal, valeur);
    public FluxV2Builder Contient(string valeur)               => Cond(Operateur.Contient,        valeur);

    private FluxV2Builder Cond(Operateur op, object? valeur)
        => _parent.AjouterFlux(new ConditionVariable(_variable, op, valeur));
}

// ── FluxV2Builder ──────────────────────────────────────────────────────────────

/// <summary>
/// Intermediate builder that chains <c>.Aller()</c> after a condition declaration,
/// then returns control to the parent <see cref="DecisionV2Builder"/>.
/// </summary>
public sealed class FluxV2Builder
{
    private readonly DecisionV2Builder _parent;
    private readonly FluxSortant       _flux;

    internal FluxV2Builder(DecisionV2Builder parent, FluxSortant flux)
    {
        _parent = parent;
        _flux   = flux;
    }

    /// <summary>Routes this branch to the target node.</summary>
    public DecisionV2Builder Aller(string id) { _flux.Vers = id; return _parent; }

    /// <summary>Same as <see cref="Aller"/> — familiar alias for v1 users.</summary>
    public DecisionV2Builder Vers(string id)  { _flux.Vers = id; return _parent; }
}

// ── AttenteTempsV2Builder ──────────────────────────────────────────────────────

public sealed class AttenteTempsV2Builder
{
    private readonly string            _id;
    private readonly string            _nom;
    private bool                       _final;
    private ISourceParametre           _echeance = new SourceValeurStatique(null);
    private readonly List<FluxSortant> _flux     = new();

    public AttenteTempsV2Builder(string id, string nom) { _id = id; _nom = nom; }

    /// <summary>Deadline read from a process variable (must hold a DateTime value).</summary>
    public AttenteTempsV2Builder EcheanceVariable(string variable)
    { _echeance = Src.Var(variable); return this; }

    /// <summary>Fixed static deadline baked in at definition time.</summary>
    public AttenteTempsV2Builder EcheanceFixe(DateTime date)
    { _echeance = Src.Val(date); return this; }

    /// <summary>Deadline computed at runtime by a query handler.</summary>
    public AttenteTempsV2Builder EcheanceQuery(string nom, Action<CommandeV2Builder>? configure = null)
    {
        Dictionary<string, ISourceParametre>? parametres = null;
        if (configure is not null)
        {
            var b = new CommandeV2Builder(nom);
            configure(b);
            var built = b.Build();
            parametres = built.Parametres.Count > 0 ? built.Parametres : null;
        }
        _echeance = new SourceQuery(nom, parametres);
        return this;
    }

    public AttenteTempsV2Builder Puis(string id)  { _flux.Add(new FluxSortant { Vers = id }); return this; }
    public AttenteTempsV2Builder Vers(string id)  { _flux.Add(new FluxSortant { Vers = id }); return this; }
    public AttenteTempsV2Builder Final()          { _final = true; return this; }

    internal NoeudAttenteTemps Build() => new()
    {
        Id                 = _id,
        Nom                = _nom,
        EstFinale          = _final,
        FluxSortants       = _flux,
        SourceDateEcheance = _echeance
    };
}

// ── SousDefinitionBuilder ─────────────────────────────────────────────────────

public sealed class SousDefinitionBuilder
{
    private readonly string            _id;
    private readonly string            _nom;
    private bool                       _final;
    private string                     _cle     = string.Empty;
    private int                        _version = 1;
    private readonly List<string>      _sorties = new();
    private readonly List<FluxSortant> _flux    = new();

    public SousDefinitionBuilder(string id, string nom) { _id = id; _nom = nom; }

    public SousDefinitionBuilder Definition(string cle, int version = 1)
    { _cle = cle; _version = version; return this; }

    /// <summary>Declares a variable whose value is propagated back to the parent process.</summary>
    public SousDefinitionBuilder Sortie(string variable)          { _sorties.Add(variable); return this; }

    /// <summary>Declares multiple output variables in one call.</summary>
    public SousDefinitionBuilder Sorties(params string[] variables) { _sorties.AddRange(variables); return this; }

    public SousDefinitionBuilder Puis(string id)  { _flux.Add(new FluxSortant { Vers = id }); return this; }
    public SousDefinitionBuilder Vers(string id)  { _flux.Add(new FluxSortant { Vers = id }); return this; }
    public SousDefinitionBuilder Final()          { _final = true; return this; }

    internal NoeudSousProcessus Build() => new()
    {
        Id               = _id,
        Nom              = _nom,
        EstFinale        = _final,
        FluxSortants     = _flux,
        CleDefinition    = _cle,
        Version          = _version,
        VariablesSorties = _sorties
    };
}

// ── MermaidExporter ────────────────────────────────────────────────────────────

/// <summary>
/// Generates a Mermaid flowchart from any <see cref="DefinitionProcessus"/>.
/// Can be used standalone via the <see cref="DefinitionProcessusExtensions.ToMermaid"/> extension.
/// </summary>
public static class MermaidExporter
{
    public static string Generer(
        DefinitionProcessus def,
        string? description = null,
        string? auteur = null,
        IReadOnlyList<string>? etiquettes = null)
    {
        var sb = new StringBuilder();

        sb.AppendLine("flowchart TD");

        if (!string.IsNullOrWhiteSpace(def.Nom))
            sb.AppendLine($"    %% {def.Nom}");
        if (!string.IsNullOrWhiteSpace(description))
            sb.AppendLine($"    %% {description}");
        if (!string.IsNullOrWhiteSpace(auteur))
            sb.AppendLine($"    %% Auteur : {auteur}");
        if (etiquettes?.Count > 0)
            sb.AppendLine($"    %% Étiquettes : {string.Join(", ", etiquettes)}");

        // Node style classes
        sb.AppendLine();
        sb.AppendLine("    classDef metier      fill:#dbeafe,stroke:#3b82f6,color:#1e3a5f");
        sb.AppendLine("    classDef terminal    fill:#dcfce7,stroke:#16a34a,color:#14532d");
        sb.AppendLine("    classDef interactif  fill:#ede9fe,stroke:#7c3aed,color:#3b0764");
        sb.AppendLine("    classDef decision    fill:#fef9c3,stroke:#ca8a04,color:#713f12");
        sb.AppendLine("    classDef attente     fill:#ffedd5,stroke:#ea580c,color:#7c2d12");
        sb.AppendLine("    classDef signal      fill:#fce7f3,stroke:#db2777,color:#831843");
        sb.AppendLine("    classDef sousproc    fill:#f1f5f9,stroke:#64748b,color:#1e293b");

        // Node declarations
        sb.AppendLine();
        foreach (var noeud in def.Noeuds)
        {
            var label  = string.IsNullOrWhiteSpace(noeud.Nom) ? noeud.Id : noeud.Nom;
            var safeId = SafeId(noeud.Id);

            var (shape, cssClass) = noeud switch
            {
                NoeudMetier    { EstFinale: true }  => ($"{safeId}([\"⏹ {Esc(label)}\"])",         "terminal"),
                NoeudMetier                          => ($"{safeId}[\"{Esc(label)}\"]",              "metier"),
                NoeudInteractif                      => ($"{safeId}[/\"👤 {Esc(label)}\"/]",         "interactif"),
                NoeudDecision                        => ($"{safeId}{{\"◇ {Esc(label)}\"}}", "decision"),
                NoeudAttenteTemps                    => ($"{safeId}>\"{Esc(label)} ⏱\"]",            "attente"),
                NoeudAttenteSignal n                 => ($"{safeId}((\"◎ {Esc(n.NomSignal)}\"))",    "signal"),
                NoeudSousProcessus                   => ($"{safeId}[[\"{Esc(label)}\"]]",            "sousproc"),
                _                                    => ($"{safeId}[\"{Esc(label)}\"]",              "metier")
            };

            sb.AppendLine($"    {shape}");
            sb.AppendLine($"    class {safeId} {cssClass}");
        }

        // Start-node marker
        var debutId = SafeId(def.NoeudDebutId);
        sb.AppendLine();
        sb.AppendLine($"    debut((▶)) --> {debutId}");
        sb.AppendLine("    style debut fill:#22c55e,stroke:#15803d,color:#fff");

        // Edges
        sb.AppendLine();
        foreach (var noeud in def.Noeuds)
        {
            var fromId = SafeId(noeud.Id);
            foreach (var flux in noeud.FluxSortants)
            {
                if (string.IsNullOrWhiteSpace(flux.Vers)) continue;

                var toId      = SafeId(flux.Vers);
                var condLabel = flux.Condition switch
                {
                    ConditionVariable cv =>
                        $"\"{cv.NomVariable} {OperateurSymbole(cv.Operateur)} {cv.Valeur}\"",
                    ConditionQuery cq =>
                        $"\"{cq.NomQuery}\"",
                    _ when flux.EstParDefaut => "\"défaut\"",
                    _                        => string.Empty
                };

                var arrow = string.IsNullOrEmpty(condLabel)
                    ? "-->"
                    : $"-->|{condLabel}|";

                sb.AppendLine($"    {fromId} {arrow} {toId}");
            }
        }

        return sb.ToString();
    }

    private static string SafeId(string id)
        => id.Replace("-", "_").Replace(" ", "_").Replace(".", "_");

    private static string Esc(string s)
        => s.Replace("\"", "'").Replace("{", "(").Replace("}", ")");

    private static string OperateurSymbole(Operateur op) => op switch
    {
        Operateur.Egal            => "=",
        Operateur.Different       => "≠",
        Operateur.Superieur       => ">",
        Operateur.Inferieur       => "<",
        Operateur.SuperieurOuEgal => "≥",
        Operateur.InferieurOuEgal => "≤",
        Operateur.Contient        => "∋",
        _                         => op.ToString()
    };
}

// ── Extension: ToMermaid on any DefinitionProcessus ───────────────────────────

/// <summary>
/// Extension methods that enrich <see cref="DefinitionProcessus"/> with v2 tooling.
/// </summary>
public static class DefinitionProcessusExtensions
{
    /// <summary>
    /// Generates a Mermaid flowchart string from an existing definition.
    /// Works on definitions built by either the v1 or the v2 builder.
    /// </summary>
    public static string ToMermaid(this DefinitionProcessus definition)
        => MermaidExporter.Generer(definition);
}

// ── Internal utility ───────────────────────────────────────────────────────────

internal static class V2Utils
{
    /// <summary>Converts a kebab/snake/space id to PascalCase + suffix.</summary>
    internal static string NomParDefaut(string id, string suffixe)
    {
        var parts = id.Split(['-', '_', ' '], StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(parts.Select(p => char.ToUpperInvariant(p[0]) + p[1..])) + suffixe;
    }
}
