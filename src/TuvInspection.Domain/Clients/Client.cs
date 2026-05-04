using TuvInspection.Domain.Common;

namespace TuvInspection.Domain.Clients;

public class Client : AggregateRoot<Guid>, IAuditable
{
    public string Name { get; private set; } = default!;
    public string Code { get; private set; } = default!;
    public string? Address { get; private set; }
    public string? ContactName { get; private set; }
    public string? ContactPhone { get; private set; }
    public string? ContactEmail { get; private set; }
    public ContractStatus ContractStatus { get; private set; } = ContractStatus.Active;
    public ServiceType AllowedServices { get; private set; } = ServiceType.All;

    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedById { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedById { get; set; }

    private Client() { }

    public Client(Guid id, string name, string code) : base(id)
    {
        Rename(name);
        SetCode(code);
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Client name is required", nameof(name));
        Name = name.Trim();
    }

    public void SetCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Client code is required", nameof(code));
        Code = code.Trim().ToUpperInvariant();
    }

    public void UpdateContact(string? name, string? phone, string? email)
    {
        ContactName = name?.Trim();
        ContactPhone = phone?.Trim();
        ContactEmail = email?.Trim();
    }

    public void UpdateAddress(string? address) => Address = address?.Trim();

    public void SetContractStatus(ContractStatus status) => ContractStatus = status;

    public void SetAllowedServices(ServiceType services) => AllowedServices = services;
}
