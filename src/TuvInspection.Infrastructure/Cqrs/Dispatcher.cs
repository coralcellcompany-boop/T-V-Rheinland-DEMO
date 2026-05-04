using Microsoft.Extensions.DependencyInjection;
using TuvInspection.Application.Common.Cqrs;

namespace TuvInspection.Infrastructure.Cqrs;

public sealed class Dispatcher : IDispatcher
{
    private readonly IServiceProvider _sp;
    public Dispatcher(IServiceProvider sp) => _sp = sp;

    public Task<TResult> Send<TResult>(ICommand<TResult> command, CancellationToken ct = default)
    {
        var cmdType = command.GetType();
        var handlerType = typeof(ICommandHandler<,>).MakeGenericType(cmdType, typeof(TResult));
        var handler = _sp.GetRequiredService(handlerType);
        var method = handlerType.GetMethod(nameof(ICommandHandler<ICommand<TResult>, TResult>.Handle))!;
        return (Task<TResult>)method.Invoke(handler, new object[] { command, ct })!;
    }

    public Task<TResult> Query<TResult>(IQuery<TResult> query, CancellationToken ct = default)
    {
        var qType = query.GetType();
        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(qType, typeof(TResult));
        var handler = _sp.GetRequiredService(handlerType);
        var method = handlerType.GetMethod(nameof(IQueryHandler<IQuery<TResult>, TResult>.Handle))!;
        return (Task<TResult>)method.Invoke(handler, new object[] { query, ct })!;
    }
}
