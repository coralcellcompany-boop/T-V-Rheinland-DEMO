namespace TuvInspection.Infrastructure.Identity;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "tuv-arabia";
    public string Audience { get; set; } = "tuv-arabia-spa";
    public string SigningKey { get; set; } = default!;          // injected from config / secrets
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 14;
}
