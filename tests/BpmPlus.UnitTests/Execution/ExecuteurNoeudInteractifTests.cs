using Autofac;
using BpmPlus.Abstractions;
using BpmPlus.Core.Execution;
using BpmPlus.Core.Execution.Executeurs;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace BpmPlus.UnitTests.Execution;

public class ExecuteurNoeudInteractifTests
{
    private static InstanceProcessus InstanceTest(long id = 1) => new()
    {
        Id = id,
        CleDefinition = "test",
        VersionDefinition = 1,
        AggregateId = 100,
        Statut = StatutInstance.Active
    };

    private static IContexteExecution ContexteVide(long idInstance = 1) =>
        new ContexteExecution(idInstance, "test", 1, null,
            new AccesseurVariables(new Dictionary<string, object?>()),
            CancellationToken.None);

    private static ResolveurParametre BuildResolveur(ILifetimeScope scope) =>
        new ResolveurParametre(scope, NullLogger<ResolveurParametre>.Instance);

    private static ExecuteurNoeudMetier BuildExecuteurMetier(ILifetimeScope scope) =>
        new ExecuteurNoeudMetier(
            scope,
            BuildResolveur(scope),
            NullLogger<ExecuteurNoeudMetier>.Instance);

    [Fact]
    public async Task Entrer_SansGestionTacheSansCommandePre_RetourneSuspendu()
    {
        var scope = new ContainerBuilder().Build().BeginLifetimeScope();
        var executeurMetier = BuildExecuteurMetier(scope);
        var executeur = new ExecuteurNoeudInteractif(executeurMetier, null,
            BuildResolveur(scope), NullLogger<ExecuteurNoeudInteractif>.Instance);

        var noeud = new NoeudInteractif
        {
            Id = "tache-1",
            DefinitionTache = new DefinitionTache { Titre = "Valider" },
            FluxSortants = new List<FluxSortant> { new() { Vers = "suite" } }
        };

        var resultat = await executeur.EntrerAsync(
            noeud, InstanceTest(), ContexteVide(), CancellationToken.None);

        resultat.Type.Should().Be(TypeResultatNoeud.Suspendu);
        resultat.NoeudSuivantId.Should().BeNull();
    }

