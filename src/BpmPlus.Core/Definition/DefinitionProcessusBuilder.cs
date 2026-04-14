using BpmPlus.Abstractions;

namespace BpmPlus.Core.Definition;

/// <summary>
/// Builder Fluent pour construire une DefinitionProcessus.
/// </summary>
public class DefinitionProcessusBuilder
{
    private readonly string _cle;
    private string _nom = string.Empty;
    private string _noeudDebutId = string.Empty;
    private readonly List<NoeudProcessus> _noeuds = new();

    public DefinitionProcessusBuilder(string cle) => _cle = cle;

    public DefinitionProcessusBuilder Nommer(string nom) { _nom = nom; return this; }

    public DefinitionProcessusBuilder Debuter(string idPremierNoeud)
    {
        _noeudDebutId = idPremierNoeud;
        return this;
    }

    public DefinitionProcessusBuilder AjouterNoeudMetier(string id, Action<NoeudMetierBuilder> configure)
    {
        var builder = new NoeudMetierBuilder(id);
        configure(builder);
        _noeuds.Add(builder.Construire());
        return this;
    }

    public DefinitionProcessusBuilder AjouterNoeudInteractif(string id, Action<NoeudInteractifBuilder> configure)
    {
        var builder = new NoeudInteractifBuilder(id);
        configure(builder);
        _noeuds.Add(builder.Construire());
        return this;
    }

    public DefinitionProcessusBuilder AjouterNoeudDecision(string id, Action<NoeudDecisionBuilder> configure)
    {
        var builder = new NoeudDecisionBuilder(id);
        configure(builder);
        _noeuds.Add(builder.Construire());
        return this;
    }

    public DefinitionProcessusBuilder AjouterNoeudAttenteTemps(string id, Action<NoeudAttenteTempsBuilder> configure)
    {
        var builder = new NoeudAttenteTempsBuilder(id);
        configure(builder);
        _noeuds.Add(builder.Construire());
        return this;
    }

    public DefinitionProcessusBuilder AjouterNoeudAttenteSignal(string id, Action<NoeudAttenteSignalBuilder> configure)
    {
        var builder = new NoeudAttenteSignalBuilder(id);
        configure(builder);
        _noeuds.Add(builder.Construire());
        return this;
    }

    public DefinitionProcessusBuilder AjouterNoeudSousProcessus(string id, Action<NoeudSousProcessusBuilder> configure)
    {
        var builder = new NoeudSousProcessusBuilder(id);
        configure(builder);
        _noeuds.Add(builder.Construire());
        return this;
    }

    public DefinitionProcessus Construire()
    {
        if (string.IsNullOrWhiteSpace(_cle))
            throw new InvalidOperationException("La clé de la définition est obligatoire.");
        if (string.IsNullOrWhiteSpace(_noeudDebutId))
            throw new InvalidOperationException("Le nœud de début est obligatoire (appeler Debuter()).");
        if (_noeuds.Count == 0)
            throw new InvalidOperationException("La définition doit contenir au moins un nœud.");
        if (_noeuds.All(n => n.Id != _noeudDebutId))
            throw new InvalidOperationException(
                $"Aucun nœud avec l'ID '{_noeudDebutId}' (nœud de début) n'a été ajouté.");

        return new DefinitionProcessus
        {
            Cle = _cle,
            Nom = _nom,
            NoeudDebutId = _noeudDebutId,
            Noeuds = new List<NoeudProcessus>(_noeuds),
            DateCreation = DateTime.UtcNow
        };
    }
}

// ── Builders de nœuds ──────────────────────────────────────────────────────

public abstract class NoeudBaseBuilder<TBuilder, TNoeud>
    where TBuilder : NoeudBaseBuilder<TBuilder, TNoeud>
    where TNoeud : NoeudProcessus
{
    protected readonly string _id;
    protected string _nom = string.Empty;
    protected bool _estFinale;
    protected readonly List<FluxSortant> _fluxSortants = new();

    protected NoeudBaseBuilder(string id) => _id = id;

    public TBuilder Nommer(string nom) { _nom = nom; return (TBuilder)this; }

    public TBuilder EstFinale() { _estFinale = true; return (TBuilder)this; }

    public TBuilder Vers(string idNoeudSuivant)
    {
        _fluxSortants.Add(new FluxSortant { Vers = idNoeudSuivant });
        return (TBuilder)this;
    }

    public abstract TNoeud Construire();
}

