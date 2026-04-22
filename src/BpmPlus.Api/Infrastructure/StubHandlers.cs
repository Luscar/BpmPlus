using BpmPlus.Abstractions;

namespace BpmPlus.Api.Infrastructure;

// Stub handlers for demo seed data — no business logic, just allow the process engine
// to advance through NoeudMetier nodes so seeded instances reach their suspended state.

file sealed class ValiderCommandeHandler : IBpmHandlerCommande
{
    public string NomCommande => "ValiderCommandeCommand";
    public Task ExecuterAsync(long idInstance, long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres, IContexteExecution contexte)
        => Task.CompletedTask;
}

file sealed class CreerCompteHandler : IBpmHandlerCommande
{
    public string NomCommande => "CreerCompteCommand";
    public Task ExecuterAsync(long idInstance, long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres, IContexteExecution contexte)
        => Task.CompletedTask;
}

file sealed class NotificationApprobationHandler : IBpmHandlerCommande
{
    public string NomCommande => "NotificationApprobationCommand";
    public Task ExecuterAsync(long idInstance, long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres, IContexteExecution contexte)
        => Task.CompletedTask;
}

file sealed class NotificationRefusHandler : IBpmHandlerCommande
{
    public string NomCommande => "NotificationRefusCommand";
    public Task ExecuterAsync(long idInstance, long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres, IContexteExecution contexte)
        => Task.CompletedTask;
}

file sealed class NotificationFinHandler : IBpmHandlerCommande
{
    public string NomCommande => "NotificationFinCommand";
    public Task ExecuterAsync(long idInstance, long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres, IContexteExecution contexte)
        => Task.CompletedTask;
}

file sealed class QuizFinalHandler : IBpmHandlerCommande
{
    public string NomCommande => "QuizFinalCommand";
    public Task ExecuterAsync(long idInstance, long? aggregateId,
        IReadOnlyDictionary<string, object?> parametres, IContexteExecution contexte)
        => Task.CompletedTask;
}
