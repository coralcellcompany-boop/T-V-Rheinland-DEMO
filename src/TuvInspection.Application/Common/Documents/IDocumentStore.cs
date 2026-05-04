namespace TuvInspection.Application.Common.Documents;

/// <summary>
/// Storage abstraction for certificate PDFs, equipment photos, sticker images. Implementations:
/// <c>LocalDocumentStore</c> (Infrastructure, dev), future S3 / Azure Blob.
/// </summary>
public interface IDocumentStore
{
    Task<string> Save(Stream content, string fileName, string contentType, CancellationToken ct);
    Task<DocumentReadResult?> Read(string key, CancellationToken ct);
    Task Delete(string key, CancellationToken ct);
}

public sealed record DocumentReadResult(Stream Content, string ContentType, string FileName);
