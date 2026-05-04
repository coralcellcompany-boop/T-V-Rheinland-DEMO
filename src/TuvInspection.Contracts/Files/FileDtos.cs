namespace TuvInspection.Contracts.Files;

public sealed record UploadedFileDto(string Key, string FileName, string ContentType, long Size);

public sealed record AttachedPhotoDto(string Key, string FileName, string ContentType);
