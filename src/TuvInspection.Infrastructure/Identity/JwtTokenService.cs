using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TuvInspection.Application.Common.Time;
using TuvInspection.Infrastructure.Persistence;

namespace TuvInspection.Infrastructure.Identity;

public sealed record TokenPair(string AccessToken, DateTime AccessExpiresAtUtc, string RefreshToken, DateTime RefreshExpiresAtUtc);

public interface IJwtTokenService
{
    Task<TokenPair> Issue(ApplicationUser user, string? ip, CancellationToken ct);
    Task<TokenPair?> Refresh(string refreshToken, string? ip, CancellationToken ct);
    Task RevokeAllForUser(string userId, CancellationToken ct);
}

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly AppDbContext _db;
    private readonly JwtOptions _opt;
    private readonly IClock _clock;

    public JwtTokenService(
        UserManager<ApplicationUser> users,
        AppDbContext db,
        IOptions<JwtOptions> opt,
        IClock clock)
    {
        _users = users;
        _db = db;
        _opt = opt.Value;
        _clock = clock;
    }

    public async Task<TokenPair> Issue(ApplicationUser user, string? ip, CancellationToken ct)
    {
        var roles = await _users.GetRolesAsync(user);
        var now = _clock.UtcNow;
        var accessExpires = now.AddMinutes(_opt.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? user.Email ?? user.Id)
        };
        if (!string.IsNullOrEmpty(user.Email)) claims.Add(new Claim(ClaimTypes.Email, user.Email));
        if (!string.IsNullOrEmpty(user.AssignedClientIdsCsv))
            claims.Add(new Claim("client_ids", user.AssignedClientIdsCsv));
        foreach (var r in roles) claims.Add(new Claim(ClaimTypes.Role, r));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            notBefore: now,
            expires: accessExpires,
            signingCredentials: creds);

        var access = new JwtSecurityTokenHandler().WriteToken(token);

        // Refresh token: high-entropy random; we store only the hash.
        var refreshRaw = GenerateRefreshTokenString();
        var refreshExpires = now.AddDays(_opt.RefreshTokenDays);
        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashToken(refreshRaw),
            IssuedAtUtc = now,
            ExpiresAtUtc = refreshExpires,
            CreatedFromIp = ip
        });
        await _db.SaveChangesAsync(ct);

        return new TokenPair(access, accessExpires, refreshRaw, refreshExpires);
    }

    public async Task<TokenPair?> Refresh(string refreshToken, string? ip, CancellationToken ct)
    {
        var hash = HashToken(refreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null || stored.RevokedAtUtc is not null || stored.ExpiresAtUtc < _clock.UtcNow)
            return null;

        var user = await _users.FindByIdAsync(stored.UserId);
        if (user is null || !user.IsActive) return null;

        // Rotate: revoke old, issue new (linked).
        var newPair = await Issue(user, ip, ct);
        stored.RevokedAtUtc = _clock.UtcNow;
        stored.ReplacedByTokenHash = HashToken(newPair.RefreshToken);
        await _db.SaveChangesAsync(ct);

        return newPair;
    }

    public async Task RevokeAllForUser(string userId, CancellationToken ct)
    {
        var tokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ToListAsync(ct);
        foreach (var t in tokens) t.RevokedAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private static string GenerateRefreshTokenString()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        return Convert.ToBase64String(bytes);
    }

    private static string HashToken(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }
}
