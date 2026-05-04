using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TuvInspection.Application.Clients;
using TuvInspection.Application.Common;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Application.Common.Time;
using TuvInspection.Contracts.Clients;
using TuvInspection.Contracts.Common;
using TuvInspection.Domain.Clients;
using TuvInspection.Domain.Identity;
using TuvInspection.Infrastructure.Persistence;

namespace TuvInspection.Infrastructure.Clients;

public sealed class ListClientsHandler : IQueryHandler<ListClientsQuery, PagedResult<ClientListItemDto>>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;

    public ListClientsHandler(AppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<PagedResult<ClientListItemDto>> Handle(ListClientsQuery q, CancellationToken ct)
    {
        // Manager sees all clients; everyone else sees only their assigned ones.
        IQueryable<Client> query = _db.Clients.AsNoTracking();

        if (!_tenant.IsInRole(Roles.Manager))
        {
            var ids = _tenant.AssignedClientIds;
            query = query.Where(c => ids.Contains(c.Id));
        }

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            query = query.Where(c =>
                EF.Functions.Like(c.Name, $"%{s}%") ||
                EF.Functions.Like(c.Code, $"%{s.ToUpper()}%"));
        }

        var total = await query.CountAsync(ct);
        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, 200);

        var items = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => new ClientListItemDto(
                c.Id, c.Name, c.Code,
                (ContractStatusDto)c.ContractStatus,
                (ServiceTypeDto)(int)c.AllowedServices,
                c.ContactName, c.ContactEmail, c.CreatedAtUtc))
            .ToListAsync(ct);

        return new PagedResult<ClientListItemDto>(items, total, page, pageSize);
    }
}

public sealed class GetClientByIdHandler : IQueryHandler<GetClientByIdQuery, ClientDetailDto?>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;

    public GetClientByIdHandler(AppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<ClientDetailDto?> Handle(GetClientByIdQuery q, CancellationToken ct)
    {
        var c = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == q.Id, ct);
        if (c is null) return null;
        if (!_tenant.IsInRole(Roles.Manager) && !_tenant.AssignedClientIds.Contains(c.Id))
            return null;
        return c.ToDetail();
    }
}

public sealed class CreateClientHandler : ICommandHandler<CreateClientCommand, ClientDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly IValidator<CreateClientRequest> _validator;

    public CreateClientHandler(AppDbContext db, ITenantContext tenant, IClock clock,
        IValidator<CreateClientRequest> validator)
    {
        _db = db;
        _tenant = tenant;
        _clock = clock;
        _validator = validator;
    }

    public async Task<ClientDetailDto> Handle(CreateClientCommand command, CancellationToken ct)
    {
        if (!_tenant.IsInRole(Roles.Manager))
            throw new UnauthorizedAccessException("Only Manager may create clients.");
        await _validator.ValidateAndThrowAsync(command.Body, ct);

        var dup = await _db.Clients.AnyAsync(c => c.Code == command.Body.Code.ToUpperInvariant(), ct);
        if (dup) throw new ArgumentException($"Client code '{command.Body.Code}' is already used.");

        var entity = new Client(Guid.NewGuid(), command.Body.Name, command.Body.Code);
        entity.UpdateAddress(command.Body.Address);
        entity.UpdateContact(command.Body.ContactName, command.Body.ContactPhone, command.Body.ContactEmail);
        entity.SetContractStatus((ContractStatus)command.Body.ContractStatus);
        entity.SetAllowedServices((ServiceType)(int)command.Body.AllowedServices);
        entity.CreatedAtUtc = _clock.UtcNow;
        entity.CreatedById = _tenant.UserId;

        _db.Clients.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity.ToDetail();
    }
}

public sealed class UpdateClientHandler : ICommandHandler<UpdateClientCommand, ClientDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly IValidator<UpdateClientRequest> _validator;

    public UpdateClientHandler(AppDbContext db, ITenantContext tenant, IClock clock,
        IValidator<UpdateClientRequest> validator)
    {
        _db = db;
        _tenant = tenant;
        _clock = clock;
        _validator = validator;
    }

    public async Task<ClientDetailDto> Handle(UpdateClientCommand command, CancellationToken ct)
    {
        if (!_tenant.IsInRole(Roles.Manager))
            throw new UnauthorizedAccessException("Only Manager may update clients.");
        await _validator.ValidateAndThrowAsync(command.Body, ct);

        var entity = await _db.Clients.FirstOrDefaultAsync(c => c.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"Client {command.Id} not found.");

        entity.Rename(command.Body.Name);
        entity.UpdateAddress(command.Body.Address);
        entity.UpdateContact(command.Body.ContactName, command.Body.ContactPhone, command.Body.ContactEmail);
        entity.SetContractStatus((ContractStatus)command.Body.ContractStatus);
        entity.SetAllowedServices((ServiceType)(int)command.Body.AllowedServices);
        entity.UpdatedAtUtc = _clock.UtcNow;
        entity.UpdatedById = _tenant.UserId;

        await _db.SaveChangesAsync(ct);
        return entity.ToDetail();
    }
}

public sealed class DeleteClientHandler : ICommandHandler<DeleteClientCommand, Unit>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;

    public DeleteClientHandler(AppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<Unit> Handle(DeleteClientCommand command, CancellationToken ct)
    {
        if (!_tenant.IsInRole(Roles.Manager))
            throw new UnauthorizedAccessException("Only Manager may delete clients.");

        var entity = await _db.Clients.FirstOrDefaultAsync(c => c.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"Client {command.Id} not found.");

        // Refuse to delete if the client has equipment / certificates.
        var hasEquipment = await _db.Equipment.IgnoreQueryFilters().AnyAsync(e => e.ClientId == command.Id, ct);
        var hasCerts = await _db.Certificates.IgnoreQueryFilters().AnyAsync(c => c.ClientId == command.Id, ct);
        if (hasEquipment || hasCerts)
            throw new InvalidOperationException(
                "Client has equipment or certificates and cannot be deleted. Mark contract as Terminated instead.");

        _db.Clients.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
