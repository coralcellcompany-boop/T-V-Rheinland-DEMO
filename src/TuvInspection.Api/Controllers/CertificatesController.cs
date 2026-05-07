using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TuvInspection.Application.Certificates;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Contracts.Certificates;
using TuvInspection.Contracts.Common;
using TuvInspection.Domain.Identity;
using TuvInspection.Infrastructure.Certificates;

namespace TuvInspection.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/certificates")]
[Produces("application/json")]
public class CertificatesController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly CertificatePdfRenderer _pdfRenderer;
    private readonly AramcoReportPdfRenderer _aramcoRenderer;

    public CertificatesController(IDispatcher dispatcher,
        CertificatePdfRenderer pdfRenderer,
        AramcoReportPdfRenderer aramcoRenderer)
    {
        _dispatcher = dispatcher;
        _pdfRenderer = pdfRenderer;
        _aramcoRenderer = aramcoRenderer;
    }

    [HttpGet]
    public Task<PagedResult<CertificateListItemDto>> List(
        [FromQuery] Guid? clientId,
        [FromQuery] Guid? equipmentId,
        [FromQuery] Guid? jobOrderId,
        [FromQuery] CertificateStateDto? state,
        [FromQuery] CertificateInspectionTypeDto? inspectionType,
        [FromQuery] InspectionResultDto? result,
        [FromQuery] string? search,
        [FromQuery] bool? aramcoOnly,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default) =>
        _dispatcher.Query(new ListCertificatesQuery(
            clientId, equipmentId, jobOrderId, state, inspectionType, result, search,
            aramcoOnly, page, pageSize), ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CertificateDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var dto = await _dispatcher.Query(new GetCertificateByIdQuery(id), ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.Inspector},{Roles.Manager},{Roles.Coordinator}")]
    public async Task<ActionResult<CertificateDetailDto>> Create(
        [FromBody] CreateCertificateRequest body, CancellationToken ct)
    {
        var dto = await _dispatcher.Send(new CreateCertificateCommand(body), ct);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{Roles.Inspector},{Roles.Manager},{Roles.Coordinator}")]
    public Task<CertificateDetailDto> Update(Guid id, [FromBody] UpdateCertificateRequest body,
        CancellationToken ct) =>
        _dispatcher.Send(new UpdateCertificateCommand(id, body), ct);

    [HttpPost("{id:guid}/transitions/{trigger}")]
    public Task<CertificateDetailDto> Transition(
        Guid id, string trigger,
        [FromBody] TransitionRequest? body, CancellationToken ct)
    {
        if (!Enum.TryParse<CertificateTriggerDto>(trigger, ignoreCase: true, out var t))
            throw new ArgumentException($"Unknown trigger '{trigger}'.");
        return _dispatcher.Send(new FireCertificateTriggerCommand(id, t, body?.Comments), ct);
    }

    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> GetPdf(Guid id, CancellationToken ct)
    {
        var dto = await _dispatcher.Query(new GetCertificateByIdQuery(id), ct);
        if (dto is null) return NotFound();
        var bytes = _pdfRenderer.Render(dto);
        return File(bytes, "application/pdf", $"{dto.CertificateNo}.pdf");
    }

    /// <summary>
    /// Aramco-approved Annex 1 (MS0053813) lifting equipment inspection report PDF —
    /// the official format Saudi Aramco accepts for Blue Sticker certificates.
    /// </summary>
    [HttpGet("{id:guid}/aramco-report")]
    public async Task<IActionResult> GetAramcoReport(Guid id, CancellationToken ct)
    {
        var dto = await _dispatcher.Query(new GetCertificateByIdQuery(id), ct);
        if (dto is null) return NotFound();
        var ctx = await _dispatcher.Query(new GetCertificateInspectorContextQuery(id), ct);
        var bytes = _aramcoRenderer.Render(dto,
            inspectorName: ctx.InspectorName ?? "—",
            inspectorSapNo: ctx.InspectorSapNo,
            equipmentSwl: ctx.EquipmentSwl ?? "—");
        return File(bytes, "application/pdf", $"{dto.CertificateNo}-Annex1.pdf");
    }
}
