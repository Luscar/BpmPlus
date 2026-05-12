using Autofac;
using BpmPlus.Abstractions;
using BpmPlus.Core.Execution;
using BpmPlus.Core.Execution.Executeurs;
using BpmPlus.Core.Persistance;
using BpmPlus.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Moq;

namespace BpmPlus.UnitTests.Services;

/// <summary>
/// Tests unitaires de ServiceBpm couvrant la logique de coordination
/// (validation d'état, recherche, variables). Le moteur d'exécution est isolé
/// via des mocks retournant un état suspendu ou terminé selon le scénario.
/// </summary>
public class ServiceBpmTests
{
    private readonly Mock<IRepositoryDefinition> _repoDefMock = new();
    private readonly Mock<IRepositoryInstance> _repoInstMock = new();
    private readonly Mock<IRepositoryVariable> _repoVarMock = new();
    private readonly Mock<IRepositoryEvenement> _repoEvtMock = new();
    private readonly Mock<IRepositoryAttenteSignal> _repoSignalMock = new();
    private readonly Mock<IGestionTache> _gestionTacheMock = new();

    private ServiceBpm BuildService(ILifetimeScope? scope = null)
    {
        scope ??= new ContainerBuilder().Build().BeginLifetimeScope();

        var resolveur = new ResolveurParametre(scope, NullLogger<ResolveurParametre>.Instance);
        var executeurMetier = new ExecuteurNoeudMetier(scope, resolveur, NullLogger<ExecuteurNoeudMetier>.Instance);
        var executeurInteractif = new ExecuteurNoeudInteractif(
            executeurMetier, _gestionTacheMock.Object, NullLogger<ExecuteurNoeudInteractif>.Instance);
        var executeurDecision = new ExecuteurNoeudDecision(resolveur, NullLogger<ExecuteurNoeudDecision>.Instance);
        var executeurAttenteTemps = new ExecuteurNoeudAttenteTemps(
            resolveur, NullLogger<ExecuteurNoeudAttenteTemps>.Instance);
        var executeurAttenteSignal = new ExecuteurNoeudAttenteSignal(
            _repoSignalMock.Object, NullLogger<ExecuteurNoeudAttenteSignal>.Instance);

        var executeurSousProcessusLazy = new Lazy<ExecuteurNoeudSousProcessus>(() =>
            new ExecuteurNoeudSousProcessus(
                _repoDefMock.Object,
                _repoInstMock.Object,
                _repoVarMock.Object,
                () => throw new NotImplementedException("SousProcessus non utilisé dans ces tests"),
                NullLogger<ExecuteurNoeudSousProcessus>.Instance));

        var moteur = new MoteurExecution(
            executeurMetier, executeurInteractif, executeurDecision,
            executeurAttenteTemps, executeurAttenteSignal,
            executeurSousProcessusLazy,
            _repoInstMock.Object, _repoVarMock.Object, _repoEvtMock.Object,
            NullLogger<MoteurExecution>.Instance);

        return new ServiceBpm(
            _repoDefMock.Object, _repoInstMock.Object, _repoVarMock.Object,
            _repoEvtMock.Object, _repoSignalMock.Object, _gestionTacheMock.Object,
            moteur, executeurInteractif, executeurAttenteTemps, executeurAttenteSignal,
            NullLogger<ServiceBpm>.Instance);
    }

    // ── DemarrerAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Demarrer_ProcessusDejaActif_LanceProcessusDejaActifException()
    {
        _repoInstMock.Setup(r => r.ExisteProcessusActifAsync("cmd", 1L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var svc = BuildService();
        var act = async () => await svc.DemarrerAsync("cmd", 1L, null);

        await act.Should().ThrowAsync<ProcessusDejaActifException>();
    }

    [Fact]
    public async Task Demarrer_DefinitionIntrouvable_LanceDefinitionIntrouvableException()
    {
        _repoInstMock.Setup(r => r.ExisteProcessusActifAsync(
            It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _repoDefMock.Setup(r => r.ObtenirDerniereVersionPublieeAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DefinitionProcessus?)null);

        var svc = BuildService();
        var act = async () => await svc.DemarrerAsync("introuvable", 1L, null);

        await act.Should().ThrowAsync<DefinitionIntrouvableException>();
    }

    // ── ObtenirAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Obtenir_InstanceExistante_RetourneInstance()
    {
        var instance = new InstanceProcessus { Id = 42, CleDefinition = "p", Statut = StatutInstance.Active };
        _repoInstMock.Setup(r => r.ObtenirParIdAsync(42L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(instance);

        var svc = BuildService();
        var result = await svc.ObtenirAsync(42L);

        result.Should().BeSameAs(instance);
    }

    [Fact]
    public async Task Obtenir_InstanceIntrouvable_LanceKeyNotFoundException()
    {
        _repoInstMock.Setup(r => r.ObtenirParIdAsync(99L, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstanceProcessus?)null);

        var svc = BuildService();
        var act = async () => await svc.ObtenirAsync(99L);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── ObtenirParAggregateAsync ──────────────────────────────────────────────

    [Fact]
    public async Task ObtenirParAggregate_DelegueAuRepository()
    {
        var instance = new InstanceProcessus { Id = 7, AggregateId = 200 };
        _repoInstMock.Setup(r => r.ObtenirActiveParAggregateAsync("proc", 200L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(instance);

        var svc = BuildService();
        var result = await svc.ObtenirParAggregateAsync("proc", 200L);

        result.Should().BeSameAs(instance);
    }

    // ── TerminerEtapeAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task TerminerEtape_InstanceNonSuspendue_LanceEtatInstanceInvalideException()
    {
        var instance = new InstanceProcessus
        {
            Id = 1, Statut = StatutInstance.Active, IdNoeudCourant = "tache"
        };
        _repoInstMock.Setup(r => r.ObtenirParIdAsync(1L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(instance);

        var svc = BuildService();
        var act = async () => await svc.TerminerEtapeAsync(1L);

        await act.Should().ThrowAsync<EtatInstanceInvalideException>();
    }

    // ── ModifierVariableAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task ModifierVariable_AppelleRepositoryEtEnregistreEvenement()
    {
        _repoVarMock.Setup(r => r.MettreAJourAsync(5L, "statut", "valide", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repoEvtMock.Setup(r => r.AjouterAsync(It.IsAny<EvenementInstance>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var svc = BuildService();
        await svc.ModifierVariableAsync(5L, "statut", "valide");

        _repoVarMock.Verify(r => r.MettreAJourAsync(5L, "statut", "valide",
            It.IsAny<CancellationToken>()), Times.Once);
        _repoEvtMock.Verify(r => r.AjouterAsync(
            It.Is<EvenementInstance>(e => e.TypeEvenement == TypeEvenement.VariableModifiee),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── SauvegarderDefinitionAsync ────────────────────────────────────────────

    [Fact]
    public async Task SauvegarderDefinition_SansBrouillonExistant_AppelleSauvegarder()
    {
        var def = new DefinitionProcessus { Cle = "p", Nom = "Test" };

        _repoDefMock.Setup(r => r.ObtenirBrouillonAsync("p", It.IsAny<CancellationToken>()))
            .ReturnsAsync((DefinitionProcessus?)null);
        _repoDefMock.Setup(r => r.SauvegarderAsync(It.IsAny<DefinitionProcessus>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1L);

        var svc = BuildService();
        await svc.SauvegarderDefinitionAsync(def);

        _repoDefMock.Verify(r => r.SauvegarderAsync(def, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── PublierDefinitionAsync ────────────────────────────────────────────────

    [Fact]
    public async Task PublierDefinition_SansBrouillon_LanceDefinitionIntrouvable()
    {
        _repoDefMock.Setup(r => r.ObtenirBrouillonAsync("p", It.IsAny<CancellationToken>()))
            .ReturnsAsync((DefinitionProcessus?)null);

        var svc = BuildService();
        var act = async () => await svc.PublierDefinitionAsync("p");

        await act.Should().ThrowAsync<DefinitionIntrouvableException>();
    }
}
