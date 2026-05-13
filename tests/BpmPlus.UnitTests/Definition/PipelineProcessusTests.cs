using BpmPlus.Abstractions;
using BpmPlus.Core.Definition;
using FluentAssertions;
using Xunit;

namespace BpmPlus.UnitTests.Definition;

// Commandes / queries factices — simples marqueurs de type
file class ValiderCommandeCommand;
file class EnregistrerDecisionCommand;
file class NotifierApprobationCommand;
file class NotifierRefusCommand;
file class PrepareApprobationCommand;
file class EstCommandeApprouveeQuery;
file class CalculerEcheanceQuery;

public class PipelineProcessusTests
{
    // ── Compilation de base ───────────────────────────────────────────────────

    [Fact]
    public void Compiler_ProcessusLineaire_DefinitionCorrecte()
    {
        var def = PipelineProcessus.Creer("mon-processus", "Processus de test")
            .Faire<ValiderCommandeCommand>()
            .Faire<EnregistrerDecisionCommand>()
            .Compiler();

        def.Cle.Should().Be("mon-processus");
        def.Nom.Should().Be("Processus de test");
        def.Noeuds.Should().HaveCount(2);
        def.NoeudDebutId.Should().Be(def.Noeuds.First().Id);
    }

    [Fact]
    public void Compiler_SansCle_LanceInvalidOperation()
    {
        var act = () => PipelineProcessus.Creer("")
            .Faire<ValiderCommandeCommand>()
            .Compiler();

        act.Should().Throw<InvalidOperationException>().WithMessage("*clé*");
    }

    [Fact]
    public void Compiler_SansEtapes_LanceInvalidOperation()
    {
        var act = () => PipelineProcessus.Creer("p").Compiler();

        act.Should().Throw<InvalidOperationException>().WithMessage("*étape*");
    }

    // ── Noms de commandes et IDs ──────────────────────────────────────────────

    [Fact]
    public void Faire_NomCommandeEstNomDuType()
    {
        var def = PipelineProcessus.Creer("p")
            .Faire<ValiderCommandeCommand>()
            .Compiler();

        def.Noeuds.OfType<NoeudMetier>().First()
            .NomCommande.Should().Be("ValiderCommandeCommand");
    }

    [Fact]
    public void Faire_IdDeriveDuTypeEnKebab_SansSuffixeCommand()
    {
        var def = PipelineProcessus.Creer("p")
            .Faire<ValiderCommandeCommand>()
            .Compiler();

        // ValiderCommandeCommand → ValiderCommande → valider-commande
        def.Noeuds.First().Id.Should().Be("valider-commande");
    }

    [Fact]
    public void Faire_AvecNomExplicite_UtiliseCeNom()
    {
        var def = PipelineProcessus.Creer("p")
            .Faire("MaCommandeSpeciale")
            .Compiler();

        def.Noeuds.OfType<NoeudMetier>().First()
            .NomCommande.Should().Be("MaCommandeSpeciale");
    }

    [Fact]
    public void Faire_MemeCommandeDeuxFois_IdsUniques()
    {
        var def = PipelineProcessus.Creer("p")
            .Faire<ValiderCommandeCommand>()
            .Faire<ValiderCommandeCommand>()
            .Compiler();

        var ids = def.Noeuds.Select(n => n.Id).ToList();
        ids.Should().OnlyHaveUniqueItems();
        ids.Should().Contain("valider-commande");
        ids.Should().Contain("valider-commande-2");
    }

    // ── Connexions automatiques ───────────────────────────────────────────────

    [Fact]
    public void Faire_EtapesAutoConnectees_PremierVersDeuxieme()
    {
        var def = PipelineProcessus.Creer("p")
            .Faire<ValiderCommandeCommand>()
            .Faire<EnregistrerDecisionCommand>()
            .Compiler();

        var premier  = def.Noeuds.First();
        var deuxieme = def.Noeuds.Last();

        premier.FluxSortants.Should().ContainSingle(f => f.Vers == deuxieme.Id);
        premier.EstFinale.Should().BeFalse();
    }

    [Fact]
    public void Compiler_DernierNoeudAutoMarqueTerminal()
    {
        var def = PipelineProcessus.Creer("p")
            .Faire<ValiderCommandeCommand>()
            .Faire<EnregistrerDecisionCommand>()
            .Compiler();

        def.Noeuds.Last().EstFinale.Should().BeTrue();
    }

    [Fact]
    public void Fin_MarqueDernierNoeudTerminalExplicitement()
    {
        var def = PipelineProcessus.Creer("p")
            .Faire<ValiderCommandeCommand>()
            .Fin()
            .Compiler();

        def.Noeuds.Single().EstFinale.Should().BeTrue();
    }