    [Fact]
    public async Task Entrer_AvecGestionTache_AppelleCreerTache()
    {
        var gestionMock = new Mock<IGestionTache>();
        gestionMock.Setup(g => g.CreerTacheAsync(
            It.IsAny<DefinitionTache>(), It.IsAny<InstanceProcessus>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var scope = new ContainerBuilder().Build().BeginLifetimeScope();
        var executeurMetier = BuildExecuteurMetier(scope);
        var executeur = new ExecuteurNoeudInteractif(executeurMetier, gestionMock.Object,
            BuildResolveur(scope), NullLogger<ExecuteurNoeudInteractif>.Instance);

        var noeud = new NoeudInteractif
        {
            Id = "tache-2",
            DefinitionTache = new DefinitionTache { Titre = "Approuver" },
            FluxSortants = new List<FluxSortant> { new() { Vers = "fin" } }
        };

        await executeur.EntrerAsync(noeud, InstanceTest(), ContexteVide(), CancellationToken.None);

        gestionMock.Verify(g => g.CreerTacheAsync(
            It.IsAny<DefinitionTache>(), It.IsAny<InstanceProcessus>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Entrer_AvecLogonAuto_AppelleAssignerTache()
    {
        var gestionMock = new Mock<IGestionTache>();
        gestionMock.Setup(g => g.CreerTacheAsync(
            It.IsAny<DefinitionTache>(), It.IsAny<InstanceProcessus>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        gestionMock.Setup(g => g.AssignerTacheAsync(
            It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var scope = new ContainerBuilder().Build().BeginLifetimeScope();
        var executeur = new ExecuteurNoeudInteractif(
            BuildExecuteurMetier(scope), gestionMock.Object,
            BuildResolveur(scope), NullLogger<ExecuteurNoeudInteractif>.Instance);

        var noeud = new NoeudInteractif
        {
            Id = "tache-auto",
            DefinitionTache = new DefinitionTache { Titre = "Tâche auto", SourceLogonAuto = new SourceValeurStatique("john.doe") },
            FluxSortants = new List<FluxSortant> { new() { Vers = "fin" } }
        };

        await executeur.EntrerAsync(noeud, InstanceTest(5), ContexteVide(5), CancellationToken.None);

        gestionMock.Verify(g => g.AssignerTacheAsync(5L, "john.doe", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Completer_SansGestionTacheSansCommandePost_RetourneNoeudSuivant()
    {
        var scope = new ContainerBuilder().Build().BeginLifetimeScope();
        var executeur = new ExecuteurNoeudInteractif(
            BuildExecuteurMetier(scope), null,
            BuildResolveur(scope), NullLogger<ExecuteurNoeudInteractif>.Instance);

        var noeud = new NoeudInteractif
        {
            Id = "tache-3",
            DefinitionTache = new DefinitionTache { Titre = "OK" },
            FluxSortants = new List<FluxSortant> { new() { Vers = "fin" } }
        };

        var resultat = await executeur.CompleterAsync(
            noeud, InstanceTest(), ContexteVide(), CancellationToken.None);

        resultat.Type.Should().Be(TypeResultatNoeud.Suivant);
        resultat.NoeudSuivantId.Should().Be("fin");
    }

    [Fact]
    public async Task Completer_NoeudFinal_RetourneTermine()
    {
        var scope = new ContainerBuilder().Build().BeginLifetimeScope();
        var executeur = new ExecuteurNoeudInteractif(
            BuildExecuteurMetier(scope), null,
            BuildResolveur(scope), NullLogger<ExecuteurNoeudInteractif>.Instance);

        var noeud = new NoeudInteractif
        {
            Id = "tache-fin",
            EstFinale = true,
            DefinitionTache = new DefinitionTache { Titre = "Fin" },
            FluxSortants = new List<FluxSortant>()
        };

        var resultat = await executeur.CompleterAsync(
            noeud, InstanceTest(), ContexteVide(), CancellationToken.None);

        resultat.Type.Should().Be(TypeResultatNoeud.Termine);
    }

    [Fact]
    public async Task Completer_AvecGestionTache_AppelleFermerTache()
    {
        var gestionMock = new Mock<IGestionTache>();
        gestionMock.Setup(g => g.FermerTacheAsync(
            It.IsAny<InstanceProcessus>(),
            It.IsAny<IReadOnlyDictionary<string, object?>>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var scope = new ContainerBuilder().Build().BeginLifetimeScope();
        var executeur = new ExecuteurNoeudInteractif(
            BuildExecuteurMetier(scope), gestionMock.Object,
            BuildResolveur(scope), NullLogger<ExecuteurNoeudInteractif>.Instance);

        var noeud = new NoeudInteractif
        {
            Id = "tache-fermer",
            DefinitionTache = new DefinitionTache { Titre = "Fermer" },
            FluxSortants = new List<FluxSortant> { new() { Vers = "fin" } }
        };

        await executeur.CompleterAsync(noeud, InstanceTest(), ContexteVide(), CancellationToken.None);

        gestionMock.Verify(g => g.FermerTacheAsync(
            It.IsAny<InstanceProcessus>(),
            It.IsAny<IReadOnlyDictionary<string, object?>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Entrer_AvecVariableLogonVideOuNull_NAssignePas(object? valeurVariable)
    {
        var gestionMock = new Mock<IGestionTache>();
        gestionMock.Setup(g => g.CreerTacheAsync(
            It.IsAny<DefinitionTache>(), It.IsAny<InstanceProcessus>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var scope = new ContainerBuilder().Build().BeginLifetimeScope();
        var executeur = new ExecuteurNoeudInteractif(
            BuildExecuteurMetier(scope), gestionMock.Object,
            BuildResolveur(scope), NullLogger<ExecuteurNoeudInteractif>.Instance);

        var noeud = new NoeudInteractif
        {
            Id = "tache-vide",
            DefinitionTache = new DefinitionTache { Titre = "Tâche", SourceLogonAuto = new SourceVariable("responsable") },
            FluxSortants = new List<FluxSortant> { new() { Vers = "fin" } }
        };

        var contexte = new ContexteExecution(1, "test", 1, null,
            new AccesseurVariables(new Dictionary<string, object?> { ["responsable"] = valeurVariable }),
            CancellationToken.None);

        await executeur.EntrerAsync(noeud, InstanceTest(), contexte, CancellationToken.None);

        gestionMock.Verify(g => g.AssignerTacheAsync(
            It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Entrer_AvecLogonDepuisVariable_AssigneLeLogonResolue()
    {
        var gestionMock = new Mock<IGestionTache>();
        gestionMock.Setup(g => g.CreerTacheAsync(
            It.IsAny<DefinitionTache>(), It.IsAny<InstanceProcessus>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        gestionMock.Setup(g => g.AssignerTacheAsync(
            It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var scope = new ContainerBuilder().Build().BeginLifetimeScope();
        var executeur = new ExecuteurNoeudInteractif(
            BuildExecuteurMetier(scope), gestionMock.Object,
            BuildResolveur(scope), NullLogger<ExecuteurNoeudInteractif>.Instance);

        var noeud = new NoeudInteractif
        {
            Id = "tache-var",
            DefinitionTache = new DefinitionTache { Titre = "Tâche variable", SourceLogonAuto = new SourceVariable("responsable") },
            FluxSortants = new List<FluxSortant> { new() { Vers = "fin" } }
        };

        var contexte = new ContexteExecution(7, "test", 1, null,
            new AccesseurVariables(new Dictionary<string, object?> { ["responsable"] = "marie.dupont" }),
            CancellationToken.None);

        await executeur.EntrerAsync(noeud, InstanceTest(7), contexte, CancellationToken.None);

        gestionMock.Verify(g => g.AssignerTacheAsync(7L, "marie.dupont", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
