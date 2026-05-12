using BpmPlus.Abstractions;
using BpmPlus.Core.Persistance;
using BpmPlus.IntegrationTests.Fixtures;
using FluentAssertions;

namespace BpmPlus.IntegrationTests.Repository;

/// <summary>
/// Tests d'intégration des opérations CRUD sur les instances de processus
/// via le repository SQLite.
/// </summary>
public class RepositoryInstanceTests : IDisposable
{
    private readonly BpmFixture _fixture = new();

    private static InstanceProcessus NouvelleInstance(string cle = "proc", long aggregateId = 1) => new()
    {
        CleDefinition = cle,
        VersionDefinition = 1,
        AggregateId = aggregateId,
        Statut = StatutInstance.Active,
        DateDebut = DateTime.UtcNow,
        DateCreation = DateTime.UtcNow,
        DateMaj = DateTime.UtcNow
    };

    [Fact]
    public async Task Creer_NouvelleInstance_AssigneId()
    {
        var instance = NouvelleInstance();
        var id = await _fixture.RepoInstance.CreerAsync(instance);

        id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ObtenirParId_InstanceExistante_RetourneInstance()
    {
        var id = await _fixture.RepoInstance.CreerAsync(NouvelleInstance("proc-get", 10));

        var result = await _fixture.RepoInstance.ObtenirParIdAsync(id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(id);
        result.CleDefinition.Should().Be("proc-get");
        result.AggregateId.Should().Be(10);
    }

    [Fact]
    public async Task ObtenirParId_Inexistant_RetourneNull()
    {
        var result = await _fixture.RepoInstance.ObtenirParIdAsync(999999);
        result.Should().BeNull();
    }

    [Fact]
    public async Task MettreAJourStatut_Suspendu_StatutMisAJour()
    {
        var id = await _fixture.RepoInstance.CreerAsync(NouvelleInstance("proc-statut", 20));

        await _fixture.RepoInstance.MettreAJourStatutAsync(id, StatutInstance.Suspendue, "noeud-1", null);

        var result = await _fixture.RepoInstance.ObtenirParIdAsync(id);
        result!.Statut.Should().Be(StatutInstance.Suspendue);
        result.IdNoeudCourant.Should().Be("noeud-1");
    }

    [Fact]
    public async Task MettreAJourStatut_Termine_DateFinRenseignee()
    {
        var id = await _fixture.RepoInstance.CreerAsync(NouvelleInstance("proc-fin", 30));
        var dateFin = DateTime.UtcNow;

        await _fixture.RepoInstance.MettreAJourStatutAsync(id, StatutInstance.Terminee, null, dateFin);

        var result = await _fixture.RepoInstance.ObtenirParIdAsync(id);
        result!.Statut.Should().Be(StatutInstance.Terminee);
        result.DateFin.Should().NotBeNull();
    }

    [Fact]
    public async Task ExisteProcessusActif_InstanceActive_RetourneTrue()
    {
        await _fixture.RepoInstance.CreerAsync(NouvelleInstance("proc-actif", 40));

        var existe = await _fixture.RepoInstance.ExisteProcessusActifAsync("proc-actif", 40);

        existe.Should().BeTrue();
    }

    [Fact]
    public async Task ExisteProcessusActif_InstanceTerminee_RetourneFalse()
    {
        var id = await _fixture.RepoInstance.CreerAsync(NouvelleInstance("proc-terme", 50));
        await _fixture.RepoInstance.MettreAJourStatutAsync(id, StatutInstance.Terminee, null, DateTime.UtcNow);

        var existe = await _fixture.RepoInstance.ExisteProcessusActifAsync("proc-terme", 50);

        existe.Should().BeFalse();
    }

    [Fact]
    public async Task ObtenirActiveParAggregate_InstanceActive_RetourneInstance()
    {
        var id = await _fixture.RepoInstance.CreerAsync(NouvelleInstance("proc-agg", 60));

        var result = await _fixture.RepoInstance.ObtenirActiveParAggregateAsync("proc-agg", 60);

        result.Should().NotBeNull();
        result!.Id.Should().Be(id);
    }

    [Fact]
    public async Task ObtenirParStatut_FiltreCorrectement()
    {
        var id1 = await _fixture.RepoInstance.CreerAsync(NouvelleInstance("proc-filtre", 70));
        var id2 = await _fixture.RepoInstance.CreerAsync(NouvelleInstance("proc-filtre", 71));
        await _fixture.RepoInstance.MettreAJourStatutAsync(id1, StatutInstance.Suspendue, "n1", null);

        var suspendues = await _fixture.RepoInstance.ObtenirParStatutAsync(StatutInstance.Suspendue);
        var actives = await _fixture.RepoInstance.ObtenirParStatutAsync(StatutInstance.Active);

        suspendues.Should().Contain(i => i.Id == id1);
        actives.Should().Contain(i => i.Id == id2);
        actives.Should().NotContain(i => i.Id == id1);
    }

    [Fact]
    public async Task ObtenirEnfants_ProcessusParentAvecEnfant_RetourneEnfants()
    {
        var idParent = await _fixture.RepoInstance.CreerAsync(NouvelleInstance("parent-proc", 80));

        var enfant = NouvelleInstance("enfant-proc", 0);
        enfant.IdInstanceParent = idParent;
        var idEnfant = await _fixture.RepoInstance.CreerAsync(enfant);

        var enfants = await _fixture.RepoInstance.ObtenirEnfantsAsync(idParent);

        enfants.Should().ContainSingle(i => i.Id == idEnfant);
    }

    [Fact]
    public async Task RechercherParVariable_VariableExistante_RetourneLesInstancesCorrespondantes()
    {
        var idInstance = await _fixture.RepoInstance.CreerAsync(NouvelleInstance("proc-var", 90));

        await _fixture.RepoVariable.SauvegarderToutesAsync(idInstance,
            new Dictionary<string, object?> { ["codeClient"] = "CLI001" });

        var resultats = await _fixture.RepoInstance.RechercherParVariableAsync("codeClient", "CLI001");

        resultats.Should().Contain(i => i.Id == idInstance);
    }

    public void Dispose() => _fixture.Dispose();
}