    // ── Paramètres ────────────────────────────────────────────────────────────

    [Fact]
    public void Faire_AvecVar_StockeSourceVariable()
    {
        var def = PipelineProcessus.Creer("p")
            .Faire<ValiderCommandeCommand>(p => p.Var("montant"))
            .Compiler();

        var noeud = def.Noeuds.OfType<NoeudMetier>().First();
        noeud.Parametres.Should().ContainKey("montant");
        noeud.Parametres["montant"].Should().BeOfType<SourceVariable>()
            .Which.NomVariable.Should().Be("montant");
    }

    [Fact]
    public void Faire_AvecVarRenomme_StockeSourceVariableAvecBonNom()
    {
        var def = PipelineProcessus.Creer("p")
            .Faire<ValiderCommandeCommand>(p => p.Var("montantParam", "montantVariable"))
            .Compiler();

        var noeud = def.Noeuds.OfType<NoeudMetier>().First();
        noeud.Parametres["montantParam"].Should().BeOfType<SourceVariable>()
            .Which.NomVariable.Should().Be("montantVariable");
    }

    [Fact]
    public void Faire_AvecVal_StockeValeurStatique()
    {
        var def = PipelineProcessus.Creer("p")
            .Faire<ValiderCommandeCommand>(p => p.Val("devise", "EUR"))
            .Compiler();

        var noeud = def.Noeuds.OfType<NoeudMetier>().First();
        noeud.Parametres["devise"].Should().BeOfType<SourceValeurStatique>()
            .Which.Valeur.Should().Be("EUR");
    }

    [Fact]
    public void Faire_AvecRequete_StockeSourceQuery()
    {
        var def = PipelineProcessus.Creer("p")
            .Faire<ValiderCommandeCommand>(p => p.Requete<CalculerEcheanceQuery>("echeance"))
            .Compiler();

        var noeud = def.Noeuds.OfType<NoeudMetier>().First();
        noeud.Parametres["echeance"].Should().BeOfType<SourceQuery>()
            .Which.NomQuery.Should().Be("CalculerEcheanceQuery");
    }

    [Fact]
    public void Faire_AvecRequeteExplicite_StockeSourceQuery()
    {
        var def = PipelineProcessus.Creer("p")
            .Faire<ValiderCommandeCommand>(p => p.Requete("echeance", "MonQueryPersonnalise"))
            .Compiler();

        var noeud = def.Noeuds.OfType<NoeudMetier>().First();
        noeud.Parametres["echeance"].Should().BeOfType<SourceQuery>()
            .Which.NomQuery.Should().Be("MonQueryPersonnalise");
    }

    // ── Tâche interactive ─────────────────────────────────────────────────────

    [Fact]
    public void Tache_SansConfig_CreesNoeudInteractifAvecTitre()
    {
        var def = PipelineProcessus.Creer("p")
            .Tache("Approuver la commande")
            .Compiler();

        var noeud = def.Noeuds.OfType<NoeudInteractif>().First();
        noeud.DefinitionTache.Titre.Should().Be("Approuver la commande");
        noeud.CommandePre.Should().BeNull();
        noeud.CommandePost.Should().BeNull();
    }

    [Fact]
    public void Tache_AvecConfigComplete_StockeToutesLesInfos()
    {
        var def = PipelineProcessus.Creer("p")
            .Tache("Approuver", t => t
                .Description("Veuillez approuver ou refuser")
                .Role("RESPONSABLE")
                .Pre<PrepareApprobationCommand>()
                .Post<EnregistrerDecisionCommand>())
            .Compiler();

        var noeud = def.Noeuds.OfType<NoeudInteractif>().First();
        noeud.DefinitionTache.Description.Should().Be("Veuillez approuver ou refuser");
        noeud.DefinitionTache.CodeRole.Should().Be("RESPONSABLE");
        noeud.CommandePre!.NomCommande.Should().Be("PrepareApprobationCommand");
        noeud.CommandePost!.NomCommande.Should().Be("EnregistrerDecisionCommand");
    }

    [Fact]
    public void Tache_NomNoeudEgalId()
    {
        var def = PipelineProcessus.Creer("p")
            .Tache("Valider")
            .Compiler();

        var noeud = def.Noeuds.OfType<NoeudInteractif>().First();
        noeud.DefinitionTache.NomNoeud.Should().Be(noeud.Id);
    }

    // ── Décisions ─────────────────────────────────────────────────────────────

