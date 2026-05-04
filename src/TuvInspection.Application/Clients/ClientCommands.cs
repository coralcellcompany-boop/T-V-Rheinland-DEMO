using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Contracts.Clients;
using TuvInspection.Contracts.Common;

namespace TuvInspection.Application.Clients;

public sealed record ListClientsQuery(string? Search, int Page, int PageSize)
    : IQuery<PagedResult<ClientListItemDto>>;

public sealed record GetClientByIdQuery(Guid Id) : IQuery<ClientDetailDto?>;

public sealed record CreateClientCommand(CreateClientRequest Body) : ICommand<ClientDetailDto>;

public sealed record UpdateClientCommand(Guid Id, UpdateClientRequest Body) : ICommand<ClientDetailDto>;

public sealed record DeleteClientCommand(Guid Id) : ICommand<Unit>;
