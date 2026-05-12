using BpmPlus.Abstractions;
using BpmPlus.Core.Definition;
using BpmPlus.IntegrationTests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BpmPlus.IntegrationTests.Repository;

/// <summary>
/// Tests d'intégration des opérations CRUD sur les définitions de processus
/// via le repository SQLite.
/// </summary>
public class RepositoryDefinitionTests : IDisposable
{
    private readonly BpmFixture _fixture = new();

    private static DefinitionProcessus ConstruireDefinition(string cle = "proc-test") =>
        new ProcessusBuilder(cle, $"Processus {cle}")
            .Debut("start")
            .Metier("start", b => b.Commande("NoOpCommand").Vers("fin"))
            .Metier("fin", b => b.Commande("NoOpCommand"))
            .Build();

    [Fact]
    public async Task Sauvegarder_NouvelleDefinition_AssigneId()
    {
        var def = ConstruireDefinition("d-save-1");
        var id = await _fixture.RepoDefinition.SauvegarderAsync(def);

        id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Sauvegarder_PuisObtenirBrouillon_RetourneMemeDefinition()
    {
        var def = ConstruireDefinition("d-brouillon-1");
        await _fixture.RepoDefinition.SauvegarderAsync(def);

        var brouillon = await _fixture.RepoDefinition.ObtenirBrouillonAsync("d-brouillon-1");

        brouillon.Should().NotBeNull();
        brouillon!.Cle.Should().Be("d-brouillon-1");
        brouillon.Statut.Should().Be(StatutDefinition.Brouillon);
        brouillon.Noeuds.Should().HaveCount(2);
    }

    [Fact]
    public async Task ObtenirBrouillon_CleInexistante_RetourneNull()
    {
        var result = await _fixture.RepoDefinition.ObtenirBrouillonAsync("inexistant");
        result.Should().BeNull();
    }

    [Fact]
    public async Task Publier_DefinitionBrouillon_StatutDevientPubliee()
    {
        var def = ConstruireDefinition("d-publish-1");
        await _fixture.RepoDefinition.SauvegarderAsync(def);
        await _fixture.RepoDefinition.PublierAsync("d-publish-1");

        var publiee = await _fixture.RepoDefinition.ObtenirDerniereVersionPublieeAsync("d-publish-1");

        publiee.Should().NotBeNull();
        publiee!.Statut.Should().Be(StatutDefinition.Publiee);
        publiee.DatePublication.Should().NotBeNull();
    }

    [Fact]
    public async Task ObtenirDerniereVersionPubliee_CleInexistante_RetourneNull()
    {
        var result = await _fixture.RepoDefinition.ObtenirDerniereVersionPublieeAsync("inexistant");
        result.Should().BeNull();
    }

    [Fact]
    public async Task Sauvegarder_DeuxFois_CreeVersionsDifferentes()
    {
        var def1 = ConstruireDefinition("d-version-1");
        await _fixture.RepoDefinition.SauvegarderAsync(def1);
        await _fixture.RepoDefinition.PublierAsync("d-version-1");

        var def2 = ConstruireDefinition("d-version-1");
        await _fixture.RepoDefinition.SauvegarderAsync(def2);

        var brouillon = await _fixture.RepoDefinition.ObtenirBrouillonAsync("d-version-1");
        var publiee = await _fixture.RepoDefinition.ObtenirDerniereVersionPublieeAsync("d-version-1");

        brouillon!.Version.Should().BeGreaterThan(publiee!.Version);
    }

    [Fact]
    public async Task ObtenirToutes_PlusieursDefs_RetourneLesListes()
    {
        var def1 = ConstruireDefinition("d-all-1");
        var def2 = ConstruireDefinition("d-all-2");
        await _fixture.RepoDefinition.SauvegarderAsync(def1);
        await _fixture.RepoDefinition.SauvegarderAsync(def2);

        var toutes = await _fixture.RepoDefinition.ObtenirToutesAsync();

        toutes.Should().Contain(d => d.Cle == "d-all-1");
        toutes.Should().Contain(d => d.Cle == "d-all-2");
    }

    [Fact]
    public async Task ObtenirVersionPubliee_VersionSpecifique_RetourneCorrectement()
    {
        await _fixture.RepoDefinition.SauvegarderAsync(ConstruireDefinition("d-ver-spec"));
        await _fixture.RepoDefinition.PublierAsync("d-ver-spec");

        var publiee = await _fixture.RepoDefinition.ObtenirDerniereVersionPublieeAsync("d-ver-spec");
        var parVersion = await _fixture.RepoDefinition.ObtenirVersionPublieeAsync(
            "d-ver-spec", publiee!.Version);

        parVersion.Should().NotBeNull();
        parVersion!.Version.Should().Be(publiee.Version);
        parVersion.Noeuds.Should().HaveCount(publiee.Noeuds.Count);
    }

    [Fact]
    public async Task Sauvegarder_BrouillonExistant_MetAJourSansNouvelleLigne()
    {
        var def = ConstruireDefinition("d-update-1");
        await _fixture.RepoDefinition.SauvegarderAsync(def);
        var brouillon1 = await _fixture.RepoDefinition.ObtenirBrouillonAsync("d-update-1");

        // Mettre à jour le brouillon existant
        def.Id = brouillon1!.Id;
        def.Version = brouillon1.Version;
        def.Nom = "Nom modifié";
        await _fixture.RepoDefinition.SauvegarderAsync(def);

        var brouillon2 = await _fixture.RepoDefinition.ObtenirBrouillonAsync("d-update-1");
        brouillon2!.Nom.Should().Be("Nom modifié");
        brouillon2.Id.Should().Be(brouillon1.Id);
    }

    public void Dispose() => _fixture.Dispose();
}
