using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using TuvInspection.Contracts.Certificates;

namespace TuvInspection.Application.Certificates;

/// <summary>
/// Pre-submit gate for Blue Sticker certificates. Inspectors must fill every Aramco-mandated
/// Annex 1 field before the cert can leave Draft — otherwise SAIC compliance fails downstream
/// and the report renders with blank cells. Run by <c>FireCertificateTriggerHandler</c>
/// when the trigger is <c>Submit</c> and the cert's equipment carries an Aramco category.
/// </summary>
public sealed class AramcoReportValidator : AbstractValidator<AramcoReportData>
{
    public AramcoReportValidator()
    {
        RuleFor(x => x.TuvJobOrderNo).NotEmpty().WithMessage("TUV Job Order No. is required.");
        RuleFor(x => x.AramcoCategoryNo).NotEmpty().WithMessage("Aramco Category No. is required.");
        RuleFor(x => x.OrgCode).NotEmpty().WithMessage("Org Code is required.");
        RuleFor(x => x.RpoNo).NotEmpty().WithMessage("RPO No. is required.");
        RuleFor(x => x.CrmNo).NotEmpty().WithMessage("CRM No. is required.");
        RuleFor(x => x.DepartmentContractor).NotEmpty().WithMessage("Department / Contractor is required.");
        RuleFor(x => x.Capacity).NotEmpty().WithMessage("Equipment capacity is required.");
        RuleFor(x => x.Manufacturer).NotEmpty().WithMessage("Manufacturer is required.");
        RuleFor(x => x.EquipmentSerialNo).NotEmpty().WithMessage("Equipment serial No. is required.");
        RuleFor(x => x.ReceiverName).NotEmpty().WithMessage("Receiver name is required.");
        RuleFor(x => x.ReceiverBadgeNo).NotEmpty().WithMessage("Receiver badge No. is required.");
        RuleFor(x => x.AreaOfInspection).NotEmpty().WithMessage("Area of inspection is required.");
    }

    /// <summary>
    /// Convenience helper for handlers: deserialises the JSON blob, validates, and returns the
    /// flattened result. A null/blank payload is treated as "no fields filled" and fails with a
    /// single composite error so the UI can highlight the section.
    /// </summary>
    public ValidationResult ValidateJson(string? aramcoReportJson)
    {
        if (string.IsNullOrWhiteSpace(aramcoReportJson))
            return new ValidationResult(new[]
            {
                new ValidationFailure("AramcoReportJson",
                    "Annex 1 report is empty — fill the Aramco section before submitting."),
            });

        AramcoReportData? data;
        try
        {
            data = JsonSerializer.Deserialize<AramcoReportData>(aramcoReportJson, JsonOpts);
        }
        catch (JsonException ex)
        {
            return new ValidationResult(new[]
            {
                new ValidationFailure("AramcoReportJson", $"Annex 1 report is not valid JSON: {ex.Message}"),
            });
        }

        if (data is null)
            return new ValidationResult(new[]
            {
                new ValidationFailure("AramcoReportJson", "Annex 1 report deserialised to null."),
            });

        return Validate(data);
    }

    // Same fault-tolerant options as the PDF renderer: a non-parseable date/time must not
    // make the whole payload deserialise as empty (which would mask the real field errors).
    private static readonly JsonSerializerOptions JsonOpts = AramcoJson.Options;
}
