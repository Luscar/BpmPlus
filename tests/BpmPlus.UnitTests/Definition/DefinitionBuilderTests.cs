using BpmPlus.Abstractions;
using BpmPlus.Core.Definition;
using FluentAssertions;
using Xunit;

namespace BpmPlus.UnitTests.Definition;

public class DefinitionBuilderTests
{
    [Fact]
    public void Build_ProcessusLineaire_CreesDefinitionCorrectement()
    {
        var def = DefinitionBuilder.Definir("cmd-test")
            .Intitule("Test Commande")
            .Commence("start")
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
        var act = () => DefinitionBuilder.Definir("").Commence("x").Metier("x").Build();
        act.Should().Throw<InvalidOperationException>().WithMessage("*clé*");
    }

    [Fact]
    public void Build_SansDebut_LanceInvalidOperation()
    {
        var act = () => DefinitionBuilder.Definir("cle").Metier("n").Build();
        act.Should().Throw<InvalidOperationException>().WithMessage("*début*");
    }

    [Fact]
    public void Build_SansNoeuds_LanceInvalidOperation()
    {
        var act = () => DefinitionBuilder.Definir("cle").Commence("start").Build();
        act.Should().Throw<InvalidOperationException>().WithMessage("*nœud*");
    }

    [Fact]
    public void Build_NoeudDebutAbsent_LanceInvalidOperation()
    {
        var act = () => DefinitionBuilder.Definir("cle")
            .Commence("missing")
            .Metier("autre")
            .Build();
        act.Should().Throw<InvalidOperationException>().WithMessage("*missing*");
    }

    [Fact]
    public void Metier_NomCommandeParDefaut_EstPascalCaseAvecSuffixeCommand()
    {
        var def = DefinitionBuilder.Definir("p")
            .Commence("valider-commande")
            .Metier("valider-commande")
            .Build();

        var noeud = def.Noeuds.OfType<NoeudMetier>().First();
        noeud.NomCommande.Should().Be("ValiderCommandeCommand");
    }

    [Fact]
    public void Metier_AvecNomCommandeExplicite_UtiliseCeNom()
    {
        var def = DefinitionBuilder.Definir("p")
            .Commence("n")
            .Metier("n", "Nom", b => b.Commande("MaCommandeSpeciale"))
            .Build();

        var noeud = def.Noeuds.OfType<NoeudMetier>().First();
        noeud.NomCommande.Should().Be("MaCommandeSpeciale");
    }

    [Fact]
    public void Metier_AvecParametres_StockeLesParametres()
    {
        var def = DefinitionBuilder.Definir("p")
            .Commence("n")
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
        var def = DefinitionBuilder.Definir("p")
            .Commence("start")
            .Metier("start", "Start", "check")
            .Decision("check", d => d
                .SiVariable("statut").EstEgalA("ok").Aller("fin-ok")
                .Sinon.Aller("fin-ko"))
            .Metier("fin-ok")
            .Metier("fin-ko")
            .Build();

        var decision = def.Noeuds.OfType<NoeudDecision>().First();
        decision.FluxSortants.Should().HaveCount(2);
        decision.FluxSortants.Should().ContainSingle(f => f.EstParDefaut);
        decision.FluxSortants.Should().ContainSingle(f =>
            f.Condition is ConditionVariable && ((ConditionVariable)f.Condition).Operateur == Operateur.Egal);
    }

    [Fact]
    public void AttenteSignal_SansVers_EstFinaleEtSignalNomCorrect()
    {
        var def = DefinitionBuilder.Definir("p")
            .Commence("wait")
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
        var def = DefinitionBuilder.Definir("p")
            .Commence("wait")
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
        var def = DefinitionBuilder.Definir("p")
            .Commence("tache")
            .Interactif("tache", ib => ib.Titre("Valider le dossier").Vers("fin"))
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
        var def = DefinitionBuilder.Definir("p")
            .Commence("attente")
            .AttenteTemps("attente", b => b.EcheanceFixe(echeance).Vers("suite"))
            .Metier("suite")
            .Build();

        var noeud = def.Noeuds.OfType<NoeudAttenteTemps>().First();
        noeud.SourceDateEcheance.Should().BeOfType<SourceValeurStatique>()
            .Which.Valeur.Should().Be(echeance);
    }

    [Fact]
    public void SousProcessus_AvecDefinitionEtSorties_CreesNoeudCorrect()
    {
        var def = DefinitionBuilder.Definir("parent")
            .Commence("sp")
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
