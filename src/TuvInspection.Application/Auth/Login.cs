using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Contracts.Auth;

namespace TuvInspection.Application.Auth;

public sealed record LoginCommand(string UserName, string Password, string? Ip)
    : ICommand<LoginResult>;

public sealed record LoginResult(bool Success, LoginResponse? Response, string? Error);

public sealed record RefreshCommand(string RefreshToken, string? Ip) : ICommand<LoginResult>;

public sealed record GetCurrentUserQuery() : IQuery<UserProfile?>;