    [Fact]
    public void SiEgal_DeuxBranchesTerminales_DecisionDeuxFluxCorrects()
    {
        var def = PipelineProcessus.Creer("p")
            .Faire<ValiderCommandeCommand>()
            .SiEgal("statut", "approuve",
                oui: p => p.Faire<NotifierApprobationCommand>().Fin(),
                non: p => p.Faire<NotifierRefusCommand>().Fin())
            .Compiler();

        var decision = def.Noeuds.OfType<NoeudDecision>().First();
        decision.FluxSortants.Should().HaveCount(2);

        var fluxOui = decision.FluxSortants.First(f => f.Condition is ConditionVariable);
        ((ConditionVariable)fluxOui.Condition!).Operateur.Should().Be(Operateur.Egal);
        ((ConditionVariable)fluxOui.Condition!).Valeur.Should().Be("approuve");

        decision.FluxSortants.Should().ContainSingle(f => f.EstParDefaut);
    }

    [Fact]
    public void SiEgal_BranchesTerminales_NoeudsBranchesMarquesFinaux()
    {
        var def = PipelineProcessus.Creer("p")
            .SiEgal("statut", "ok",
                oui: p => p.Faire<NotifierApprobationCommand>().Fin(),
                non: p => p.Faire<NotifierRefusCommand>().Fin())
            .Compiler();

        def.Noeuds.OfType<NoeudMetier>()
            .Should().AllSatisfy(n => n.EstFinale.Should().BeTrue());
    }

    [Fact]
    public void SiEgal_BranchesContinuent_MergeeSurEtapeSuivante()
    {
        var def = PipelineProcessus.Creer("p")
            .SiEgal("statut", "ok",
                oui: p => p.Faire<NotifierApprobationCommand>(),
                non: p => p.Faire<NotifierRefusCommand>())
            .Faire<EnregistrerDecisionCommand>()   // reçoit les deux branches
            .Compiler();

        var merge = def.Noeuds.OfType<NoeudMetier>()
            .First(n => n.NomCommande == "EnregistrerDecisionCommand");

        // Les deux nœuds de branche doivent pointer vers merge
        var noeudsAvantMerge = def.Noeuds.OfType<NoeudMetier>()
            .Where(n => n.NomCommande != "EnregistrerDecisionCommand");

        noeudsAvantMerge.Should().AllSatisfy(n =>
            n.FluxSortants.Should().ContainSingle(f => f.Vers == merge.Id));
    }

    [Fact]
    public void SiQuery_AvecType_ConditionQueryAvecNomDuType()
    {
        var def = PipelineProcessus.Creer("p")
            .SiQuery<EstCommandeApprouveeQuery>(
                oui: p => p.Faire<NotifierApprobationCommand>().Fin(),
                non: p => p.Faire<NotifierRefusCommand>().Fin())
            .Compiler();

        var decision = def.Noeuds.OfType<NoeudDecision>().First();
        var fluxQuery = decision.FluxSortants.First(f => f.Condition is ConditionQuery);
        ((ConditionQuery)fluxQuery.Condition!).NomQuery.Should().Be("EstCommandeApprouveeQuery");
    }

    [Fact]
    public void SiQuery_AvecNomExplicite_ConditionQueryAvecCeNom()
    {
        var def = PipelineProcessus.Creer("p")
            .SiQuery("MonQueryPersonnalise",
                oui: p => p.Faire<NotifierApprobationCommand>().Fin(),
                non: p => p.Faire<NotifierRefusCommand>().Fin())
            .Compiler();

        var decision = def.Noeuds.OfType<NoeudDecision>().First();
        var fluxQuery = decision.FluxSortants.First(f => f.Condition is ConditionQuery);
        ((ConditionQuery)fluxQuery.Condition!).NomQuery.Should().Be("MonQueryPersonnalise");
    }

    [Fact]
    public void DeuxDecisions_IdsUniques()
    {
        // La décision imbriquée dans la branche "non" doit recevoir un ID différent ("decision-2")
        // grâce au compteur d'IDs partagé entre le pipeline parent et les branches.
        var def = PipelineProcessus.Creer("p")
            .SiEgal("a", 1,
                oui: p => p.Faire<NotifierApprobationCommand>().Fin(),
                non: p => p
                    .SiEgal("b", 2,
                        oui: b => b.Faire<ValiderCommandeCommand>().Fin(),
                        non: b => b.Faire<EnregistrerDecisionCommand>().Fin()))
            .Compiler();

        var decisions = def.Noeuds.OfType<NoeudDecision>().ToList();
        decisions.Should().HaveCount(2);
        decisions.Select(d => d.Id).Should().OnlyHaveUniqueItems();
        decisions.Select(d => d.Id).Should().Contain("decision").And.Contain("decision-2");
    }

    [Fact]
    public void SiEgal_ApresDecisionTerminale_LanceException()
    {
        var act = () => PipelineProcessus.Creer("p")
            .SiEgal("x", 1,
                oui: p => p.Faire<NotifierApprobationCommand>().Fin(),
                non: p => p.Faire<NotifierRefusCommand>().Fin())
            .Faire<ValiderCommandeCommand>()  // toutes les branches sont terminées
            .Compiler();

        act.Should().Throw<InvalidOperationException>().WithMessage("*terminée*");
    }

