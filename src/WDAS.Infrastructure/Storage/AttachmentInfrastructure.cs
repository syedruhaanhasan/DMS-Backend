using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WDAS.Application.Abstractions;
using WDAS.Application.Options;
using WDAS.Domain.Enums;

namespace WDAS.Infrastructure.Storage;

public class LocalFileStorage : IFileStorage
{
    private readonly string _root;
    private readonly IHostEnvironment _environment;

    public LocalFileStorage(IHostEnvironment environment, IOptions<AttachmentOptions> options)
    {
        _environment = environment;
        _root = Path.IsPathRooted(options.Value.StorageRoot)
            ? options.Value.StorageRoot
            : Path.Combine(environment.ContentRootPath, options.Value.StorageRoot);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(Stream content, string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);

        await using var file = File.Create(fullPath);
        await content.CopyToAsync(file, cancellationToken);
        return relativePath;
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(_root, storageKey.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Stored file not found.", fullPath);
        }

        Stream stream = File.OpenRead(fullPath);
        return Task.FromResult(stream);
    }

    public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(_root, storageKey.Replace('/', Path.DirectorySeparatorChar));
        return Task.FromResult(File.Exists(fullPath));
    }
}

public class DevAttachmentScanner : IAttachmentScanner
{
    public Task<AttachmentScanStatus> ScanAsync(Stream content, string fileName, CancellationToken cancellationToken = default) =>
        Task.FromResult(AttachmentScanStatus.Clean);
}

public class ClamAvAttachmentScanner : IAttachmentScanner
{
    private readonly AttachmentOptions _options;
    private readonly ILogger<ClamAvAttachmentScanner> _logger;

    public ClamAvAttachmentScanner(IOptions<AttachmentOptions> options, ILogger<ClamAvAttachmentScanner> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AttachmentScanStatus> ScanAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            await client.ConnectAsync(_options.ClamAvHost, _options.ClamAvPort, cancellationToken);
            await using var stream = client.GetStream();

            var command = System.Text.Encoding.ASCII.GetBytes("zINSTREAM\0");
            await stream.WriteAsync(command, cancellationToken);

            var buffer = new byte[2048];
            int read;
            while ((read = await content.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
            {
                var sizeBytes = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(read));
                await stream.WriteAsync(sizeBytes, cancellationToken);
                await stream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            await stream.WriteAsync(BitConverter.GetBytes(0), cancellationToken);

            using var reader = new StreamReader(stream);
            var response = await reader.ReadLineAsync(cancellationToken) ?? string.Empty;

            if (response.Contains("FOUND", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("ClamAV detected threat in {FileName}: {Response}", fileName, response);
                return AttachmentScanStatus.Quarantined;
            }

            if (response.Contains("OK", StringComparison.OrdinalIgnoreCase))
            {
                return AttachmentScanStatus.Clean;
            }

            _logger.LogWarning("Unexpected ClamAV response for {FileName}: {Response}", fileName, response);
            return AttachmentScanStatus.Pending;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ClamAV scan failed for {FileName}", fileName);
            return AttachmentScanStatus.Pending;
        }
    }
}

public class DevAttachmentPreviewGenerator : IAttachmentPreviewGenerator
{
    public Task<AttachmentPreviewResult?> GenerateAsync(Stream content, string contentType, string fileName, CancellationToken cancellationToken = default)
    {
        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
            contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<AttachmentPreviewResult?>(null);
        }

        var previewKey = $"previews/{Guid.NewGuid():N}.html";
        var html = $"<html><body><p>Preview placeholder for {fileName}. Convert Office documents in production.</p></body></html>";
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(html));
        return Task.FromResult<AttachmentPreviewResult?>(new AttachmentPreviewResult(previewKey, "text/html", stream));
    }
}
