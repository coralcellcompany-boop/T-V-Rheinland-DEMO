using System.Text.Json;
using Microsoft.Extensions.Options;
using TuvInspection.Application.Common.Documents;

namespace TuvInspection.Infrastructure.Storage;

public sealed class LocalDocumentStoreOptions
{
    public string RootPath { get; set; } = "./.data/documents";
}

public sealed class LocalDocumentStore : IDocumentStore
{
    private readonly string _root;

    public LocalDocumentStore(IOptions<LocalDocumentStoreOptions> options)
    {
        _root = Path.GetFullPath(options.Value.RootPath);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> Save(Stream content, string fileName, string contentType, CancellationToken ct)
    {
        var key = $"{Guid.NewGuid():N}{Path.GetExtension(fileName)}";
        var fullPath = Path.Combine(_root, key);
        await using var fs = File.Create(fullPath);
        await content.CopyToAsync(fs, ct);

        var meta = new { FileName = fileName, ContentType = contentType };
        await File.WriteAllTextAsync(fullPath + ".meta.json", JsonSerializer.Serialize(meta), ct);
        return key;
    }

    public async Task<DocumentReadResult?> Read(string key, CancellationToken ct)
    {
        var fullPath = Path.Combine(_root, key);
        if (!File.Exists(fullPath)) return null;

        string fileName = key, contentType = "application/octet-stream";
        var metaPath = fullPath + ".meta.json";
        if (File.Exists(metaPath))
        {
            var doc = JsonDocument.Parse(await File.ReadAllTextAsync(metaPath, ct));
            fileName = doc.RootElement.GetProperty("FileName").GetString() ?? key;
            contentType = doc.RootElement.GetProperty("ContentType").GetString() ?? contentType;
        }

        return new DocumentReadResult(File.OpenRead(fullPath), contentType, fileName);
    }

    public Task Delete(string key, CancellationToken ct)
    {
        var fullPath = Path.Combine(_root, key);
        if (File.Exists(fullPath)) File.Delete(fullPath);
        var metaPath = fullPath + ".meta.json";
        if (File.Exists(metaPath)) File.Delete(metaPath);
        return Task.CompletedTask;
    }
}