public class NoeudMetierBuilder : NoeudBaseBuilder<NoeudMetierBuilder, NoeudMetier>
{
    private string _nomCommande = string.Empty;
    private ISourceParametre? _sourceAggregateId;
    private readonly Dictionary<string, ISourceParametre> _parametres = new();

    public NoeudMetierBuilder(string id) : base(id) { }

    public NoeudMetierBuilder CommandeNommee(string nomCommande)
    {
        _nomCommande = nomCommande;
        return this;
    }

    public NoeudMetierBuilder AggregateIdDepuisVariable(string nomVariable)
    {
        _sourceAggregateId = new SourceVariable(nomVariable);
        return this;
    }

    public NoeudMetierBuilder AggregateIdStatique(long aggregateId)
    {
        _sourceAggregateId = new SourceValeurStatique(aggregateId);
        return this;
    }

    public NoeudMetierBuilder ParametreDepuisVariable(string nomParam, string nomVariable)
    {
        _parametres[nomParam] = new SourceVariable(nomVariable);
        return this;
    }

    public NoeudMetierBuilder ParametreStatique(string nomParam, object? valeur)
    {
        _parametres[nomParam] = new SourceValeurStatique(valeur);
        return this;
    }

    public override NoeudMetier Construire() => new()
    {
        Id = _id,
        Nom = _nom,
        EstFinale = _estFinale,
        FluxSortants = _fluxSortants,
        NomCommande = _nomCommande,
        SourceAggregateId = _sourceAggregateId,
        Parametres = _parametres
    };
}

public class NoeudInteractifBuilder : NoeudBaseBuilder<NoeudInteractifBuilder, NoeudInteractif>
{
    private DefinitionTache _definitionTache = new();
    private DefinitionCommande? _commandePre;
    private DefinitionCommande? _commandePost;

    public NoeudInteractifBuilder(string id) : base(id) { }

    public NoeudInteractifBuilder DefinirTache(Action<TacheBuilder> configure)
    {
        var builder = new TacheBuilder();
        configure(builder);
        _definitionTache = builder.Construire();
        return this;
    }

    public NoeudInteractifBuilder AvecCommandePre(string nomCommande, Action<CommandeBuilder>? configure = null)
    {
        var builder = new CommandeBuilder(nomCommande);
        configure?.Invoke(builder);
        _commandePre = builder.Construire();
        return this;
    }

    public NoeudInteractifBuilder AvecCommandePost(string nomCommande, Action<CommandeBuilder>? configure = null)
    {
        var builder = new CommandeBuilder(nomCommande);
        configure?.Invoke(builder);
        _commandePost = builder.Construire();
        return this;
    }

    public override NoeudInteractif Construire() => new()
    {
        Id = _id,
        Nom = _nom,
        EstFinale = _estFinale,
        FluxSortants = _fluxSortants,
        DefinitionTache = _definitionTache,
        CommandePre = _commandePre,
        CommandePost = _commandePost
    };
}

public class NoeudDecisionBuilder : NoeudBaseBuilder<NoeudDecisionBuilder, NoeudDecision>
{
    public NoeudDecisionBuilder(string id) : base(id) { }

    public FluxSortantBuilder SiVariable(string nomVariable, Operateur operateur, object? valeur)
    {
        var flux = new FluxSortant
        {
            Condition = new ConditionVariable(nomVariable, operateur, valeur)
        };
        _fluxSortants.Add(flux);
        return new FluxSortantBuilder(this, flux);
    }

    public FluxSortantBuilder SiQuery(string nomQuery,
        ISourceParametre? sourceAggregateId = null,
        Dictionary<string, ISourceParametre>? parametres = null)
    {
        var flux = new FluxSortant
        {
            Condition = new ConditionQuery(nomQuery, sourceAggregateId, parametres)
        };
        _fluxSortants.Add(flux);
        return new FluxSortantBuilder(this, flux);
    }

    public FluxSortantBuilder ParDefaut()
    {
        var flux = new FluxSortant { EstParDefaut = true };
        _fluxSortants.Add(flux);
        return new FluxSortantBuilder(this, flux);
    }

    // Override Vers n'est pas applicable sur un NoeudDecision sans condition
    public override NoeudDecision Construire() => new()
    {
        Id = _id,
        Nom = _nom,
        EstFinale = _estFinale,
        FluxSortants = _fluxSortants
    };
}

public class FluxSortantBuilder
{
    private readonly NoeudDecisionBuilder _parent;
    private readonly FluxSortant _flux;

