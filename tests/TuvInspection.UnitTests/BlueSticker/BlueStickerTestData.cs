using TuvInspection.Contracts.BlueSticker;

namespace TuvInspection.UnitTests.BlueSticker;

/// <summary>Shared minimal <see cref="BlueStickerReportDetailDto"/> for renderer/filler tests.
/// Only the fields the PDF reads are meaningful; everything else is null/default.</summary>
internal static class BlueStickerTestData
{
    public static BlueStickerReportDetailDto SampleReport(string? checklistNumber = "SAIC-U-7007") => new(
        Id: Guid.Empty,
        ReportNo: "IS-NA-2026-003",
        JobOrderId: Guid.Empty,
        EquipmentId: Guid.Empty,
        ClientId: Guid.Empty,
        TuvJobOrderNo: "JO-1",
        AramcoCategoryNo: "CR01",
        OrgCode: null, RpoNo: null, CrmNo: null, DepartmentContractor: null,
        InspectionDate: null, InspectionTime: null,
        PreviousStickerNo: null, PreviousStickerIssuedBy: null,
        AreaOfInspection: null,
        Result: BlueStickerResultDto.Pass,
        EquipmentIdNo: "DEV-YANBU-EQ-001",
        Capacity: null, EquipmentLocation: null,
        Manufacturer: null, Model: null,
        EquipmentType: "Mobile Crane (Telescopic Boom)",
        EquipmentSerialNo: null, NewStickerNo: null,
        StickerExpirationDate: null,
        Deficiencies: null, CorrectiveActionsTaken: null,
        ReceiverName: null, ReceiverBadgeNo: null, ReceiverTelephone: null,
        InspectorName: null, InspectorSapNo: null, InspectorTelephone: null,
        TechnicalReviewerName: null,
        ReceivedDate: null, ReviewedDate: null,
        ReceiverSignaturePng: null, InspectorSignaturePng: null, TechnicalReviewerSignaturePng: null,
        State: BlueStickerReportStateDto.ClientSigned,
        CreatedAtUtc: default,
        UpdatedAtUtc: null,
        Transitions: Array.Empty<BlueStickerTransitionDto>(),
        InspectionChecklistNumber: checklistNumber);
}
