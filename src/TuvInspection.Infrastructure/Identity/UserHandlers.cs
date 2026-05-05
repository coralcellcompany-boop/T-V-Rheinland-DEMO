using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TuvInspection.Application.Common;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Application.Common.Time;
using TuvInspection.Application.Users;
using TuvInspection.Contracts.Users;
using TuvInspection.Domain.Identity;

namespace TuvInspection.Infrastructure.Identity;

public sealed class ListUsersHandler : IQueryHandler<ListUsersQuery, IReadOnlyList<UserListItemDto>>
{
    private readonly UserManager<ApplicationUser> _users;
    public ListUsersHandler(UserManager<ApplicationUser> users) => _users = users;

    public async Task<IReadOnlyList<UserListItemDto>> Handle(ListUsersQuery q, CancellationToken ct)
    {
        var query = _users.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(u =>
                (u.UserName != null && u.UserName.ToLower().Contains(s)) ||
                (u.Email != null && u.Email.ToLower().Contains(s)) ||
                (u.FullName != null && u.FullName.ToLower().Contains(s)));
        }
        var rows = await query.OrderBy(u => u.UserName).ToListAsync(ct);
        var result = new List<UserListItemDto>(rows.Count);
        foreach (var u in rows)
        {
            var roles = await _users.GetRolesAsync(u);
            result.Add(ToDto(u, roles));
        }
        return result;
    }

    internal static UserListItemDto ToDto(ApplicationUser u, IList<string> roles)
    {
        var clientIds = (u.AssignedClientIdsCsv ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .ToList();
        return new UserListItemDto(
            u.Id, u.UserName ?? "", u.Email,
            u.FullName, u.SapNo, u.CertNo,
            u.IsActive,
            IsLockedOut: u.LockoutEnd is not null && u.LockoutEnd > DateTimeOffset.UtcNow,
            roles.ToList(), clientIds, u.CreatedAtUtc);
    }
}

public sealed class GetUserByIdHandler : IQueryHandler<GetUserByIdQuery, UserListItemDto?>
{
    private readonly UserManager<ApplicationUser> _users;
    public GetUserByIdHandler(UserManager<ApplicationUser> users) => _users = users;

    public async Task<UserListItemDto?> Handle(GetUserByIdQuery q, CancellationToken ct)
    {
        var u = await _users.FindByIdAsync(q.Id);
        if (u is null) return null;
        var roles = await _users.GetRolesAsync(u);
        return ListUsersHandler.ToDto(u, roles);
    }
}

public sealed class CreateUserHandler : ICommandHandler<CreateUserCommand, UserListItemDto>
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly IValidator<CreateUserRequest> _validator;

    public CreateUserHandler(UserManager<ApplicationUser> users, ITenantContext tenant, IClock clock,
        IValidator<CreateUserRequest> validator)
    { _users = users; _tenant = tenant; _clock = clock; _validator = validator; }

    public async Task<UserListItemDto> Handle(CreateUserCommand command, CancellationToken ct)
    {
        if (!_tenant.IsInRole(Roles.Manager))
            throw new UnauthorizedAccessException("Only Manager may create users.");
        await _validator.ValidateAndThrowAsync(command.Body, ct);
        ValidateRoleNames(command.Body.Roles);

        var existing = await _users.FindByEmailAsync(command.Body.Email);
        if (existing is not null)
            throw new ArgumentException($"A user with email {command.Body.Email} already exists.");

        var user = new ApplicationUser
        {
            UserName = command.Body.Email,
            Email = command.Body.Email,
            EmailConfirmed = true,
            FullName = command.Body.FullName,
            SapNo = command.Body.SapNo,
            CertNo = command.Body.CertNo,
            IsActive = true,
            CreatedAtUtc = _clock.UtcNow,
            AssignedClientIdsCsv = string.Join(",", command.Body.AssignedClientIds ?? new List<Guid>()),
        };

        var create = await _users.CreateAsync(user, command.Body.Password);
        if (!create.Succeeded) throw new ArgumentException(JoinErrors(create));

        var addRoles = await _users.AddToRolesAsync(user, command.Body.Roles);
        if (!addRoles.Succeeded) throw new ArgumentException(JoinErrors(addRoles));

        return ListUsersHandler.ToDto(user, command.Body.Roles.ToList());
    }

    internal static void ValidateRoleNames(IReadOnlyList<string> roles)
    {
        var unknown = roles.Where(r => !Roles.All.Contains(r)).ToList();
        if (unknown.Count > 0)
            throw new ArgumentException($"Unknown role(s): {string.Join(", ", unknown)}.");
    }

    internal static string JoinErrors(IdentityResult r) =>
        string.Join("; ", r.Errors.Select(e => e.Description));
}

