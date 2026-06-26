using TuvInspection.Application.Certificates;
using Xunit;

namespace TuvInspection.UnitTests.Certificates;

/// <summary>
/// Guards the Blue Sticker pre-submit gate. Regression: a real production cert with no linked
/// job order stored <c>tuvJobOrderNo: null</c> (the field is auto-filled from the job order),
/// so Submit returned 400 "TUV Job Order No. is required" with no way for the inspector to
/// supply it. The fix lets the inspector type the value when it's blank; these tests pin the
/// contract — every other field being present, the report is valid iff TUV Job Order No. is set.
/// </summary>
public class AramcoReportValidatorTests
{
    // Shape mirrors the real stored AramcoReportJson; {0} is the tuvJobOrderNo value (raw JSON).
    private const string JsonTemplate =
        "{{\"tuvJobOrderNo\":{0},\"aramcoCategoryNo\":\"CR01\",\"orgCode\":\"code 1\"," +
        "\"rpoNo\":\"number 1\",\"crmNo\":\"crm 1\",\"reportNo\":\"IS-NA-2026-003\"," +
        "\"departmentContractor\":\"Dept\",\"inspectionTime\":\"03:18\"," +
        "\"areaOfInspection\":\"Area\",\"capacity\":\"5 t\",\"manufacturer\":\"Acme\"," +
        "\"model\":\"M1\",\"equipmentType\":\"Crawler Crane\",\"equipmentSerialNo\":\"SN-1\"," +
        "\"receiverName\":\"hamouda\",\"receiverBadgeNo\":\"one\",\"deficiencyItems\":[]}}";

    private static string Json(string tuvJobOrderNoRaw) => string.Format(JsonTemplate, tuvJobOrderNoRaw);

    [Fact]
    public void Missing_tuv_job_order_no_fails_with_the_field_message()
    {
        var result = new AramcoReportValidator().ValidateJson(Json("null"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "TUV Job Order No. is required.");
    }

    [Fact]
    public void Complete_report_with_tuv_job_order_no_is_valid()
    {
        var result = new AramcoReportValidator().ValidateJson(Json("\"JO-2026-001\""));

        Assert.True(result.IsValid,
            "every required Aramco field is present, so the report must pass once TUV Job Order No. is set: "
            + string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public void Empty_payload_fails()
        => Assert.False(new AramcoReportValidator().ValidateJson(null).IsValid);
}
