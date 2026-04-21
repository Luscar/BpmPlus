using BpmPlus.Abstractions;

namespace BpmPlus.Core.Definition;

// ── Src : sources de valeur ───────────────────────────────────────────────────

/// <summary>Raccourcis pour créer des sources de paramètres.</summary>
public static class Src
{
    public static ISourceParametre Var(string nom) => new SourceVariable(nom);
    public static ISourceParametre Val(object? valeur) => new SourceValeurStatique(valeur);
    public static ISourceParametre Query(string nom) => new SourceQuery(nom);
}

// ── ProcessusBuilder ──────────────────────────────────────────────────────────

public class ProcessusBuilder
{
    private readonly string _cle;
    private string _nom;
    private string _debut;
    private readonly List<NoeudProcessus> _noeuds = new();

    public ProcessusBuilder(string cle, string nom = "", string debut = "")
    {
        _cle = cle;
        _nom = nom;
        _debut = debut;
    }

    public ProcessusBuilder Nom(string nom)   { _nom   = nom; return this; }
    public ProcessusBuilder Debut(string id)  { _debut = id;  return this; }

    // ── Métier ────────────────────────────────────────────────────────────────

    /// <summary>Nœud final (pas de vers → EstFinale = true).</summary>
    public ProcessusBuilder Metier(string id, string nom = "")
    {
        var b = new MetierBuilder(id, nom);
        b.Final();
        _noeuds.Add(b.Build());
        return this;
    }

    /// <summary>Nœud métier avec nœud suivant.</summary>
    public ProcessusBuilder Metier(string id, string nom, string vers)
    {
        var b = new MetierBuilder(id, nom);
        b.Vers(vers);
        _noeuds.Add(b.Build());
        return this;
    }

    /// <summary>Nœud métier avec configuration avancée (paramètres, commande personnalisée…).</summary>
    public ProcessusBuilder Metier(string id, string nom, Action<MetierBuilder> config)
    {
        var b = new MetierBuilder(id, nom);
        config(b);
        _noeuds.Add(b.Build());
        return this;
    }

    /// <summary>Même chose sans nom d'affichage.</summary>
    public ProcessusBuilder Metier(string id, Action<MetierBuilder> config)
        => Metier(id, string.Empty, config);

    // ── Interactif ────────────────────────────────────────────────────────────

    public ProcessusBuilder Interactif(string id, string nom, Action<InteractifBuilder> config)
    {
        var b = new InteractifBuilder(id, nom);
        config(b);
        _noeuds.Add(b.Build());
        return this;
    }

    public ProcessusBuilder Interactif(string id, Action<InteractifBuilder> config)
        => Interactif(id, string.Empty, config);

    // ── Décision ──────────────────────────────────────────────────────────────

    public ProcessusBuilder Decision(string id, string nom, Action<DecisionBuilder> config)
    {
        var b = new DecisionBuilder(id, nom);
        config(b);
        _noeuds.Add(b.Build());
        return this;
    }

    public ProcessusBuilder Decision(string id, Action<DecisionBuilder> config)
        => Decision(id, string.Empty, config);

    // ── Attente ───────────────────────────────────────────────────────────────

    public ProcessusBuilder AttenteTemps(string id, string nom, Action<AttenteTempsBuilder> config)
    {
        var b = new AttenteTempsBuilder(id, nom);
        config(b);
        _noeuds.Add(b.Build());
        return this;
    }

    public ProcessusBuilder AttenteTemps(string id, Action<AttenteTempsBuilder> config)
        => AttenteTemps(id, string.Empty, config);

    /// <summary>
    /// Nœud d'attente de signal — inline, sans lambda.
    /// Si <paramref name="vers"/> est omis, le nœud est final.
    /// </summary>
    public ProcessusBuilder AttenteSignal(string id, string signal, string? vers = null)
        => AttenteSignal(id, string.Empty, signal, vers);