public sealed class UpdateUserHandler : ICommandHandler<UpdateUserCommand, UserListItemDto>
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ITenantContext _tenant;
    private readonly IValidator<UpdateUserRequest> _validator;

    public UpdateUserHandler(UserManager<ApplicationUser> users, ITenantContext tenant,
        IValidator<UpdateUserRequest> validator)
    { _users = users; _tenant = tenant; _validator = validator; }

    public async Task<UserListItemDto> Handle(UpdateUserCommand command, CancellationToken ct)
    {
        if (!_tenant.IsInRole(Roles.Manager))
            throw new UnauthorizedAccessException("Only Manager may update users.");
        await _validator.ValidateAndThrowAsync(command.Body, ct);
        CreateUserHandler.ValidateRoleNames(command.Body.Roles);

        var u = await _users.FindByIdAsync(command.Id)
            ?? throw new KeyNotFoundException($"User {command.Id} not found.");

        u.FullName = command.Body.FullName;
        u.SapNo = command.Body.SapNo;
        u.CertNo = command.Body.CertNo;
        u.IsActive = command.Body.IsActive;
        u.AssignedClientIdsCsv = string.Join(",", command.Body.AssignedClientIds);

        var save = await _users.UpdateAsync(u);
        if (!save.Succeeded) throw new ArgumentException(CreateUserHandler.JoinErrors(save));

        // Sync roles: replace the current set with the requested one.
        var current = await _users.GetRolesAsync(u);
        var toRemove = current.Except(command.Body.Roles).ToList();
        var toAdd = command.Body.Roles.Except(current).ToList();
        if (toRemove.Count > 0)
        {
            var r = await _users.RemoveFromRolesAsync(u, toRemove);
            if (!r.Succeeded) throw new ArgumentException(CreateUserHandler.JoinErrors(r));
        }
        if (toAdd.Count > 0)
        {
            var r = await _users.AddToRolesAsync(u, toAdd);
            if (!r.Succeeded) throw new ArgumentException(CreateUserHandler.JoinErrors(r));
        }

        return ListUsersHandler.ToDto(u, command.Body.Roles.ToList());
    }
}

public sealed class ResetUserPasswordHandler : ICommandHandler<ResetUserPasswordCommand, Unit>
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ITenantContext _tenant;
    private readonly IValidator<ResetPasswordRequest> _validator;

    public ResetUserPasswordHandler(UserManager<ApplicationUser> users, ITenantContext tenant,
        IValidator<ResetPasswordRequest> validator)
    { _users = users; _tenant = tenant; _validator = validator; }

    public async Task<Unit> Handle(ResetUserPasswordCommand command, CancellationToken ct)
    {
        if (!_tenant.IsInRole(Roles.Manager))
            throw new UnauthorizedAccessException("Only Manager may reset passwords.");
        await _validator.ValidateAndThrowAsync(command.Body, ct);

        var u = await _users.FindByIdAsync(command.Id)
            ?? throw new KeyNotFoundException($"User {command.Id} not found.");

        var token = await _users.GeneratePasswordResetTokenAsync(u);
        var result = await _users.ResetPasswordAsync(u, token, command.Body.NewPassword);
        if (!result.Succeeded) throw new ArgumentException(CreateUserHandler.JoinErrors(result));

        return Unit.Value;
    }
}

public sealed class GetUserLicenseHandler : IQueryHandler<GetUserLicenseQuery, UserLicenseDto?>
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly IClock _clock;
    public GetUserLicenseHandler(UserManager<ApplicationUser> users, IClock clock)
    { _users = users; _clock = clock; }

    public async Task<UserLicenseDto?> Handle(GetUserLicenseQuery q, CancellationToken ct)
    {
        var u = await _users.FindByIdAsync(q.Id);
        return u is null ? null : ToDto(u, _clock);
    }

    public static UserLicenseDto ToDto(ApplicationUser u, IClock clock)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var isValid = u.LicenseValidUntil is { } until
            && (u.LicenseValidFrom is null || u.LicenseValidFrom.Value <= today)
            && until >= today
            && !string.IsNullOrWhiteSpace(u.LicenseNumber);
        var days = u.LicenseValidUntil is { } d ? d.DayNumber - today.DayNumber : (int?)null;
        return new UserLicenseDto(
            u.LicenseNumber, u.LicenseAuthority, u.LicenseScope,
            u.LicenseValidFrom, u.LicenseValidUntil, isValid, days);
    }
}

public sealed class UpdateUserLicenseHandler : ICommandHandler<UpdateUserLicenseCommand, UserLicenseDto>
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    public UpdateUserLicenseHandler(UserManager<ApplicationUser> users, ITenantContext tenant, IClock clock)
    { _users = users; _tenant = tenant; _clock = clock; }

    public async Task<UserLicenseDto> Handle(UpdateUserLicenseCommand command, CancellationToken ct)
    {
        if (!_tenant.IsInRole(Roles.Manager))
            throw new UnauthorizedAccessException("Only Manager may edit licence information.");

        var u = await _users.FindByIdAsync(command.Id)
            ?? throw new KeyNotFoundException($"User {command.Id} not found.");

        if (command.Body.ValidFrom is { } from && command.Body.ValidUntil is { } until && until < from)
            throw new ArgumentException("Licence valid-until must be on or after valid-from.");

        u.LicenseNumber = string.IsNullOrWhiteSpace(command.Body.LicenseNumber) ? null : command.Body.LicenseNumber.Trim();
        u.LicenseAuthority = string.IsNullOrWhiteSpace(command.Body.LicenseAuthority) ? null : command.Body.LicenseAuthority.Trim();
        u.LicenseScope = string.IsNullOrWhiteSpace(command.Body.LicenseScope) ? null : command.Body.LicenseScope.Trim();
        u.LicenseValidFrom = command.Body.ValidFrom;
        u.LicenseValidUntil = command.Body.ValidUntil;

        var result = await _users.UpdateAsync(u);
        if (!result.Succeeded) throw new ArgumentException(CreateUserHandler.JoinErrors(result));

        return GetUserLicenseHandler.ToDto(u, _clock);
    }
}
