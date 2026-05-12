using Autofac;
using BpmPlus.Abstractions;
using BpmPlus.Core.Execution;
using BpmPlus.Core.Execution.Executeurs;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BpmPlus.UnitTests.Execution;

public class ExecuteurNoeudDecisionTests
{
    private readonly ExecuteurNoeudDecision _executeur;

    public ExecuteurNoeudDecisionTests()
    {
        var scopeMock = new Mock<ILifetimeScope>();
        var resolveur = new ResolveurParametre(scopeMock.Object, NullLogger<ResolveurParametre>.Instance);
        _executeur = new ExecuteurNoeudDecision(resolveur, NullLogger<ExecuteurNoeudDecision>.Instance);
    }

    private static IContexteExecution ContexteAvec(Dictionary<string, object?> vars)
    {
        var acc = new AccesseurVariables(vars);
        return new ContexteExecution(1, "test", 1, null, acc, CancellationToken.None);
    }

    [Fact]
    public async Task Executer_ConditionEgaleVraie_RetourneNoeudSuivantCorrespondant()
    {
        var noeud = NoeudDecisionAvec(
            flux: new FluxSortant { Condition = new ConditionVariable("statut", Operateur.Egal, "ok"), Vers = "fin-ok" },
            defaut: "fin-ko");

        var resultat = await _executeur.ExecuterAsync(
            noeud,
            ContexteAvec(new Dictionary<string, object?> { ["statut"] = "ok" }),
            CancellationToken.None);

        resultat.Type.Should().Be(TypeResultatNoeud.Suivant);
        resultat.NoeudSuivantId.Should().Be("fin-ok");
    }

    [Fact]
    public async Task Executer_AucuneConditionVraie_PrendBrancheParDefaut()
    {
        var noeud = NoeudDecisionAvec(
            flux: new FluxSortant { Condition = new ConditionVariable("statut", Operateur.Egal, "ok"), Vers = "fin-ok" },
            defaut: "fin-ko");

        var resultat = await _executeur.ExecuterAsync(
            noeud,
            ContexteAvec(new Dictionary<string, object?> { ["statut"] = "refuse" }),
            CancellationToken.None);

        resultat.Type.Should().Be(TypeResultatNoeud.Suivant);
        resultat.NoeudSuivantId.Should().Be("fin-ko");
    }

    [Fact]
    public async Task Executer_AucunCheminEtSansBrancheDefaut_LanceAucunCheminException()
    {
        var noeud = new NoeudDecision
        {
            Id = "d",
            FluxSortants = new List<FluxSortant>
            {
                new() { Condition = new ConditionVariable("x", Operateur.Egal, 1), Vers = "a" }
            }
        };

        var act = async () => await _executeur.ExecuterAsync(
            noeud,
            ContexteAvec(new Dictionary<string, object?> { ["x"] = 99 }),
            CancellationToken.None);

        await act.Should().ThrowAsync<AucunCheminException>()
            .WithMessage("*d*");
    }

    [Theory]
    [InlineData(Operateur.Superieur, 10, 5)]
    [InlineData(Operateur.Inferieur, 3, 5)]
    [InlineData(Operateur.SuperieurOuEgal, 5, 5)]
    [InlineData(Operateur.InferieurOuEgal, 4, 5)]
    [InlineData(Operateur.Different, "a", "b")]
    public async Task Executer_ConditionNumeriqueSatisfaite_RetourneNoeudOk(
        Operateur operateur, object valeurInstance, object valeurCondition)
    {
        var noeud = NoeudDecisionAvec(
            flux: new FluxSortant { Condition = new ConditionVariable("v", operateur, valeurCondition), Vers = "ok" },
            defaut: "ko");

        var resultat = await _executeur.ExecuterAsync(
            noeud,
            ContexteAvec(new Dictionary<string, object?> { ["v"] = valeurInstance }),
            CancellationToken.None);

        resultat.NoeudSuivantId.Should().Be("ok");
    }

    [Fact]
    public async Task Executer_ConditionContient_ChaineContenue_RetourneOk()
    {
        var noeud = NoeudDecisionAvec(
            flux: new FluxSortant
            {
                Condition = new ConditionVariable("message", Operateur.Contient, "erreur"),
                Vers = "ok"
            },
            defaut: "ko");

        var resultat = await _executeur.ExecuterAsync(
            noeud,
            ContexteAvec(new Dictionary<string, object?> { ["message"] = "Une erreur critique" }),
            CancellationToken.None);

        resultat.NoeudSuivantId.Should().Be("ok");
    }

    [Fact]
    public async Task Executer_PlusieursBranches_PremiereConditionVraieGagne()
    {
        var noeud = new NoeudDecision
        {
            Id = "d",
            FluxSortants = new List<FluxSortant>
            {
                new() { Condition = new ConditionVariable("score", Operateur.SuperieurOuEgal, 90), Vers = "excellent" },
                new() { Condition = new ConditionVariable("score", Operateur.SuperieurOuEgal, 70), Vers = "bien" },
                new() { EstParDefaut = true, Vers = "insuffisant" }
            }
        };

        var resultat = await _executeur.ExecuterAsync(
            noeud,
            ContexteAvec(new Dictionary<string, object?> { ["score"] = 95 }),
            CancellationToken.None);

        resultat.NoeudSuivantId.Should().Be("excellent");
    }

    private static NoeudDecision NoeudDecisionAvec(FluxSortant flux, string defaut) =>
        new()
        {
            Id = "decision",
            FluxSortants = new List<FluxSortant>
            {
                flux,
                new() { EstParDefaut = true, Vers = defaut }
            }
        };
}
