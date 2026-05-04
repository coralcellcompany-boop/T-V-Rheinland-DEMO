namespace TuvInspection.Contracts.Clients;

public sealed record ClientListItemDto(
    Guid Id,
    string Name,
    string Code,
    ContractStatusDto ContractStatus,
    ServiceTypeDto AllowedServices,
    string? ContactName,
    string? ContactEmail,
    DateTime CreatedAtUtc);

public sealed record ClientDetailDto(
    Guid Id,
    string Name,
    string Code,
    string? Address,
    string? ContactName,
    string? ContactPhone,
    string? ContactEmail,
    ContractStatusDto ContractStatus,
    ServiceTypeDto AllowedServices,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record CreateClientRequest(
    string Name,
    string Code,
    string? Address,
    string? ContactName,
    string? ContactPhone,
    string? ContactEmail,
    ContractStatusDto ContractStatus,
    ServiceTypeDto AllowedServices);

public sealed record UpdateClientRequest(
    string Name,
    string? Address,
    string? ContactName,
    string? ContactPhone,
    string? ContactEmail,
    ContractStatusDto ContractStatus,
    ServiceTypeDto AllowedServices);

public enum ContractStatusDto { Active = 0, Suspended = 1, Terminated = 2 }

[Flags]
public enum ServiceTypeDto
{
    None = 0,
    ThirdPartyInspection = 1 << 0,
    BlueSticker = 1 << 1,
    OperatorAssessment = 1 << 2,
    All = ThirdPartyInspection | BlueSticker | OperatorAssessment
}
