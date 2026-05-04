namespace TuvInspection.Domain.Clients;

[Flags]
public enum ServiceType
{
    None = 0,
    ThirdPartyInspection = 1 << 0,
    BlueSticker = 1 << 1,
    OperatorAssessment = 1 << 2,
    All = ThirdPartyInspection | BlueSticker | OperatorAssessment
}

public enum ContractStatus
{
    Active = 0,
    Suspended = 1,
    Terminated = 2
}
