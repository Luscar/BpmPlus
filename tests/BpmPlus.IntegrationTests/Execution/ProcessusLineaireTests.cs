using BpmPlus.Abstractions;
using BpmPlus.Core.Definition;
using BpmPlus.IntegrationTests.Fixtures;
using FluentAssertions;

namespace BpmPlus.IntegrationTests.Execution;

/// <summary>
/// Tests d'intégration d'un processus linéaire complet :
/// démarrage, exécution des nœuds métier, fin.
/// </summary>
public class ProcessusLineaireTests : IDisposable
{
    private readonly BpmFixture _fixture = new();

    [Fact]
    public async Task Demarrer_ProcessusSimple_TermineAvecStatutTerminee()
    {
        await _fixture.PublierProcessusSimpleAsync("p-lineaire");

        var idInstance = await _fixture.ServiceBpm.DemarrerAsync("p-lineaire", 1L, null);

        idInstance.Should().BeGreaterThan(0);

        var instance = await _fixture.ServiceBpm.ObtenirAsync(idInstance);
        instance.Statut.Should().Be(StatutInstance.Terminee);
        instance.DateFin.Should().NotBeNull();
    }

    [Fact]
    public async Task Demarrer_AvecVariablesInitiales_VariablesPersistees()
    {
        await _fixture.PublierProcessusSimpleAsync("p-variables");

        var vars = new Dictionary<string, object?> { ["montant"] = 1500, ["devise"] = "EUR" };
        var idInstance = await _fixture.ServiceBpm.DemarrerAsync("p-variables", 2L, vars);

        var instance = await _fixture.ServiceBpm.ObtenirAsync(idInstance);
        instance.Statut.Should().Be(StatutInstance.Terminee);
    }

    [Fact]
    public async Task Demarrer_DeuxFoisMemAgregate_LanceProcessusDejaActifException()
    {
        // Crée un processus qui se suspend pour permettre le double démarrage
        var def = new ProcessusBuilder("p-double", "Double")
            .Debut("tache")
            .Interactif("tache", b => b.Tache("Attente").Vers("fin"))
            .Metier("fin")
            .Build();
        await _fixture.PublierDefinitionAsync(def);

        await _fixture.ServiceBpm.DemarrerAsync("p-double", 10L, null);

        var act = async () => await _fixture.ServiceBpm.DemarrerAsync("p-double", 10L, null);
        await act.Should().ThrowAsync<ProcessusDejaActifException>();
    }

    [Fact]
    public async Task Demarrer_DefinitionInexistante_LanceDefinitionIntrouvableException()
    {
        var act = async () => await _fixture.ServiceBpm.DemarrerAsync("inexistante", 1L, null);
        await act.Should().ThrowAsync<DefinitionIntrouvableException>();
    }

    [Fact]
    public async Task Demarrer_ProcessusAvecDecision_RoutageSelonVariable()
    {
        var def = new ProcessusBuilder("p-decision", "Décision")
            .Debut("check")
            .Decision("check", d => d
                .SiEgal("statut", "ok").Vers("fin-ok")
                .Defaut().Vers("fin-ko"))
            .Metier("fin-ok", b => b.Commande("NoOpCommand"))
            .Metier("fin-ko", b => b.Commande("NoOpCommand"))
            .Build();
        await _fixture.PublierDefinitionAsync(def);

        var idOk = await _fixture.ServiceBpm.DemarrerAsync("p-decision", 20L,
            new Dictionary<string, object?> { ["statut"] = "ok" });
        var idKo = await _fixture.ServiceBpm.DemarrerAsync("p-decision", 21L,
            new Dictionary<string, object?> { ["statut"] = "refuse" });

        var instOk = await _fixture.ServiceBpm.ObtenirAsync(idOk);
        var instKo = await _fixture.ServiceBpm.ObtenirAsync(idKo);

        instOk.Statut.Should().Be(StatutInstance.Terminee);
        instKo.Statut.Should().Be(StatutInstance.Terminee);
    }

    [Fact]
    public async Task Historique_ProcessusTermine_ContientEvenementsDebutEtFin()
    {
        await _fixture.PublierProcessusSimpleAsync("p-historique");

        var idInstance = await _fixture.ServiceBpm.DemarrerAsync("p-historique", 30L, null);

        var historique = await _fixture.ServiceBpm.ObtenirHistoriqueAsync(idInstance);

        historique.Should().Contain(e => e.TypeEvenement == TypeEvenement.DebutProcessus);
        historique.Should().Contain(e => e.TypeEvenement == TypeEvenement.FinProcessus);
    }

    [Fact]
    public async Task Demarrer_PlusieursDifferentsAgregats_IndependantsLUnDeLAutre()
    {
        await _fixture.PublierProcessusSimpleAsync("p-multi");

        var id1 = await _fixture.ServiceBpm.DemarrerAsync("p-multi", 100L, null);
        var id2 = await _fixture.ServiceBpm.DemarrerAsync("p-multi", 101L, null);
        var id3 = await _fixture.ServiceBpm.DemarrerAsync("p-multi", 102L, null);

        id1.Should().NotBe(id2);
        id2.Should().NotBe(id3);

        var inst1 = await _fixture.ServiceBpm.ObtenirAsync(id1);
        var inst2 = await _fixture.ServiceBpm.ObtenirAsync(id2);
        inst1.AggregateId.Should().Be(100L);
        inst2.AggregateId.Should().Be(101L);
    }

    public void Dispose() => _fixture.Dispose();
}
