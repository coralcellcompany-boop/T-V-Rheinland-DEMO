using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Contracts.Users;

namespace TuvInspection.Application.Users;

public sealed record ListUsersQuery(string? Search) : IQuery<IReadOnlyList<UserListItemDto>>;

public sealed record ListInspectorsQuery() : IQuery<IReadOnlyList<InspectorLookupDto>>;

public sealed record GetUserByIdQuery(string Id) : IQuery<UserListItemDto?>;

public sealed record CreateUserCommand(CreateUserRequest Body) : ICommand<UserListItemDto>;

public sealed record UpdateUserCommand(string Id, UpdateUserRequest Body) : ICommand<UserListItemDto>;

public sealed record ResetUserPasswordCommand(string Id, ResetPasswordRequest Body) : ICommand<Unit>;

public sealed record GetUserLicenseQuery(string Id) : IQuery<UserLicenseDto?>;

public sealed record UpdateUserLicenseCommand(string Id, UpdateUserLicenseRequest Body) : ICommand<UserLicenseDto>;
