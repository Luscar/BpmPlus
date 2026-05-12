using BpmPlus.Abstractions;
using BpmPlus.Core.Definition;
using FluentAssertions;
using Xunit;

namespace BpmPlus.UnitTests.Definition;

public class ProcessusBuilderTests
{
    [Fact]
    public void Build_ProcessusLineaire_CreesDefinitionCorrectement()
    {
        var def = new ProcessusBuilder("cmd-test", "Test Commande")
            .Debut("start")
            .Metier("start", "Valider", "fin")
            .Metier("fin")
            .Build();

        def.Cle.Should().Be("cmd-test");
        def.Nom.Should().Be("Test Commande");
        def.NoeudDebutId.Should().Be("start");
        def.Noeuds.Should().HaveCount(2);
    }

    [Fact]
    public void Build_SansCle_LanceInvalidOperation()
    {
        var act = () => new ProcessusBuilder("").Debut("x").Metier("x").Build();
        act.Should().Throw<InvalidOperationException>().WithMessage("*clé*");
    }

    [Fact]
    public void Build_SansDebut_LanceInvalidOperation()
    {
        var act = () => new ProcessusBuilder("cle").Metier("n").Build();
        act.Should().Throw<InvalidOperationException>().WithMessage("*début*");
    }

    [Fact]
    public void Build_SansNoeuds_LanceInvalidOperation()
    {
        var act = () => new ProcessusBuilder("cle").Debut("start").Build();
        act.Should().Throw<InvalidOperationException>().WithMessage("*nœud*");
    }

    [Fact]
    public void Build_NoeudDebutAbsent_LanceInvalidOperation()
    {
        var act = () => new ProcessusBuilder("cle")
            .Debut("missing")
            .Metier("autre")
            .Build();
        act.Should().Throw<InvalidOperationException>().WithMessage("*missing*");
    }

    [Fact]
    public void Metier_NomCommandeParDefaut_EstPascalCaseAvecSuffixeCommand()
    {
        var def = new ProcessusBuilder("p")
            .Debut("valider-commande")
            .Metier("valider-commande")
            .Build();

        var noeud = def.Noeuds.OfType<NoeudMetier>().First();
        noeud.NomCommande.Should().Be("ValiderCommandeCommand");
    }

    [Fact]
    public void Metier_AvecNomCommandeExplicite_UtiliseCeNom()
    {
        var def = new ProcessusBuilder("p")
            .Debut("n")
            .Metier("n", "Nom", b => b.Commande("MaCommandeSpeciale"))
            .Build();

        var noeud = def.Noeuds.OfType<NoeudMetier>().First();
        noeud.NomCommande.Should().Be("MaCommandeSpeciale");
    }

    [Fact]
    public void Metier_AvecParametres_StockeLesParametres()
    {
        var def = new ProcessusBuilder("p")
            .Debut("n")
            .Metier("n", b => b
                .Param("montant")
                .Param("devise", Src.Val("EUR")))
            .Build();

        var noeud = def.Noeuds.OfType<NoeudMetier>().First();
        noeud.Parametres.Should().ContainKey("montant");
        noeud.Parametres.Should().ContainKey("devise");
        noeud.Parametres["devise"].Should().BeOfType<SourceValeurStatique>()
            .Which.Valeur.Should().Be("EUR");
    }

    [Fact]
    public void Decision_AvecConditionEtDefaut_CreesFluxCorrects()
    {
        var def = new ProcessusBuilder("p")
            .Debut("start")
            .Metier("start", "Start", "check")
            .Decision("check", d => d
                .SiEgal("statut", "ok").Vers("fin-ok")
                .Defaut().Vers("fin-ko"))
            .Metier("fin-ok")
            .Metier("fin-ko")
            .Build();

        var decision = def.Noeuds.OfType<NoeudDecision>().First();
        decision.FluxSortants.Should().HaveCount(2);
        decision.FluxSortants.Should().ContainSingle(f => f.EstParDefaut);
        decision.FluxSortants.Should().ContainSingle(f =>
            f.Condition is ConditionVariable cv && cv.Operateur == Operateur.Egal);
    }

    [Fact]
    public void AttenteSignal_SansVers_EstFinaleEtSignalNomCorrect()
    {
        var def = new ProcessusBuilder("p")
            .Debut("wait")
            .AttenteSignal("wait", "mon-signal")
            .Build();

        var noeud = def.Noeuds.OfType<NoeudAttenteSignal>().First();
        noeud.NomSignal.Should().Be("mon-signal");
        noeud.EstFinale.Should().BeTrue();
        noeud.FluxSortants.Should().BeEmpty();
    }

    [Fact]
    public void AttenteSignal_AvecVers_NEstPasFinale()
    {
        var def = new ProcessusBuilder("p")
            .Debut("wait")
            .AttenteSignal("wait", "signal-1", "suite")
            .Metier("suite")
            .Build();

        var noeud = def.Noeuds.OfType<NoeudAttenteSignal>().First();
        noeud.EstFinale.Should().BeFalse();
        noeud.FluxSortants.Should().ContainSingle(f => f.Vers == "suite");
    }

    [Fact]
    public void Interactif_AvecTacheEtVers_CreesNoeudCorrect()
    {
        var def = new ProcessusBuilder("p")
            .Debut("tache")
            .Interactif("tache", ib => ib.Tache("Valider le dossier").Vers("fin"))
            .Metier("fin")
            .Build();

        var noeud = def.Noeuds.OfType<NoeudInteractif>().First();
        noeud.DefinitionTache.Titre.Should().Be("Valider le dossier");
        noeud.FluxSortants.Should().ContainSingle(f => f.Vers == "fin");
        noeud.EstFinale.Should().BeFalse();
    }

    [Fact]
    public void AttenteTemps_AvecEcheanceStatique_CreesNoeudCorrect()
    {
        var echeance = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var def = new ProcessusBuilder("p")
            .Debut("attente")
            .AttenteTemps("attente", b => b.Echeance(echeance).Vers("suite"))
            .Metier("suite")
            .Build();

        var noeud = def.Noeuds.OfType<NoeudAttenteTemps>().First();
        noeud.SourceDateEcheance.Should().BeOfType<SourceValeurStatique>()
            .Which.Valeur.Should().Be(echeance);
    }

    [Fact]
    public void SousProcessus_AvecDefinitionEtSorties_CreesNoeudCorrect()
    {
        var def = new ProcessusBuilder("parent")
            .Debut("sp")
            .SousProcessus("sp", b => b
                .Definition("enfant", 1)
                .Sorties("resultat", "erreur")
                .Vers("fin"))
            .Metier("fin")
            .Build();

        var noeud = def.Noeuds.OfType<NoeudSousProcessus>().First();
        noeud.CleDefinition.Should().Be("enfant");
        noeud.Version.Should().Be(1);
        noeud.VariablesSorties.Should().Contain("resultat").And.Contain("erreur");
    }
}
