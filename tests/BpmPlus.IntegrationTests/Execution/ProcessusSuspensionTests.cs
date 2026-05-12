using BpmPlus.Abstractions;
using BpmPlus.Core.Definition;
using BpmPlus.IntegrationTests.Fixtures;
using FluentAssertions;

namespace BpmPlus.IntegrationTests.Execution;

/// <summary>
/// Tests d'intégration pour les processus avec suspension sur nœud interactif.
/// Couvre : entrée, suspension, reprise via TerminerEtapeAsync.
/// </summary>
public class ProcessusSuspensionTests : IDisposable
{
    private readonly BpmFixture _fixture = new();

    private async Task<string> PublierProcessusInteractifAsync(string cle = "p-interactif")
    {
        var def = new ProcessusBuilder(cle, "Processus interactif")
            .Debut("debut")
            .Metier("debut", "Initialisation", b => b.Commande("NoOpCommand").Vers("tache"))
            .Interactif("tache", b => b.Tache("Valider le dossier").Vers("fin"))
            .Metier("fin", b => b.Commande("NoOpCommand"))
            .Build();
        await _fixture.PublierDefinitionAsync(def);
        return cle;
    }

    [Fact]
    public async Task Demarrer_ProcessusInteractif_SuspenduSurNoeudTache()
    {
        var cle = await PublierProcessusInteractifAsync("p-susp-1");

        var idInstance = await _fixture.ServiceBpm.DemarrerAsync(cle, 1L, null);

        var instance = await _fixture.ServiceBpm.ObtenirAsync(idInstance);
        instance.Statut.Should().Be(StatutInstance.Suspendue);
        instance.IdNoeudCourant.Should().Be("tache");
    }

    [Fact]
    public async Task TerminerEtape_InstanceSuspendue_ReprendEtTermine()
    {
        var cle = await PublierProcessusInteractifAsync("p-susp-2");

        var idInstance = await _fixture.ServiceBpm.DemarrerAsync(cle, 2L, null);

        var avantReprise = await _fixture.ServiceBpm.ObtenirAsync(idInstance);
        avantReprise.Statut.Should().Be(StatutInstance.Suspendue);

        await _fixture.ServiceBpm.TerminerEtapeAsync(idInstance);

        var apresReprise = await _fixture.ServiceBpm.ObtenirAsync(idInstance);
        apresReprise.Statut.Should().Be(StatutInstance.Terminee);
    }

    [Fact]
    public async Task TerminerEtape_InstanceActive_LanceEtatInstanceInvalideException()
    {
        var def = new ProcessusBuilder("p-actif", "Actif")
            .Debut("debut")
            .Interactif("debut", b => b.Tache("T").Vers("fin"))
            .Metier("fin", b => b.Commande("NoOpCommand"))
            .Build();
        await _fixture.PublierDefinitionAsync(def);

        var idInstance = await _fixture.ServiceBpm.DemarrerAsync("p-actif", 3L, null);
        await _fixture.ServiceBpm.TerminerEtapeAsync(idInstance);

        // L'instance est maintenant Terminée — TerminerEtape doit échouer
        var act = async () => await _fixture.ServiceBpm.TerminerEtapeAsync(idInstance);
        await act.Should().ThrowAsync<EtatInstanceInvalideException>();
    }

    [Fact]
    public async Task ObtenirParAggregate_InstanceSuspendue_TrouveInstance()
    {
        var cle = await PublierProcessusInteractifAsync("p-susp-3");

        var idInstance = await _fixture.ServiceBpm.DemarrerAsync(cle, 4L, null);

        var result = await _fixture.ServiceBpm.ObtenirParAggregateAsync(cle, 4L);
        result.Should().NotBeNull();
        result!.Id.Should().Be(idInstance);
    }

    [Fact]
    public async Task AssignerLogon_InstanceSuspendue_EnregistreEvenementAssignation()
    {
        var cle = await PublierProcessusInteractifAsync("p-susp-4");

        var idInstance = await _fixture.ServiceBpm.DemarrerAsync(cle, 5L, null);

        await _fixture.ServiceBpm.AssignerLogonAsync(idInstance, "jean.dupont");

        var logon = await _fixture.ServiceBpm.ObtenirLogonTacheActiveAsync(idInstance);
        logon.Should().Be("jean.dupont");
    }

    [Fact]
    public async Task ModifierVariable_InstanceSuspendue_VariableModifiee()
    {
        var cle = await PublierProcessusInteractifAsync("p-susp-5");

        var idInstance = await _fixture.ServiceBpm.DemarrerAsync(cle, 6L,
            new Dictionary<string, object?> { ["statut"] = "en-cours" });

        await _fixture.ServiceBpm.ModifierVariableAsync(idInstance, "statut", "valide");

        var historique = await _fixture.ServiceBpm.ObtenirHistoriqueAsync(idInstance);
        historique.Should().Contain(e =>
            e.TypeEvenement == TypeEvenement.VariableModifiee &&
            e.Detail != null && e.Detail.Contains("statut"));
    }

    [Fact]
    public async Task ProcessusInteractifAvecSuspensions_MultiplesCycles()
    {
        // Deux tâches interactives consécutives
        var def = new ProcessusBuilder("p-deux-taches", "Deux tâches")
            .Debut("tache1")
            .Interactif("tache1", b => b.Tache("Première tâche").Vers("tache2"))
            .Interactif("tache2", b => b.Tache("Deuxième tâche").Vers("fin"))
            .Metier("fin", b => b.Commande("NoOpCommand"))
            .Build();
        await _fixture.PublierDefinitionAsync(def);

        var idInstance = await _fixture.ServiceBpm.DemarrerAsync("p-deux-taches", 7L, null);

        var apres1er = await _fixture.ServiceBpm.ObtenirAsync(idInstance);
        apres1er.Statut.Should().Be(StatutInstance.Suspendue);
        apres1er.IdNoeudCourant.Should().Be("tache1");

        await _fixture.ServiceBpm.TerminerEtapeAsync(idInstance);

        var apres2eme = await _fixture.ServiceBpm.ObtenirAsync(idInstance);
        apres2eme.Statut.Should().Be(StatutInstance.Suspendue);
        apres2eme.IdNoeudCourant.Should().Be("tache2");

        await _fixture.ServiceBpm.TerminerEtapeAsync(idInstance);

        var final = await _fixture.ServiceBpm.ObtenirAsync(idInstance);
        final.Statut.Should().Be(StatutInstance.Terminee);
    }

    public void Dispose() => _fixture.Dispose();
}
