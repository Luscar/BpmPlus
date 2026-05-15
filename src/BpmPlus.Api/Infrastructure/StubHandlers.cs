using BpmPlus.Abstractions;

namespace BpmPlus.Api.Infrastructure;

// Stub handlers for demo seed data — no business logic, just allow the process engine
// to advance through NoeudMetier nodes so seeded instances reach their suspended state.

[BpmCommande("ValiderCommandeCommand")]
file sealed class ValiderCommandeHandler : IBpmHandlerCommande
{
    public Task ExecuterAsync(long idInstance, long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres, IContexteExecution contexte)
        => Task.CompletedTask;
}

[BpmCommande("CreerCompteCommand")]
file sealed class CreerCompteHandler : IBpmHandlerCommande
{
    public Task ExecuterAsync(long idInstance, long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres, IContexteExecution contexte)
        => Task.CompletedTask;
}

[BpmCommande("NotificationApprobationCommand")]
file sealed class NotificationApprobationHandler : IBpmHandlerCommande
{
    public Task ExecuterAsync(long idInstance, long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres, IContexteExecution contexte)
        => Task.CompletedTask;
}

[BpmCommande("NotificationRefusCommand")]
file sealed class NotificationRefusHandler : IBpmHandlerCommande
{
    public Task ExecuterAsync(long idInstance, long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres, IContexteExecution contexte)
        => Task.CompletedTask;
}

[BpmCommande("NotificationFinCommand")]
file sealed class NotificationFinHandler : IBpmHandlerCommande
{
    public Task ExecuterAsync(long idInstance, long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres, IContexteExecution contexte)
        => Task.CompletedTask;
}

[BpmCommande("QuizFinalCommand")]
file sealed class QuizFinalHandler : IBpmHandlerCommande
{
    public Task ExecuterAsync(long idInstance, long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres, IContexteExecution contexte)
        => Task.CompletedTask;
}
