namespace TuvInspection.Application.Common.Cqrs;

/// <summary>
/// Marker for a write request. Returns <typeparamref name="TResult"/>.
/// </summary>
public interface ICommand<TResult> { }

/// <summary>Marker for a write request with no result (use Unit-style or just change to result-bearing).</summary>
public interface ICommand : ICommand<Unit> { }

/// <summary>Marker for a read request.</summary>
public interface IQuery<TResult> { }

public interface ICommandHandler<TCommand, TResult> where TCommand : ICommand<TResult>
{
    Task<TResult> Handle(TCommand command, CancellationToken ct);
}

public interface ICommandHandler<TCommand> : ICommandHandler<TCommand, Unit> where TCommand : ICommand { }

public interface IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult>
{
    Task<TResult> Handle(TQuery query, CancellationToken ct);
}

/// <summary>Void-equivalent for handlers. Avoids null-result ambiguity.</summary>
public readonly record struct Unit
{
    public static readonly Unit Value = default;
}
