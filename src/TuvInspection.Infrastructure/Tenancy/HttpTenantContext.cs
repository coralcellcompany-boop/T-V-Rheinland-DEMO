using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using TuvInspection.Application.Common;

namespace TuvInspection.Infrastructure.Tenancy;

/// <summary>
/// Reads <see cref="ITenantContext"/> from the current ClaimsPrincipal in the HTTP request.
/// Roles, AssignedClientIds and the optional ActiveClientId are populated from JWT claims.
/// </summary>
public sealed class HttpTenantContext : ITenantContext
{
    public HttpTenantContext(IHttpContextAccessor accessor)
    {
        var http = accessor.HttpContext;
        var user = http?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            IsAnonymous = true;
            Roles = new HashSet<string>();
            AssignedClientIds = new HashSet<Guid>();
            return;
        }

        UserId = user.FindFirstValue(ClaimTypes.NameIdentifier)
              ?? user.FindFirstValue("sub");
        UserName = user.FindFirstValue(ClaimTypes.Name) ?? user.Identity.Name;
        Roles = new HashSet<string>(user.FindAll(ClaimTypes.Role).Select(c => c.Value));
        PrimaryRole = Roles.FirstOrDefault();

        var csv = user.FindFirstValue("client_ids");
        AssignedClientIds = new HashSet<Guid>(
            (csv ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
                .Where(g => g != Guid.Empty));

        // Optional client switch sent via header (X-Active-Client).
        if (http != null && http.Request.Headers.TryGetValue("X-Active-Client", out var header)
            && Guid.TryParse(header.ToString(), out var active)
            && AssignedClientIds.Contains(active))
        {
            ActiveClientId = active;
        }

        IpAddress = http?.Connection.RemoteIpAddress?.ToString();
    }

    public bool IsAnonymous { get; }
    public string? UserId { get; }
    public string? UserName { get; }
    public string? PrimaryRole { get; }
    public IReadOnlySet<string> Roles { get; }
    public IReadOnlySet<Guid> AssignedClientIds { get; }
    public Guid? ActiveClientId { get; }
    public string? IpAddress { get; }
    public bool IsInRole(string role) => Roles.Contains(role);
}
