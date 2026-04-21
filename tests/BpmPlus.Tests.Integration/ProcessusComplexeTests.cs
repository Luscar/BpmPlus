using BpmPlus.Abstractions;
using BpmPlus.Core.Definition;
using BpmPlus.Tests.Integration.Infrastructure;
using Xunit;

namespace BpmPlus.Tests.Integration;

/// <summary>
/// Tests E2E : processus complexe couvrant nœuds interactifs, décisions, signaux et temporisation.
/// </summary>
public class ProcessusComplexeTests : TestBase
{
    // ── Clés des processus ────────────────────────────────────────────────────

    private const string CleApprobation = "processus-approbation";
    private const string CleSignal      = "processus-signal";
    private const string CleTimer       = "processus-timer";

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await PublierDefinitionAsync(CreerDefinitionApprobation());
        await PublierDefinitionAsync(CreerDefinitionSignal());
        await PublierDefinitionAsync(CreerDefinitionTimer());
    }

    // ── Définitions ───────────────────────────────────────────────────────────

    private static DefinitionProcessus CreerDefinitionApprobation()
    {
        //
        //  initialiser-dossier (NoeudMetier)
        //    → approbation-gestionnaire (NoeudInteractif, post: EnregistrerDecisionCommand)
        //        → decision-approbation (NoeudDecision)
        //            [EstDossierApprouveQuery == true] → notifier-approbation (NoeudMetier, final)
        //            [par défaut]                     → notifier-refus        (NoeudMetier, final)
        //
        return new DefinitionProcessusBuilder(
                CleApprobation,
                "Processus d'approbation",
                "initialiser-dossier")
            .AjouterNoeudMetier("initialiser-dossier", "Initialiser le dossier",
                vers: "approbation-gestionnaire")
            .AjouterNoeudInteractif("approbation-gestionnaire", "Approbation gestionnaire", n => n
                .DefinirTache("Approuver le dossier", "Veuillez approuver ou refuser le dossier")
                .AvecCommandePost("EnregistrerDecisionCommand")
                .Vers("decision-approbation"))
            .AjouterNoeudDecision("decision-approbation", "Décision d'approbation", n => n
                .SiQuery("EstDossierApprouveQuery").Vers("notifier-approbation")
                .ParDefaut().Vers("notifier-refus"))
            .AjouterNoeudMetier("notifier-approbation", "Notifier approbation")
            .AjouterNoeudMetier("notifier-refus", "Notifier refus")
            .Construire();
    }

    private static DefinitionProcessus CreerDefinitionSignal()
    {
        //
        //  initialiser-dossier (NoeudMetier)
        //    → attente-signal-validation (NoeudAttenteSignal, signal: "signal-validation")
        //        → finaliser-dossier (NoeudMetier, final)
        //
        return new DefinitionProcessusBuilder(
                CleSignal,
                "Processus avec signal d'attente",
                "initialiser-dossier")
            .AjouterNoeudMetier("initialiser-dossier", "Initialiser le dossier",
                vers: "attente-signal-validation")
            .AjouterNoeudAttenteSignal("attente-signal-validation", "Attente signal validation", n => n
                .Signal("signal-validation")
                .Vers("finaliser-dossier"))
            .AjouterNoeudMetier("finaliser-dossier", "Finaliser le dossier")
            .Construire();
    }

    private static DefinitionProcessus CreerDefinitionTimer()
    {
        //
        //  initialiser-dossier (NoeudMetier)
        //    → attente-traitement (NoeudAttenteTemps, écheance depuis variable "date_echeance")
        //        → traiter-apres-delai (NoeudMetier, final)
        //
        return new DefinitionProcessusBuilder(
                CleTimer,
                "Processus avec temporisation",
                "initialiser-dossier")
            .AjouterNoeudMetier("initialiser-dossier", "Initialiser le dossier",
                vers: "attente-traitement")
            .AjouterNoeudAttenteTemps("attente-traitement", "Attente de traitement", n => n
                .EcheanceDepuisVariable("date_echeance")
                .Vers("traiter-apres-delai"))
            .AjouterNoeudMetier("traiter-apres-delai", "Traiter après délai")
            .Construire();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // BLOC 1 : Nœud interactif (humain)
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Interactif_ApresDepart_InstanceSuspendueSurNoeudInteractif()
    {
        var id = await DemarrerAsync(CleApprobation, aggregateId: 3001);

        var instance = await ObtenirAsync(id);

        Assert.Equal(StatutInstance.Suspendue, instance.Statut);
        Assert.Equal("approbation-gestionnaire", instance.IdNoeudCourant);
    }

    [Fact]
    public async Task Interactif_DeuxDemarragesMemeAggregateActif_LeveException()
    {
        await DemarrerAsync(CleApprobation, aggregateId: 3002);

        await Assert.ThrowsAsync<ProcessusDejaActifException>(
            () => DemarrerAsync(CleApprobation, aggregateId: 3002));
    }

    [Fact]
    public async Task Interactif_Approuve_ProcessusTermineViaBrancheApprobation()
    {
        var id = await DemarrerAsync(CleApprobation, aggregateId: 3003);

        await ModifierVariableEtTerminerEtapeAsync(id, "approbation", "Approuve");

        var instance = await ObtenirAsync(id);
        Assert.Equal(StatutInstance.Terminee, instance.Statut);

        var historique = await ObtenirHistoriqueAsync(id);
        AssertEvenementPresent(historique, TypeEvenement.EntreeNoeud, "notifier-approbation");
        AssertEvenementPresent(historique, TypeEvenement.FinProcessus);
    }

    [Fact]
    public async Task Interactif_Refuse_ProcessusTermineViaBrancheRefus()
    {
        var id = await DemarrerAsync(CleApprobation, aggregateId: 3004);

        await ModifierVariableEtTerminerEtapeAsync(id, "approbation", "Refuse");

        var instance = await ObtenirAsync(id);
        Assert.Equal(StatutInstance.Terminee, instance.Statut);

        var historique = await ObtenirHistoriqueAsync(id);
        AssertEvenementPresent(historique, TypeEvenement.EntreeNoeud, "notifier-refus");
        AssertEvenementPresent(historique, TypeEvenement.FinProcessus);
    }

    [Fact]
    public async Task Interactif_Approuve_NoeudRefusNonVisite()
    {
        var id = await DemarrerAsync(CleApprobation, aggregateId: 3005);

        await ModifierVariableEtTerminerEtapeAsync(id, "approbation", "Approuve");

        var historique = await ObtenirHistoriqueAsync(id);
        Assert.DoesNotContain(historique, e => e.IdNoeud == "notifier-refus");
    }

    [Fact]
    public async Task Interactif_Refuse_NoeudApprobationNonVisite()
    {
        var id = await DemarrerAsync(CleApprobation, aggregateId: 3006);

        await ModifierVariableEtTerminerEtapeAsync(id, "approbation", "Refuse");

        var historique = await ObtenirHistoriqueAsync(id);
        Assert.DoesNotContain(historique, e => e.IdNoeud == "notifier-approbation");
    }

    [Fact]
    public async Task Interactif_CommandePost_ExecuteeAvantDecision()
    {
        var id = await DemarrerAsync(CleApprobation, aggregateId: 3007);

        await ModifierVariableEtTerminerEtapeAsync(id, "approbation", "Approuve");

        // La CommandePost "EnregistrerDecisionCommand" s'exécute pendant TerminerEtapeAsync.
        // L'événement NoeudRepris confirme que le nœud interactif a bien été repris (et
        // donc la post-commande exécutée) avant que la décision soit évaluée.
        var historique = await ObtenirHistoriqueAsync(id);
        AssertEvenementPresent(historique, TypeEvenement.NoeudRepris, "approbation-gestionnaire");
        AssertEvenementPresent(historique, TypeEvenement.FinProcessus);
    }

    [Fact]
    public async Task Interactif_HistoriqueContientNoeudSuspenduEtNoeudRepris()
    {
        var id = await DemarrerAsync(CleApprobation, aggregateId: 3008);

        AssertEvenementPresent(await ObtenirHistoriqueAsync(id),
            TypeEvenement.NoeudSuspendu, "approbation-gestionnaire");

        await ModifierVariableEtTerminerEtapeAsync(id, "approbation", "Approuve");

        var historique = await ObtenirHistoriqueAsync(id);
        AssertEvenementPresent(historique, TypeEvenement.NoeudSuspendu, "approbation-gestionnaire");
        AssertEvenementPresent(historique, TypeEvenement.NoeudRepris,   "approbation-gestionnaire");
    }

    [Fact]
    public async Task Interactif_Notification_VariableDefinie()
    {
        var id = await DemarrerAsync(CleApprobation, aggregateId: 3009);
        await ModifierVariableEtTerminerEtapeAsync(id, "approbation", "Approuve");

        var resultats = await RechercherParVariableAsync("notification", "approbation");
        Assert.Contains(resultats, i => i.Id == id);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // BLOC 2 : Nœud d'attente de signal
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Signal_ApresDepart_InstanceSuspendueSurAttenteSignal()
    {
        var id = await DemarrerAsync(CleSignal, aggregateId: 4001);

        var instance = await ObtenirAsync(id);

        Assert.Equal(StatutInstance.Suspendue, instance.Statut);
        Assert.Equal("attente-signal-validation", instance.IdNoeudCourant);
    }

    [Fact]
    public async Task Signal_SignauxEnAttente_ContientNomDuSignal()
    {
        var id = await DemarrerAsync(CleSignal, aggregateId: 4002);

        var signaux = await ObtenirSignauxEnAttenteAsync(id);

        Assert.Contains("signal-validation", signaux);
    }

    [Fact]
    public async Task Signal_EnvoiSpecifique_ProcessusTermine()
    {
        var id = await DemarrerAsync(CleSignal, aggregateId: 4003);

        await EnvoyerSignalAsync("signal-validation", idInstance: id);

        var instance = await ObtenirAsync(id);
        Assert.Equal(StatutInstance.Terminee, instance.Statut);
    }

    [Fact]
    public async Task Signal_EnvoiBroadcast_ReprendTousLesProcessusEnAttente()
    {
        var id1 = await DemarrerAsync(CleSignal, aggregateId: 4004);
        var id2 = await DemarrerAsync(CleSignal, aggregateId: 4005);

        await EnvoyerSignalAsync("signal-validation"); // broadcast sans idInstance

        var i1 = await ObtenirAsync(id1);
        var i2 = await ObtenirAsync(id2);

        Assert.Equal(StatutInstance.Terminee, i1.Statut);
        Assert.Equal(StatutInstance.Terminee, i2.Statut);
    }

    [Fact]
    public async Task Signal_ApresReception_HistoriqueContientSignalRecu()
    {
        var id = await DemarrerAsync(CleSignal, aggregateId: 4006);
        await EnvoyerSignalAsync("signal-validation", idInstance: id);

        var historique = await ObtenirHistoriqueAsync(id);
        AssertEvenementPresent(historique, TypeEvenement.SignalRecu);
        AssertEvenementPresent(historique, TypeEvenement.FinProcessus);
    }

    [Fact]
    public async Task Signal_ApresReception_NoeudFinalVisite()
    {
        var id = await DemarrerAsync(CleSignal, aggregateId: 4007);
        await EnvoyerSignalAsync("signal-validation", idInstance: id);

        var historique = await ObtenirHistoriqueAsync(id);
        AssertEvenementPresent(historique, TypeEvenement.EntreeNoeud, "finaliser-dossier");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // BLOC 3 : Nœud d'attente temporisée
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Timer_ApresDepart_InstanceSuspendueSurAttenteTemps()
    {
        var echeanceDansLeFutur = DateTime.UtcNow.AddDays(1);
        var id = await DemarrerAsync(CleTimer, aggregateId: 5001,
            variables: new Dictionary<string, object?> { ["date_echeance"] = echeanceDansLeFutur });

        var instance = await ObtenirAsync(id);

        Assert.Equal(StatutInstance.Suspendue, instance.Statut);
        Assert.Equal("attente-traitement", instance.IdNoeudCourant);
    }

    [Fact]
    public async Task Timer_ReprendreAttenteTemps_ProcessusTermine()
    {
        var echeanceDansLeFutur = DateTime.UtcNow.AddDays(1);
        var id = await DemarrerAsync(CleTimer, aggregateId: 5002,
            variables: new Dictionary<string, object?> { ["date_echeance"] = echeanceDansLeFutur });

        await ReprendreAttenteTempsAsync(id);

        var instance = await ObtenirAsync(id);
        Assert.Equal(StatutInstance.Terminee, instance.Statut);
    }

    [Fact]
    public async Task Timer_ReprendreAttenteTemps_NoeudFinalVisite()
    {
        var id = await DemarrerAsync(CleTimer, aggregateId: 5003,
            variables: new Dictionary<string, object?> { ["date_echeance"] = DateTime.UtcNow.AddDays(1) });

        await ReprendreAttenteTempsAsync(id);

        var historique = await ObtenirHistoriqueAsync(id);
        AssertEvenementPresent(historique, TypeEvenement.EntreeNoeud, "traiter-apres-delai");
        AssertEvenementPresent(historique, TypeEvenement.FinProcessus);
    }

    [Fact]
    public async Task Timer_ObtenirInstancesEchues_RetourneInstancesPasDeadline()
    {
        var echeanceDejaPasse = DateTime.UtcNow.AddDays(-1);
        var id = await DemarrerAsync(CleTimer, aggregateId: 5004,
            variables: new Dictionary<string, object?> { ["date_echeance"] = echeanceDejaPasse });

        var echues = await ObtenirInstancesEchuesAsync(DateTime.UtcNow);

        Assert.Contains(echues, e => e.IdInstance == id);
    }

    [Fact]
    public async Task Timer_ObtenirInstancesEchues_ExclutInstancesNonEchues()
    {
        var echeanceDansLeFutur = DateTime.UtcNow.AddDays(1);
        var id = await DemarrerAsync(CleTimer, aggregateId: 5005,
            variables: new Dictionary<string, object?> { ["date_echeance"] = echeanceDansLeFutur });

        var echues = await ObtenirInstancesEchuesAsync(DateTime.UtcNow);

        Assert.DoesNotContain(echues, e => e.IdInstance == id);
    }

    [Fact]
    public async Task Timer_HandlerFinalExecute_NoeudFinalVisiteEtInstanceTerminee()
    {
        var id = await DemarrerAsync(CleTimer, aggregateId: 5006,
            variables: new Dictionary<string, object?> { ["date_echeance"] = DateTime.UtcNow.AddDays(1) });

        await ReprendreAttenteTempsAsync(id);

        var historique = await ObtenirHistoriqueAsync(id);
        AssertEvenementPresent(historique, TypeEvenement.EntreeNoeud, "traiter-apres-delai");
        Assert.Equal(StatutInstance.Terminee, (await ObtenirAsync(id)).Statut);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // BLOC 4 : Décision par condition variable (sans query)
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Decision_ConditionVariable_RouteCorrectement_BrancheVraie()
    {
        const string cleConditionVariable = "processus-decision-variable";

        // Enregistrer un processus avec condition sur variable
        var definition = new DefinitionProcessusBuilder(
                cleConditionVariable,
                "Test décision par variable",
                "initialiser-dossier")
            .AjouterNoeudMetier("initialiser-dossier", "Init",
                vers: "decision-priorite")
            .AjouterNoeudDecision("decision-priorite", "Décision priorité", n => n
                .SiEgal("priorite", "haute").Vers("notifier-approbation")
                .ParDefaut().Vers("notifier-refus"))
            .AjouterNoeudMetier("notifier-approbation", "Haute priorité")
            .AjouterNoeudMetier("notifier-refus", "Priorité normale")
            .Construire();

        await PublierDefinitionAsync(definition);

        var id = await DemarrerAsync(cleConditionVariable, aggregateId: 6001,
            variables: new Dictionary<string, object?> { ["priorite"] = "haute" });

        var historique = await ObtenirHistoriqueAsync(id);
        AssertEvenementPresent(historique, TypeEvenement.EntreeNoeud, "notifier-approbation");
        Assert.DoesNotContain(historique, e => e.IdNoeud == "notifier-refus");
    }

    [Fact]
    public async Task Decision_ConditionVariable_RouteCorrectement_BrancheParDefaut()
    {
        const string cleConditionVariable = "processus-decision-variable-defaut";

        var definition = new DefinitionProcessusBuilder(
                cleConditionVariable,
                "Test décision par variable (défaut)",
                "initialiser-dossier")
            .AjouterNoeudMetier("initialiser-dossier", "Init",
                vers: "decision-priorite")
            .AjouterNoeudDecision("decision-priorite", "Décision priorité", n => n
                .SiEgal("priorite", "haute").Vers("notifier-approbation")
                .ParDefaut().Vers("notifier-refus"))
            .AjouterNoeudMetier("notifier-approbation", "Haute priorité")
            .AjouterNoeudMetier("notifier-refus", "Priorité normale")
            .Construire();

        await PublierDefinitionAsync(definition);

        var id = await DemarrerAsync(cleConditionVariable, aggregateId: 6002,
            variables: new Dictionary<string, object?> { ["priorite"] = "basse" });

        var historique = await ObtenirHistoriqueAsync(id);
        AssertEvenementPresent(historique, TypeEvenement.EntreeNoeud, "notifier-refus");
        Assert.DoesNotContain(historique, e => e.IdNoeud == "notifier-approbation");
    }
}
