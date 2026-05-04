namespace TuvInspection.Application.Common.Cqrs;

/// <summary>
/// Resolves the appropriate command/query handler from DI and invokes it.
/// Implementation lives in Infrastructure (uses IServiceProvider via Scrutor-registered handlers).
/// </summary>
public interface IDispatcher
{
    Task<TResult> Send<TResult>(ICommand<TResult> command, CancellationToken ct = default);
    Task<TResult> Query<TResult>(IQuery<TResult> query, CancellationToken ct = default);
}
