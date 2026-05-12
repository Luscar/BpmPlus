using BpmPlus.Abstractions;
using BpmPlus.Core.Definition;
using BpmPlus.IntegrationTests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BpmPlus.IntegrationTests.Execution;

/// <summary>
/// Tests d'intégration pour les processus contenant un nœud sous-processus.
/// Couvre : complétion, variables de sortie, définition introuvable, suspension.
/// </summary>
public class ProcessusSousProcessusTests : IDisposable
{
    private readonly BpmFixture _fixture = new();

    [Fact]
    public async Task DemarrerSousProcessus_EnfantTermine_ParentContinueEtTermine()
    {
        var enfant = new ProcessusBuilder("sp-enfant-1", "Enfant simple")
            .Debut("tache")
            .Metier("tache")
            .Build();
        await _fixture.PublierDefinitionAsync(enfant);

        var parent = new ProcessusBuilder("sp-parent-1", "Parent")
            .Debut("sp")
            .SousProcessus("sp", b => b.Definition("sp-enfant-1", 1).Vers("fin"))
            .Metier("fin")
            .Build();
        await _fixture.PublierDefinitionAsync(parent);

        var idParent = await _fixture.ServiceBpm.DemarrerAsync("sp-parent-1", 1L, null);

        var instance = await _fixture.ServiceBpm.ObtenirAsync(idParent);
        instance.Statut.Should().Be(StatutInstance.Terminee);
        instance.DateFin.Should().NotBeNull();
    }

    [Fact]
    public async Task DemarrerSousProcessus_NoeudFinalSansFlux_ParentTermine()
    {
        var enfant = new ProcessusBuilder("sp-enfant-2", "Enfant final")
            .Debut("tache")
            .Metier("tache")
            .Build();
        await _fixture.PublierDefinitionAsync(enfant);

        // Nœud SousProcessus sans .Vers() — doit terminer le parent implicitement
        var parent = new ProcessusBuilder("sp-parent-2", "Parent final")
            .Debut("sp")
            .SousProcessus("sp", b => b.Definition("sp-enfant-2", 1))
            .Build();
        await _fixture.PublierDefinitionAsync(parent);

        var idParent = await _fixture.ServiceBpm.DemarrerAsync("sp-parent-2", 1L, null);

        var instance = await _fixture.ServiceBpm.ObtenirAsync(idParent);
        instance.Statut.Should().Be(StatutInstance.Terminee);
    }

    [Fact]
    public async Task DemarrerSousProcessus_PropageVariablesSorties_VersParent()
    {
        var enfant = new ProcessusBuilder("sp-enfant-3", "Enfant avec variable")
            .Debut("set")
            .Metier("set", b => b
                .Commande("DefinirVariableCommand")
                .Param("nom", Src.Val("resultat"))
                .Param("valeur", Src.Val("succès")))
            .Build();
        await _fixture.PublierDefinitionAsync(enfant);

        var parent = new ProcessusBuilder("sp-parent-3", "Parent avec sortie")
            .Debut("sp")
            .SousProcessus("sp", b => b
                .Definition("sp-enfant-3", 1)
                .Sortie("resultat")
                .Vers("fin"))
            .Metier("fin")
            .Build();
        await _fixture.PublierDefinitionAsync(parent);

        var idParent = await _fixture.ServiceBpm.DemarrerAsync("sp-parent-3", 1L, null);

        var variables = await _fixture.RepoVariable.ChargerToutesAsync(idParent);
        variables.Should().ContainKey("resultat");
        variables["resultat"].Should().Be("succès");
    }

    [Fact]
    public async Task DemarrerSousProcessus_DefinitionEnfantIntrouvable_LanceException()
    {
        var parent = new ProcessusBuilder("sp-parent-4", "Parent orphelin")
            .Debut("sp")
            .SousProcessus("sp", b => b.Definition("inexistant", 1).Vers("fin"))
            .Metier("fin")
            .Build();
        await _fixture.PublierDefinitionAsync(parent);

        var act = async () => await _fixture.ServiceBpm.DemarrerAsync("sp-parent-4", 1L, null);
        await act.Should().ThrowAsync<DefinitionIntrouvableException>();
    }

    [Fact]
    public async Task DemarrerSousProcessus_EnfantSuspendu_ParentSuspenduAvecEnfantLie()
    {
        var enfant = new ProcessusBuilder("sp-enfant-5", "Enfant interactif")
            .Debut("attente")
            .Interactif("attente", b => b.Tache("Valider").Vers("fin"))
            .Metier("fin")
            .Build();
        await _fixture.PublierDefinitionAsync(enfant);

        var parent = new ProcessusBuilder("sp-parent-5", "Parent avec enfant interactif")
            .Debut("sp")
            .SousProcessus("sp", b => b.Definition("sp-enfant-5", 1).Vers("fin"))
            .Metier("fin")
            .Build();
        await _fixture.PublierDefinitionAsync(parent);

        var idParent = await _fixture.ServiceBpm.DemarrerAsync("sp-parent-5", 1L, null);

        var instanceParent = await _fixture.ServiceBpm.ObtenirAsync(idParent);
        instanceParent.Statut.Should().Be(StatutInstance.Suspendue);
        instanceParent.IdNoeudCourant.Should().Be("sp");

        var enfants = await _fixture.ServiceBpm.ObtenirEnfantsAsync(idParent);
        enfants.Should().HaveCount(1);
        enfants[0].IdInstanceParent.Should().Be(idParent);
        enfants[0].Statut.Should().Be(StatutInstance.Suspendue);
        enfants[0].IdNoeudCourant.Should().Be("attente");
    }

    public void Dispose() => _fixture.Dispose();
}
