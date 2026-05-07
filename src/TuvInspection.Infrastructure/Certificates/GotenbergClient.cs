using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;

namespace TuvInspection.Infrastructure.Certificates;

/// <summary>
/// Thin client around the gotenberg LibreOffice REST endpoint. Converts a docx byte
/// array into a PDF byte array by POSTing it as multipart form data to
/// /forms/libreoffice/convert. Configurable via <c>Gotenberg:Url</c> in
/// appsettings.json (default <c>http://localhost:3030</c>).
/// </summary>
public sealed class GotenbergClient
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public GotenbergClient(HttpClient http, IConfiguration config)
    {
        _http = http;
        _baseUrl = (config["Gotenberg:Url"] ?? "http://localhost:3030").TrimEnd('/');
    }

    public async Task<byte[]> ConvertDocxToPdfAsync(
        byte[] docxBytes, string fileName, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(docxBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        // Gotenberg keys file form fields by `files` and uses the upload filename as
        // the input identifier, so the extension matters.
        form.Add(fileContent, "files", fileName);

        var url = $"{_baseUrl}/forms/libreoffice/convert";
        using var resp = await _http.PostAsync(url, form, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Gotenberg conversion failed ({(int)resp.StatusCode}): {body}");
        }
        return await resp.Content.ReadAsByteArrayAsync(ct);
    }
}