    public FluxSortantBuilder(NoeudDecisionBuilder parent, FluxSortant flux)
    {
        _parent = parent;
        _flux = flux;
    }

    public NoeudDecisionBuilder Vers(string idNoeudSuivant)
    {
        _flux.Vers = idNoeudSuivant;
        return _parent;
    }
}

public class NoeudAttenteTempsBuilder : NoeudBaseBuilder<NoeudAttenteTempsBuilder, NoeudAttenteTemps>
{
    private ISourceParametre _sourceDate = new SourceValeurStatique(null);

    public NoeudAttenteTempsBuilder(string id) : base(id) { }

    public NoeudAttenteTempsBuilder EcheanceDepuisVariable(string nomVariable)
    {
        _sourceDate = new SourceVariable(nomVariable);
        return this;
    }

    public NoeudAttenteTempsBuilder EcheanceStatique(DateTime date)
    {
        _sourceDate = new SourceValeurStatique(date);
        return this;
    }

    public NoeudAttenteTempsBuilder EcheanceDepuisQuery(string nomQuery,
        ISourceParametre? sourceAggregateId = null)
    {
        _sourceDate = new SourceQuery(nomQuery, sourceAggregateId);
        return this;
    }

    public override NoeudAttenteTemps Construire() => new()
    {
        Id = _id,
        Nom = _nom,
        EstFinale = _estFinale,
        FluxSortants = _fluxSortants,
        SourceDateEcheance = _sourceDate
    };
}

public class NoeudAttenteSignalBuilder : NoeudBaseBuilder<NoeudAttenteSignalBuilder, NoeudAttenteSignal>
{
    private string _nomSignal = string.Empty;

    public NoeudAttenteSignalBuilder(string id) : base(id) { }

    public NoeudAttenteSignalBuilder Signal(string nomSignal) { _nomSignal = nomSignal; return this; }

    public override NoeudAttenteSignal Construire() => new()
    {
        Id = _id,
        Nom = _nom,
        EstFinale = _estFinale,
        FluxSortants = _fluxSortants,
        NomSignal = _nomSignal
    };
}

public class NoeudSousProcessusBuilder : NoeudBaseBuilder<NoeudSousProcessusBuilder, NoeudSousProcessus>
{
    private string _cleDefinition = string.Empty;
    private int _version;
    private readonly List<string> _variablesSorties = new();

    public NoeudSousProcessusBuilder(string id) : base(id) { }

    public NoeudSousProcessusBuilder DefinitionEnfant(string cle, int version)
    {
        _cleDefinition = cle;
        _version = version;
        return this;
    }

    public NoeudSousProcessusBuilder SortieVariable(string nomVariable)
    {
        _variablesSorties.Add(nomVariable);
        return this;
    }

    public override NoeudSousProcessus Construire() => new()
    {
        Id = _id,
        Nom = _nom,
        EstFinale = _estFinale,
        FluxSortants = _fluxSortants,
        CleDefinition = _cleDefinition,
        Version = _version,
        VariablesSorties = _variablesSorties
    };
}

// ── Builders auxiliaires ──────────────────────────────────────────────────

public class TacheBuilder
{
    private string _titre = string.Empty;
    private string? _description;

    public TacheBuilder Titre(string titre) { _titre = titre; return this; }
    public TacheBuilder Description(string description) { _description = description; return this; }

    public DefinitionTache Construire() => new() { Titre = _titre, Description = _description };
}

public class CommandeBuilder
{
    private readonly string _nomCommande;
    private ISourceParametre? _sourceAggregateId;
    private readonly Dictionary<string, ISourceParametre> _parametres = new();

    public CommandeBuilder(string nomCommande) => _nomCommande = nomCommande;

    public CommandeBuilder AggregateIdDepuisVariable(string nomVariable)
    {
        _sourceAggregateId = new SourceVariable(nomVariable);
        return this;
    }

    public CommandeBuilder AggregateIdStatique(long aggregateId)
    {
        _sourceAggregateId = new SourceValeurStatique(aggregateId);
        return this;
    }

    public CommandeBuilder ParametreDepuisVariable(string nomParam, string nomVariable)
    {
        _parametres[nomParam] = new SourceVariable(nomVariable);
        return this;
    }

    public DefinitionCommande Construire() => new()
    {
        NomCommande = _nomCommande,
        SourceAggregateId = _sourceAggregateId,
        Parametres = _parametres
    };
}