    public ProcessusBuilder AttenteSignal(string id, string nom, string signal, string? vers = null)
    {
        _noeuds.Add(new NoeudAttenteSignal
        {
            Id        = id,
            Nom       = nom,
            NomSignal = signal,
            EstFinale = vers is null,
            FluxSortants = vers is not null
                ? [new FluxSortant { Vers = vers }]
                : []
        });
        return this;
    }

    // ── Sous-processus ────────────────────────────────────────────────────────

    public ProcessusBuilder SousProcessus(string id, string nom, Action<SousProcessusBuilder> config)
    {
        var b = new SousProcessusBuilder(id, nom);
        config(b);
        _noeuds.Add(b.Build());
        return this;
    }

    public ProcessusBuilder SousProcessus(string id, Action<SousProcessusBuilder> config)
        => SousProcessus(id, string.Empty, config);

    // ── Build ─────────────────────────────────────────────────────────────────

    public DefinitionProcessus Build()
    {
        if (string.IsNullOrWhiteSpace(_cle))
            throw new InvalidOperationException("La clé est obligatoire.");
        if (string.IsNullOrWhiteSpace(_debut))
            throw new InvalidOperationException("Le nœud de début est obligatoire.");
        if (_noeuds.Count == 0)
            throw new InvalidOperationException("La définition doit contenir au moins un nœud.");
        if (_noeuds.All(n => n.Id != _debut))
            throw new InvalidOperationException($"Aucun nœud '{_debut}' (nœud de début) trouvé.");

        return new DefinitionProcessus
        {
            Cle          = _cle,
            Nom          = _nom,
            NoeudDebutId = _debut,
            Noeuds       = new List<NoeudProcessus>(_noeuds),
            DateCreation = DateTime.UtcNow
        };
    }
}

// ── Base ──────────────────────────────────────────────────────────────────────

