namespace BpmPlus.Abstractions;

/// <summary>
/// Abstraction pour la résolution des handlers BPM, indépendante du conteneur DI.
/// </summary>
public interface IBpmServiceResolver
{
    IBpmHandlerCommande? GetCommande(string nomCommande);
    IBpmHandlerQuery? GetQuery(string nomQuery);
    IBpmHandlerQuery<TResultat>? GetQuery<TResultat>(string nomQuery);
}
