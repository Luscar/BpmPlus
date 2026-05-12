using Autofac;
using BpmPlus.Abstractions;
using BpmPlus.Core.Execution;
using BpmPlus.Core.Execution.Executeurs;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BpmPlus.UnitTests.Execution;

public class ExecuteurNoeudMetierTests
{
    private ILifetimeScope BuildScope(IBpmHandlerCommande handler)
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance(handler)
            .As<IBpmHandlerCommande>()
            .Keyed<IBpmHandlerCommande>(handler.NomCommande);
        return builder.Build().BeginLifetimeScope();
    }

    private static IContexteExecution ContexteVide() =>
        new ContexteExecution(1, "test", 1, 42L, new AccesseurVariables(new()), CancellationToken.None);

    [Fact]
    public async Task Executer_NoeudAvecFluxSortant_RetourneNoeudSuivant()
    {
        var handlerMock = new Mock<IBpmHandlerCommande>();
        handlerMock.Setup(h => h.NomCommande).Returns("ValiderCommand");
        handlerMock.Setup(h => h.ExecuterAsync(
            It.IsAny<long>(), It.IsAny<long?>(),
            It.IsAny<IReadOnlyDictionary<string, object?>>(),
            It.IsAny<IContexteExecution>()))
            .Returns(Task.CompletedTask);

        var scope = BuildScope(handlerMock.Object);
        var resolveur = new ResolveurParametre(scope, NullLogger<ResolveurParametre>.Instance);
        var executeur = new ExecuteurNoeudMetier(scope, resolveur, NullLogger<ExecuteurNoeudMetier>.Instance);

        var noeud = new NoeudMetier
        {
            Id = "valider",
            NomCommande = "ValiderCommand",
            Parametres = new Dictionary<string, ISourceParametre>(),
            FluxSortants = new List<FluxSortant> { new() { Vers = "suite" } }
        };

        var resultat = await executeur.ExecuterAsync(noeud, ContexteVide(), CancellationToken.None);

        resultat.Type.Should().Be(TypeResultatNoeud.Suivant);
        resultat.NoeudSuivantId.Should().Be("suite");
        handlerMock.Verify(h => h.ExecuterAsync(
            It.IsAny<long>(), It.IsAny<long?>(),
            It.IsAny<IReadOnlyDictionary<string, object?>>(),
            It.IsAny<IContexteExecution>()), Times.Once);
    }

    [Fact]
    public async Task Executer_NoeudFinal_RetourneTermine()
    {
        var handlerMock = new Mock<IBpmHandlerCommande>();
        handlerMock.Setup(h => h.NomCommande).Returns("FinCommand");
        handlerMock.Setup(h => h.ExecuterAsync(
            It.IsAny<long>(), It.IsAny<long?>(),
            It.IsAny<IReadOnlyDictionary<string, object?>>(),
            It.IsAny<IContexteExecution>()))
            .Returns(Task.CompletedTask);

        var scope = BuildScope(handlerMock.Object);
        var resolveur = new ResolveurParametre(scope, NullLogger<ResolveurParametre>.Instance);
        var executeur = new ExecuteurNoeudMetier(scope, resolveur, NullLogger<ExecuteurNoeudMetier>.Instance);

        var noeud = new NoeudMetier
        {
            Id = "fin",
            NomCommande = "FinCommand",
            EstFinale = true,
            Parametres = new Dictionary<string, ISourceParametre>(),
            FluxSortants = new List<FluxSortant>()
        };

        var resultat = await executeur.ExecuterAsync(noeud, ContexteVide(), CancellationToken.None);

        resultat.Type.Should().Be(TypeResultatNoeud.Termine);
    }

    [Fact]
    public async Task Executer_AvecParametreVariable_PasseValeurAuHandler()
    {
        object? parametresRecus = null;

        var handlerMock = new Mock<IBpmHandlerCommande>();
        handlerMock.Setup(h => h.NomCommande).Returns("TestCommand");
        handlerMock.Setup(h => h.ExecuterAsync(
            It.IsAny<long>(), It.IsAny<long?>(),
            It.IsAny<IReadOnlyDictionary<string, object?>>(),
            It.IsAny<IContexteExecution>()))
            .Callback<long, long?, IReadOnlyDictionary<string, object?>, IContexteExecution>(
                (_, _, p, _) => parametresRecus = p)
            .Returns(Task.CompletedTask);

        var scope = BuildScope(handlerMock.Object);
        var resolveur = new ResolveurParametre(scope, NullLogger<ResolveurParametre>.Instance);
        var executeur = new ExecuteurNoeudMetier(scope, resolveur, NullLogger<ExecuteurNoeudMetier>.Instance);

        var noeud = new NoeudMetier
        {
            Id = "test",
            NomCommande = "TestCommand",
            EstFinale = true,
            Parametres = new Dictionary<string, ISourceParametre>
            {
                ["montant"] = new SourceVariable("montant"),
                ["devise"] = new SourceValeurStatique("EUR")
            }
        };

        var contexte = new ContexteExecution(1, "p", 1, null,
            new AccesseurVariables(new Dictionary<string, object?> { ["montant"] = 500 }),
            CancellationToken.None);

        await executeur.ExecuterAsync(noeud, contexte, CancellationToken.None);

        var p = (IReadOnlyDictionary<string, object?>)parametresRecus!;
        p["montant"].Should().Be(500);
        p["devise"].Should().Be("EUR");
    }

    [Fact]
    public async Task Executer_CommandeInconnue_LanceInvalidOperation()
    {
        var scope = new ContainerBuilder().Build().BeginLifetimeScope();
        var resolveur = new ResolveurParametre(scope, NullLogger<ResolveurParametre>.Instance);
        var executeur = new ExecuteurNoeudMetier(scope, resolveur, NullLogger<ExecuteurNoeudMetier>.Instance);

        var noeud = new NoeudMetier
        {
            Id = "n",
            NomCommande = "CommandeInexistante",
            Parametres = new Dictionary<string, ISourceParametre>(),
        };

        var act = async () => await executeur.ExecuterAsync(noeud, ContexteVide(), CancellationToken.None);
        await act.Should().ThrowAsync<Exception>();
    }
}
