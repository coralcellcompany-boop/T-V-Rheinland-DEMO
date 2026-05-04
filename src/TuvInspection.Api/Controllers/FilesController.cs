using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TuvInspection.Application.Common.Documents;
using TuvInspection.Contracts.Files;

namespace TuvInspection.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/files")]
public class FilesController : ControllerBase
{
    private readonly IDocumentStore _store;
    public FilesController(IDocumentStore store) => _store = store;

    /// <summary>
    /// Generic upload — returns the storage key the caller should attach to the parent
    /// entity (Equipment.PhotoKey, Certificate.PhotosJson list, etc.).
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<UploadedFileDto>> Upload(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest("No file uploaded.");
        var allowed = new[] { "image/png", "image/jpeg", "image/webp", "application/pdf" };
        if (!allowed.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            return BadRequest($"Unsupported content type {file.ContentType}.");

        await using var stream = file.OpenReadStream();
        var key = await _store.Save(stream, file.FileName, file.ContentType, ct);
        return Ok(new UploadedFileDto(key, file.FileName, file.ContentType, file.Length));
    }

    /// <summary>Stream the stored file. Auth-required so an attacker can't enumerate keys.</summary>
    [HttpGet("{key}")]
    public async Task<IActionResult> Get(string key, CancellationToken ct)
    {
        var result = await _store.Read(key, ct);
        if (result is null) return NotFound();
        Response.Headers.CacheControl = "private, max-age=600";
        return File(result.Content, result.ContentType, result.FileName);
    }
}
