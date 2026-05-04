using TuvInspection.Domain.Common;

namespace TuvInspection.Domain.Equipment;

public class Equipment : AggregateRoot<Guid>, IAuditable, ITenantScoped
{
    public Guid ClientId { get; private set; }
    public Guid EquipmentTypeId { get; private set; }
    public string IdNo { get; private set; } = default!;
    public string? SerialNo { get; private set; }
    public string? Manufacturer { get; private set; }
    public string? Model { get; private set; }
    public int? YearOfManufacture { get; private set; }
    public string? Swl { get; private set; }
    public AramcoCategory? AramcoCategory { get; private set; }
    public string? Location { get; private set; }
    public string? PhotoKey { get; private set; }
    public EquipmentStatus Status { get; private set; } = EquipmentStatus.Active;

    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedById { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedById { get; set; }

    private Equipment() { }

    public Equipment(
        Guid id,
        Guid clientId,
        Guid equipmentTypeId,
        string idNo,
        AramcoCategory? aramcoCategory) : base(id)
    {
        if (clientId == Guid.Empty) throw new ArgumentException("ClientId required", nameof(clientId));
        if (equipmentTypeId == Guid.Empty) throw new ArgumentException("EquipmentTypeId required", nameof(equipmentTypeId));
        if (string.IsNullOrWhiteSpace(idNo)) throw new ArgumentException("IdNo required", nameof(idNo));

        ClientId = clientId;
        EquipmentTypeId = equipmentTypeId;
        IdNo = idNo.Trim();
        AramcoCategory = aramcoCategory;
    }

    public void UpdateIdentification(string idNo, string? serialNo)
    {
        if (string.IsNullOrWhiteSpace(idNo)) throw new ArgumentException("IdNo required", nameof(idNo));
        IdNo = idNo.Trim();
        SerialNo = serialNo?.Trim();
    }

    public void UpdateSpec(string? manufacturer, string? model, int? year, string? swl)
    {
        Manufacturer = manufacturer?.Trim();
        Model = model?.Trim();
        YearOfManufacture = year;
        Swl = swl?.Trim();
    }

    public void UpdateLocation(string? location) => Location = location?.Trim();
    public void SetPhoto(string? photoKey) => PhotoKey = photoKey;

    public void Decommission() => Status = EquipmentStatus.Decommissioned;
    public void MarkSold() => Status = EquipmentStatus.Sold;
    public void Reactivate() => Status = EquipmentStatus.Active;
}
