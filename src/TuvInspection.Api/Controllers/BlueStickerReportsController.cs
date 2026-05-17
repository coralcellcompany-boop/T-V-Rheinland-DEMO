using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TuvInspection.Application.BlueSticker;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Contracts.BlueSticker;
using TuvInspection.Contracts.Common;
using TuvInspection.Domain.Identity;
using TuvInspection.Infrastructure.BlueSticker;

namespace TuvInspection.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/blue-sticker-reports")]
[Produces("application/json")]
public class BlueStickerReportsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly BlueStickerReportPdfRenderer _pdf;

    public BlueStickerReportsController(IDispatcher dispatcher, BlueStickerReportPdfRenderer pdf)
    { _dispatcher = dispatcher; _pdf = pdf; }

    [HttpGet]
    public Task<PagedResult<BlueStickerReportListItemDto>> List(
        [FromQuery] Guid? jobOrderId, [FromQuery] BlueStickerReportStateDto? state,
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        CancellationToken ct = default) =>
        _dispatcher.Query(new ListBlueStickerReportsQuery(jobOrderId, state, search, page, pageSize), ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BlueStickerReportDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var dto = await _dispatcher.Query(new GetBlueStickerReportByIdQuery(id), ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.Coordinator},{Roles.Manager}")]
    public Task<IReadOnlyList<BlueStickerReportDetailDto>> Create(
        [FromBody] CreateBlueStickerReportsRequest body, CancellationToken ct) =>
        _dispatcher.Send(new CreateBlueStickerReportsCommand(body), ct);

    [HttpPut("{id:guid}/inspection")]
    [Authorize(Roles = $"{Roles.Inspector},{Roles.Manager}")]
    public Task<BlueStickerReportDetailDto> UpdateInspection(
        Guid id, [FromBody] UpdateBlueStickerInspectionRequest body, CancellationToken ct) =>
        _dispatcher.Send(new UpdateBlueStickerInspectionCommand(id, body), ct);

    [HttpPost("{id:guid}/transitions/{trigger}")]
    public Task<BlueStickerReportDetailDto> Transition(
        Guid id, string trigger, [FromBody] BlueStickerTransitionRequest? body, CancellationToken ct)
    {
        if (!Enum.TryParse<BlueStickerTriggerDto>(trigger, ignoreCase: true, out var t))
            throw new ArgumentException($"Unknown trigger '{trigger}'.");
        return _dispatcher.Send(new FireBlueStickerTriggerCommand(id, t, body), ct);
    }

    [HttpPost("{id:guid}/request-otp")]
    [Authorize(Roles = $"{Roles.Inspector},{Roles.Manager}")]
    public Task<BlueStickerReportDetailDto> RequestOtp(Guid id, CancellationToken ct) =>
        _dispatcher.Send(new RequestClientOtpCommand(id), ct);

    [HttpPost("{id:guid}/verify-and-sign")]
    [Authorize(Roles = $"{Roles.Inspector},{Roles.Manager}")]
    public Task<BlueStickerReportDetailDto> VerifyAndSign(
        Guid id, [FromBody] VerifyOtpAndSignRequest body, CancellationToken ct) =>
        _dispatcher.Send(new VerifyOtpAndSignCommand(id, body), ct);

    [HttpGet("{id:guid}/report.pdf")]
    public async Task<IActionResult> GetPdf(Guid id, CancellationToken ct)
    {
        var dto = await _dispatcher.Query(new GetBlueStickerReportByIdQuery(id), ct);
        if (dto is null) return NotFound();
        var bytes = await _pdf.RenderAsync(dto, ct);
        return File(bytes, "application/pdf", $"{dto.ReportNo}-Annex1.pdf");
    }
}
