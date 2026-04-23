using Autofac;
using BpmPlus.Abstractions;

namespace BpmPlus.Registration;

internal sealed class AutofacBpmServiceResolver : IBpmServiceResolver
{
    private readonly ILifetimeScope _scope;

    public AutofacBpmServiceResolver(ILifetimeScope scope) => _scope = scope;

    public IBpmHandlerCommande? GetCommande(string nomCommande)
        => _scope.TryResolveKeyed<IBpmHandlerCommande>(nomCommande, out var h) ? h : null;

    public IBpmHandlerQuery? GetQuery(string nomQuery)
        => _scope.TryResolveKeyed<IBpmHandlerQuery>(nomQuery, out var h) ? h : null;

    public IBpmHandlerQuery<TResultat>? GetQuery<TResultat>(string nomQuery)
        => _scope.TryResolveKeyed<IBpmHandlerQuery<TResultat>>(nomQuery, out var h) ? h : null;
}