public abstract class NoeudBuilder<TBuilder, TNoeud>
    where TBuilder : NoeudBuilder<TBuilder, TNoeud>
    where TNoeud : NoeudProcessus
{
    protected readonly string _id;
    protected string _nom;
    protected bool _final;
    protected readonly List<FluxSortant> _flux = new();

    protected NoeudBuilder(string id, string nom) { _id = id; _nom = nom; }

    public TBuilder Vers(string id)  { _flux.Add(new FluxSortant { Vers = id }); return (TBuilder)this; }
    public TBuilder Final()          { _final = true; return (TBuilder)this; }

    public abstract TNoeud Build();

    /// <summary>Convertit un id kebab/snake en PascalCase + suffixe. Ex. "creer-dossier" + "Command" → "CreerDossierCommand".</summary>
    internal static string NomParDefaut(string id, string suffixe)
    {
        var parts = id.Split(['-', '_', ' '], StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(parts.Select(p => char.ToUpperInvariant(p[0]) + p[1..])) + suffixe;
    }
}

// ── MetierBuilder ─────────────────────────────────────────────────────────────

public class MetierBuilder : NoeudBuilder<MetierBuilder, NoeudMetier>
{
    private string _commande = string.Empty;
    private readonly Dictionary<string, ISourceParametre> _params = new();

    public MetierBuilder(string id, string nom) : base(id, nom) { }

    /// <summary>Surcharge le nom de commande. Par défaut : PascalCase(id) + "Command".</summary>
    public MetierBuilder Commande(string nom) { _commande = nom; return this; }

    /// <summary>Paramètre depuis la variable du même nom (cas le plus fréquent).</summary>
    public MetierBuilder Param(string nom) => Param(nom, Src.Var(nom));

    /// <summary>Paramètre avec source explicite — Src.Var / Src.Val / Src.Query.</summary>
    public MetierBuilder Param(string nom, ISourceParametre source) { _params[nom] = source; return this; }

    public override NoeudMetier Build() => new()
    {
        Id           = _id,
        Nom          = _nom,
        EstFinale    = _final,
        FluxSortants = _flux,
        NomCommande  = string.IsNullOrEmpty(_commande) ? NomParDefaut(_id, "Command") : _commande,
        Parametres   = _params
    };
}

// ── InteractifBuilder ─────────────────────────────────────────────────────────

public class InteractifBuilder : NoeudBuilder<InteractifBuilder, NoeudInteractif>
{
    private DefinitionTache _tache = new();
    private DefinitionCommande? _commandePre;
    private DefinitionCommande? _commandePost;

    public InteractifBuilder(string id, string nom) : base(id, nom) { }

    public InteractifBuilder Tache(string titre, string? description = null)
    {
        _tache = new DefinitionTache { Titre = titre, Description = description };
        return this;
    }

    /// <summary>Commande exécutée à la suspension. Nom par défaut : PascalCase(id) + "PreCommand".</summary>
    public InteractifBuilder CommandePre(string? nom = null, Action<CommandeBuilder>? config = null)
    {
        var b = new CommandeBuilder(nom ?? NomParDefaut(_id, "PreCommand"));
        config?.Invoke(b);
        _commandePre = b.Build();
        return this;
    }

    /// <summary>Commande exécutée à la reprise. Nom par défaut : PascalCase(id) + "PostCommand".</summary>
    public InteractifBuilder CommandePost(string? nom = null, Action<CommandeBuilder>? config = null)
    {
        var b = new CommandeBuilder(nom ?? NomParDefaut(_id, "PostCommand"));
        config?.Invoke(b);
        _commandePost = b.Build();
        return this;
    }

    public override NoeudInteractif Build() => new()
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

// ── DecisionBuilder ───────────────────────────────────────────────────────────

public class DecisionBuilder : NoeudBuilder<DecisionBuilder, NoeudDecision>
{
    public DecisionBuilder(string id, string nom) : base(id, nom) { }

    // Conditions sur variable ─────────────────────────────────────────────────

    public FluxBuilder SiEgal(string variable, object? valeur)
        => Flux(new ConditionVariable(variable, Operateur.Egal, valeur));

    public FluxBuilder SiDiff(string variable, object? valeur)
        => Flux(new ConditionVariable(variable, Operateur.Different, valeur));

    public FluxBuilder SiSup(string variable, object? valeur)
        => Flux(new ConditionVariable(variable, Operateur.Superieur, valeur));

    public FluxBuilder SiInf(string variable, object? valeur)
        => Flux(new ConditionVariable(variable, Operateur.Inferieur, valeur));

    public FluxBuilder SiSupEgal(string variable, object? valeur)
        => Flux(new ConditionVariable(variable, Operateur.SuperieurOuEgal, valeur));

    public FluxBuilder SiInfEgal(string variable, object? valeur)
        => Flux(new ConditionVariable(variable, Operateur.InferieurOuEgal, valeur));

    public FluxBuilder SiContient(string variable, string valeur)
        => Flux(new ConditionVariable(variable, Operateur.Contient, valeur));

    // Condition query ─────────────────────────────────────────────────────────

    /// <summary>
    /// Condition query. Nom par défaut : PascalCase(nodeId) + "Query".
    /// Paramètres optionnels via lambda.
    /// </summary>
    public FluxBuilder SiQuery(string? nom = null, Action<CommandeBuilder>? config = null)
    {
        var nomQuery = nom ?? NomParDefaut(_id, "Query");
        Dictionary<string, ISourceParametre>? p = null;
        if (config is not null)
        {
            var b = new CommandeBuilder(nomQuery);
            config(b);
            var built = b.Build();
            p = built.Parametres.Count > 0 ? built.Parametres : null;
        }
        return Flux(new ConditionQuery(nomQuery, p));
    }

    public FluxBuilder Defaut()
    {
        var flux = new FluxSortant { EstParDefaut = true };
        _flux.Add(flux);
        return new FluxBuilder(this, flux);
    }

    public override NoeudDecision Build() => new()
    {
        Id           = _id,
        Nom          = _nom,
        EstFinale    = _final,
        FluxSortants = _flux
    };

    private FluxBuilder Flux(ICondition condition)
    {
        var flux = new FluxSortant { Condition = condition };
        _flux.Add(flux);
        return new FluxBuilder(this, flux);
    }
}

/// <summary>Builder intermédiaire pour chaîner .Vers() après une condition.</summary>
public class FluxBuilder
{
    private readonly DecisionBuilder _parent;
    private readonly FluxSortant _flux;

    public FluxBuilder(DecisionBuilder parent, FluxSortant flux) { _parent = parent; _flux = flux; }

    public DecisionBuilder Vers(string id) { _flux.Vers = id; return _parent; }
}

// ── AttenteTempsBuilder ───────────────────────────────────────────────────────

public class AttenteTempsBuilder : NoeudBuilder<AttenteTempsBuilder, NoeudAttenteTemps>
{
    private ISourceParametre _echeance = new SourceValeurStatique(null);

    public AttenteTempsBuilder(string id, string nom) : base(id, nom) { }

    /// <summary>Échéance depuis une variable du processus.</summary>
    public AttenteTempsBuilder Echeance(string variable)
        { _echeance = Src.Var(variable); return this; }

    /// <summary>Échéance statique.</summary>
    public AttenteTempsBuilder Echeance(DateTime date)
        { _echeance = Src.Val(date); return this; }

    /// <summary>Échéance calculée par query, avec paramètres optionnels.</summary>
    public AttenteTempsBuilder EcheanceQuery(string nom, Action<CommandeBuilder>? config = null)
    {
        Dictionary<string, ISourceParametre>? p = null;
        if (config is not null)
        {
            var b = new CommandeBuilder(nom);
            config(b);
            var built = b.Build();
            p = built.Parametres.Count > 0 ? built.Parametres : null;
        }
        _echeance = new SourceQuery(nom, p);
        return this;
    }

    public override NoeudAttenteTemps Build() => new()
    {
        Id                  = _id,
        Nom                 = _nom,
        EstFinale           = _final,
        FluxSortants        = _flux,
        SourceDateEcheance  = _echeance
    };
}

// ── SousProcessusBuilder ──────────────────────────────────────────────────────

public class SousProcessusBuilder : NoeudBuilder<SousProcessusBuilder, NoeudSousProcessus>
{
    private string _cle = string.Empty;
    private int _version = 1;
    private readonly List<string> _sorties = new();

    public SousProcessusBuilder(string id, string nom) : base(id, nom) { }

    public SousProcessusBuilder Definition(string cle, int version = 1)
        { _cle = cle; _version = version; return this; }

    /// <summary>Variable de sortie remontée au processus parent.</summary>
    public SousProcessusBuilder Sortie(string variable)
        { _sorties.Add(variable); return this; }

    /// <summary>Plusieurs variables de sortie en un seul appel.</summary>
    public SousProcessusBuilder Sorties(params string[] variables)
        { _sorties.AddRange(variables); return this; }

    public override NoeudSousProcessus Build() => new()
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

// ── CommandeBuilder ───────────────────────────────────────────────────────────

/// <summary>Builder de paramètres pour commandes (pre/post) et queries.</summary>
public class CommandeBuilder
{
    private readonly string _nom;
    private readonly Dictionary<string, ISourceParametre> _params = new();

    public CommandeBuilder(string nom) => _nom = nom;

    /// <summary>Paramètre depuis la variable du même nom (cas le plus fréquent).</summary>
    public CommandeBuilder Param(string nom) => Param(nom, Src.Var(nom));

    /// <summary>Paramètre avec source explicite — Src.Var / Src.Val / Src.Query.</summary>
    public CommandeBuilder Param(string nom, ISourceParametre source) { _params[nom] = source; return this; }

    public DefinitionCommande Build() => new() { NomCommande = _nom, Parametres = _params };
}
