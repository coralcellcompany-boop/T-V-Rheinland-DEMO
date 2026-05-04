namespace TuvInspection.Domain.Identity;

/// <summary>
/// Canonical role names. Mapped onto ASP.NET Identity roles in Infrastructure.
/// Permissions matrix in plan §7.
/// </summary>
public static class Roles
{
    public const string Manager = "Manager";
    public const string Coordinator = "Coordinator";
    public const string Inspector = "Inspector";
    public const string TechReviewer = "TechReviewer";
    public const string ClientUser = "ClientUser";

    public static readonly IReadOnlyList<string> All =
        new[] { Manager, Coordinator, Inspector, TechReviewer, ClientUser };
}