    // ── Attentes ──────────────────────────────────────────────────────────────

    [Fact]
    public void AttendreSignal_DernierNoeud_EstFinal()
    {
        var def = PipelineProcessus.Creer("p")
            .AttendreSignal("paiement-recu")
            .Compiler();

        var noeud = def.Noeuds.OfType<NoeudAttenteSignal>().First();
        noeud.NomSignal.Should().Be("paiement-recu");
        noeud.EstFinale.Should().BeTrue();
    }

    [Fact]
    public void AttendreSignal_PasEnDernierNoeud_ConnecteASuivant()
    {
        var def = PipelineProcessus.Creer("p")
            .AttendreSignal("paiement-recu")
            .Faire<ValiderCommandeCommand>()
            .Compiler();

        var signal = def.Noeuds.OfType<NoeudAttenteSignal>().First();
        signal.EstFinale.Should().BeFalse();
        signal.FluxSortants.Should().ContainSingle();
    }

    [Fact]
    public void AttendreDate_AvecVariable_SourceVariableCorrecte()
    {
        var def = PipelineProcessus.Creer("p")
            .AttendreDate("echeance-contrat")
            .Compiler();

        var noeud = def.Noeuds.OfType<NoeudAttenteTemps>().First();
        noeud.SourceDateEcheance.Should().BeOfType<SourceVariable>()
            .Which.NomVariable.Should().Be("echeance-contrat");
    }

    [Fact]
    public void AttendreDate_AvecDateStatique_SourceValeurStatique()
    {
        var date = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var def = PipelineProcessus.Creer("p")
            .AttendreDate(date)
            .Compiler();

        var noeud = def.Noeuds.OfType<NoeudAttenteTemps>().First();
        noeud.SourceDateEcheance.Should().BeOfType<SourceValeurStatique>()
            .Which.Valeur.Should().Be(date);
    }

    [Fact]
    public void AttendreDate_AvecQuery_SourceQueryCorrecte()
    {
        var def = PipelineProcessus.Creer("p")
            .AttendreDate<CalculerEcheanceQuery>()
            .Compiler();

        var noeud = def.Noeuds.OfType<NoeudAttenteTemps>().First();
        noeud.SourceDateEcheance.Should().BeOfType<SourceQuery>()
            .Which.NomQuery.Should().Be("CalculerEcheanceQuery");
    }

    // ── Sous-processus ────────────────────────────────────────────────────────

    [Fact]
    public void SousProcessus_AvecCleEtSorties_NoeudCorrect()
    {
        var def = PipelineProcessus.Creer("parent")
            .SousProcessus("validation-supplementaire", version: 2, "resultat", "erreur")
            .Compiler();

        var noeud = def.Noeuds.OfType<NoeudSousProcessus>().First();
        noeud.CleDefinition.Should().Be("validation-supplementaire");
        noeud.Version.Should().Be(2);
        noeud.VariablesSorties.Should().Contain("resultat").And.Contain("erreur");
    }

    // ── Scénario complet ──────────────────────────────────────────────────────

    [Fact]
    public void ScenarioComplet_ApprobationCommande_StructureCorrecte()
    {
        var def = PipelineProcessus.Creer("approbation-commande", "Approbation de commande")
            .Faire<ValiderCommandeCommand>()
            .Tache("Approuver la commande", t => t
                .Description("Veuillez approuver ou refuser")
                .Role("RESPONSABLE")
                .Post<EnregistrerDecisionCommand>())
            .SiQuery<EstCommandeApprouveeQuery>(
                oui: p => p.Faire<NotifierApprobationCommand>().Fin(),
                non: p => p.Faire<NotifierRefusCommand>().Fin())
            .Compiler();

        def.Cle.Should().Be("approbation-commande");
        def.Noeuds.Should().HaveCount(5); // Métier + Interactif + Décision + 2 branches

        def.Noeuds.OfType<NoeudMetier>().Should().HaveCount(3);
        def.Noeuds.OfType<NoeudInteractif>().Should().HaveCount(1);
        def.Noeuds.OfType<NoeudDecision>().Should().HaveCount(1);

        // Toutes les branches terminales doivent être marquées finales
        def.Noeuds.OfType<NoeudMetier>()
            .Where(n => n.NomCommande is "NotifierApprobationCommand" or "NotifierRefusCommand")
            .Should().AllSatisfy(n => n.EstFinale.Should().BeTrue());

        // Le nœud de début est bien le premier ajouté
        def.NoeudDebutId.Should().Be("valider-commande");
    }
}
