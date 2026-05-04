using TuvInspection.Contracts.Clients;
using TuvInspection.Domain.Clients;

namespace TuvInspection.Infrastructure.Clients;

internal static class ClientMapper
{
    public static ClientListItemDto ToListItem(this Client c) =>
        new(c.Id, c.Name, c.Code, (ContractStatusDto)c.ContractStatus,
            (ServiceTypeDto)(int)c.AllowedServices, c.ContactName, c.ContactEmail, c.CreatedAtUtc);

    public static ClientDetailDto ToDetail(this Client c) =>
        new(c.Id, c.Name, c.Code, c.Address, c.ContactName, c.ContactPhone, c.ContactEmail,
            (ContractStatusDto)c.ContractStatus, (ServiceTypeDto)(int)c.AllowedServices,
            c.CreatedAtUtc, c.UpdatedAtUtc);
}
