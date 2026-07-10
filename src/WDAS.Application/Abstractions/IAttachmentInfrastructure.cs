using WDAS.Domain.Enums;

namespace WDAS.Application.Abstractions;

public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, string relativePath, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default);
}

public interface IAttachmentScanner
{
    Task<AttachmentScanStatus> ScanAsync(Stream content, string fileName, CancellationToken cancellationToken = default);
}

public interface IAttachmentPreviewGenerator
{
    Task<AttachmentPreviewResult?> GenerateAsync(Stream content, string contentType, string fileName, CancellationToken cancellationToken = default);
}

public record AttachmentPreviewResult(string StorageKey, string ContentType, Stream Content);
