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

    /// <summary>Crée le builder avec la clé et le nom en une seule instruction.</summary>
    public DefinitionProcessusBuilder(string cle, string nom) { _cle = cle; _nom = nom; }

    /// <summary>Crée le builder avec clé, nom et nœud de début en une seule instruction.</summary>
    public DefinitionProcessusBuilder(string cle, string nom, string noeudDebutId)
    {
        _cle = cle;
        _nom = nom;
        _noeudDebutId = noeudDebutId;
    }

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

    public DefinitionProcessusBuilder AjouterNoeudMetier(string id, string nom, Action<NoeudMetierBuilder> configure)
    {
        var builder = new NoeudMetierBuilder(id);
        builder.Nommer(nom);
        configure(builder);
        _noeuds.Add(builder.Construire());
        return this;
    }

    /// <summary>
    /// Nœud métier ultra-compact : l'id du nœud sert de nom de commande et l'aggregate id
    /// est lu depuis la variable <paramref name="aggregateIdVariable"/>.
    /// Si <paramref name="vers"/> est omis, le nœud est marqué EstFinale.
    /// </summary>
    public DefinitionProcessusBuilder AjouterNoeudMetier(
        string id, string aggregateIdVariable, string? vers = null)
    {
        var builder = new NoeudMetierBuilder(id);
        builder.CommandeNommee(id, aggregateIdVariable);
        if (vers is not null)
            builder.Vers(vers);
        else
            builder.EstFinale();
        _noeuds.Add(builder.Construire());
        return this;
    }

    /// <summary>
    /// Nœud métier compact avec nom d'affichage : l'id du nœud sert de nom de commande et l'aggregate id
    /// est lu depuis la variable <paramref name="aggregateIdVariable"/>.
    /// Si <paramref name="vers"/> est omis, le nœud est marqué EstFinale.
    /// </summary>
    public DefinitionProcessusBuilder AjouterNoeudMetier(
        string id, string nom, string aggregateIdVariable, string? vers = null)
    {
        var builder = new NoeudMetierBuilder(id);
        builder.Nommer(nom);
        builder.CommandeNommee(id, aggregateIdVariable);
        if (vers is not null)
            builder.Vers(vers);
        else
            builder.EstFinale();
        _noeuds.Add(builder.Construire());
        return this;
    }

    /// <summary>
    /// Nœud métier compact sans lambda. Si <paramref name="vers"/> est omis, le nœud est marqué EstFinale.
    /// </summary>
    public DefinitionProcessusBuilder AjouterNoeudMetier(
        string id, string nom, string commande, string aggregateIdVariable, string? vers = null)
    {
        var builder = new NoeudMetierBuilder(id);
        builder.Nommer(nom);
        builder.CommandeNommee(commande, aggregateIdVariable);
        if (vers is not null)
            builder.Vers(vers);
        else
            builder.EstFinale();
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

    public DefinitionProcessusBuilder AjouterNoeudInteractif(string id, string nom, Action<NoeudInteractifBuilder> configure)
    {
        var builder = new NoeudInteractifBuilder(id);
        builder.Nommer(nom);
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

    public DefinitionProcessusBuilder AjouterNoeudDecision(string id, string nom, Action<NoeudDecisionBuilder> configure)
    {
        var builder = new NoeudDecisionBuilder(id);
        builder.Nommer(nom);
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

    public DefinitionProcessusBuilder AjouterNoeudAttenteTemps(string id, string nom, Action<NoeudAttenteTempsBuilder> configure)
    {
        var builder = new NoeudAttenteTempsBuilder(id);
        builder.Nommer(nom);
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

    public DefinitionProcessusBuilder AjouterNoeudAttenteSignal(string id, string nom, Action<NoeudAttenteSignalBuilder> configure)
    {
        var builder = new NoeudAttenteSignalBuilder(id);
        builder.Nommer(nom);
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

    public DefinitionProcessusBuilder AjouterNoeudSousProcessus(string id, string nom, Action<NoeudSousProcessusBuilder> configure)
    {
        var builder = new NoeudSousProcessusBuilder(id);
        builder.Nommer(nom);
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

    /// <summary>Définit la commande et l'aggregate id (depuis une variable) en un seul appel.</summary>
    public NoeudMetierBuilder CommandeNommee(string nomCommande, string aggregateIdVariable)
    {
        _nomCommande = nomCommande;
        _sourceAggregateId = new SourceVariable(aggregateIdVariable);
        return this;
    }

    /// <summary>Utilise l'id du nœud comme nom de commande.</summary>
    public NoeudMetierBuilder CommandeParId()
    {
        _nomCommande = _id;
        return this;
    }

    /// <summary>Utilise l'id du nœud comme nom de commande et lit l'aggregate id depuis la variable.</summary>
    public NoeudMetierBuilder CommandeParId(string aggregateIdVariable)
    {
        _nomCommande = _id;
        _sourceAggregateId = new SourceVariable(aggregateIdVariable);
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

    /// <summary>Définit la tâche avec titre et description sans sous-builder.</summary>
    public NoeudInteractifBuilder DefinirTache(string titre, string? description = null)
    {
        _definitionTache = new DefinitionTache { Titre = titre, Description = description };
        return this;
    }

    public NoeudInteractifBuilder AvecCommandePre(string nomCommande, Action<CommandeBuilder>? configure = null)
    {
        var builder = new CommandeBuilder(nomCommande);
        configure?.Invoke(builder);
        _commandePre = builder.Construire();
        return this;
    }

    /// <summary>Définit la commande PRE avec aggregate id (depuis une variable) en un seul appel.</summary>
    public NoeudInteractifBuilder AvecCommandePre(string nomCommande, string aggregateIdVariable)
    {
        _commandePre = new DefinitionCommande
        {
            NomCommande = nomCommande,
            SourceAggregateId = new SourceVariable(aggregateIdVariable)
        };
        return this;
    }

    public NoeudInteractifBuilder AvecCommandePost(string nomCommande, Action<CommandeBuilder>? configure = null)
    {
        var builder = new CommandeBuilder(nomCommande);
        configure?.Invoke(builder);
        _commandePost = builder.Construire();
        return this;
    }

    /// <summary>Définit la commande POST avec aggregate id (depuis une variable) en un seul appel.</summary>
    public NoeudInteractifBuilder AvecCommandePost(string nomCommande, string aggregateIdVariable)
    {
        _commandePost = new DefinitionCommande
        {
            NomCommande = nomCommande,
            SourceAggregateId = new SourceVariable(aggregateIdVariable)
        };
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

    /// <summary>Condition : variable == valeur.</summary>
    public FluxSortantBuilder SiEgal(string nomVariable, object? valeur)
        => SiVariable(nomVariable, Operateur.Egal, valeur);

    /// <summary>Condition : variable != valeur.</summary>
    public FluxSortantBuilder SiDifferent(string nomVariable, object? valeur)
        => SiVariable(nomVariable, Operateur.Different, valeur);

    /// <summary>Condition : variable > valeur.</summary>
    public FluxSortantBuilder SiSuperieur(string nomVariable, object? valeur)
        => SiVariable(nomVariable, Operateur.Superieur, valeur);

    /// <summary>Condition : variable < valeur.</summary>
    public FluxSortantBuilder SiInferieur(string nomVariable, object? valeur)
        => SiVariable(nomVariable, Operateur.Inferieur, valeur);

    /// <summary>Condition : variable contient valeur (string).</summary>
    public FluxSortantBuilder SiContient(string nomVariable, string valeur)
        => SiVariable(nomVariable, Operateur.Contient, valeur);

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

    /// <summary>Condition query avec aggregate id depuis une variable — paramètres ajoutables via ParametreQuery*.</summary>
    public FluxSortantBuilder SiQuery(string nomQuery, string aggregateIdVariable)
    {
        var flux = new FluxSortant
        {
            Condition = new ConditionQuery(nomQuery, new SourceVariable(aggregateIdVariable))
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

    /// <summary>Ajoute un paramètre (depuis une variable) à la ConditionQuery de ce flux.</summary>
    public FluxSortantBuilder ParametreQueryDepuisVariable(string nomParam, string nomVariable)
    {
        EnsureConditionQuery().Parametres![nomParam] = new SourceVariable(nomVariable);
        return this;
    }

    /// <summary>Ajoute un paramètre statique à la ConditionQuery de ce flux.</summary>
    public FluxSortantBuilder ParametreQueryStatique(string nomParam, object? valeur)
    {
        EnsureConditionQuery().Parametres![nomParam] = new SourceValeurStatique(valeur);
        return this;
    }

    public NoeudDecisionBuilder Vers(string idNoeudSuivant)
    {
        _flux.Vers = idNoeudSuivant;
        return _parent;
    }

    private ConditionQuery EnsureConditionQuery()
    {
        if (_flux.Condition is not ConditionQuery cq)
            throw new InvalidOperationException(
                "ParametreQuery* n'est disponible que sur un flux créé avec SiQuery().");

        if (cq.Parametres is null)
        {
            var avecParametres = cq with { Parametres = new Dictionary<string, ISourceParametre>() };
            _flux.Condition = avecParametres;
            return avecParametres;
        }

        return cq;
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
        _sourceDate = new SourceQuery(nomQuery, sourceAggregateId, new Dictionary<string, ISourceParametre>());
        return this;
    }

    /// <summary>Échéance via query avec aggregate id depuis une variable.</summary>
    public NoeudAttenteTempsBuilder EcheanceDepuisQuery(string nomQuery, string aggregateIdVariable)
    {
        _sourceDate = new SourceQuery(nomQuery, new SourceVariable(aggregateIdVariable), new Dictionary<string, ISourceParametre>());
        return this;
    }

    /// <summary>Ajoute un paramètre (depuis une variable) à la query d'échéance. Appeler après EcheanceDepuisQuery.</summary>
    public NoeudAttenteTempsBuilder ParametreQueryDepuisVariable(string nomParam, string nomVariable)
    {
        ((SourceQuery)_sourceDate).Parametres![nomParam] = new SourceVariable(nomVariable);
        return this;
    }

    /// <summary>Ajoute un paramètre statique à la query d'échéance. Appeler après EcheanceDepuisQuery.</summary>
    public NoeudAttenteTempsBuilder ParametreQueryStatique(string nomParam, object? valeur)
    {
        ((SourceQuery)_sourceDate).Parametres![nomParam] = new SourceValeurStatique(valeur);
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

    /// <summary>Déclare plusieurs variables de sortie en un seul appel.</summary>
    public NoeudSousProcessusBuilder SortiesVariables(params string[] noms)
    {
        _variablesSorties.AddRange(noms);
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

    public CommandeBuilder ParametreStatique(string nomParam, object? valeur)
    {
        _parametres[nomParam] = new SourceValeurStatique(valeur);
        return this;
    }

    public DefinitionCommande Construire() => new()
    {
        NomCommande = _nomCommande,
        SourceAggregateId = _sourceAggregateId,
        Parametres = _parametres
    };
}
