namespace TuvInspection.Contracts.Reports;

public sealed record MonthlyStatsRowDto(
    string Period,                      // "2026-05"
    int TotalCertificates,
    int Approved,
    int Rejected,
    int InProgress);

public sealed record InspectorProductivityRowDto(
    string InspectorId,
    string InspectorName,
    int CertificatesCreated,
    int CertificatesApproved,
    int DwrEntries,
    double TotalHours);

public sealed record DueSoonRowDto(
    string CertificateNo,
    Guid ClientId,
    string ClientName,
    string EquipmentIdNo,
    string EquipmentTypeName,
    DateOnly NextDueDate,
    int DaysUntilDue);

public sealed record OverdueRowDto(
    string CertificateNo,
    Guid ClientId,
    string ClientName,
    string EquipmentIdNo,
    DateOnly NextDueDate,
    int DaysOverdue);
