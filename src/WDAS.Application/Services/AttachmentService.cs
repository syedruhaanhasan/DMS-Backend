using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WDAS.Application.Abstractions;
using WDAS.Application.Models;
using WDAS.Application.Options;
using WDAS.Domain.Entities;
using WDAS.Domain.Enums;
using WDAS.Domain.Exceptions;

namespace WDAS.Application.Services;

public class AttachmentService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IFileStorage _fileStorage;
    private readonly IAttachmentScanner _scanner;
    private readonly IAttachmentPreviewGenerator _previewGenerator;
    private readonly AttachmentOptions _options;

    public AttachmentService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IClock clock,
        IFileStorage fileStorage,
        IAttachmentScanner scanner,
        IAttachmentPreviewGenerator previewGenerator,
        IOptions<AttachmentOptions> options)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _fileStorage = fileStorage;
        _scanner = scanner;
        _previewGenerator = previewGenerator;
        _options = options.Value;
    }

    public async Task<AttachmentDto> UploadAsync(
        Guid documentId,
        Stream fileStream,
        string fileName,
        string contentType,
        string? logicalName,
        Guid? workflowStepActionId,
        CancellationToken cancellationToken = default)
    {
        var document = await LoadDocumentAsync(documentId, cancellationToken);
        EnsureCanUpload(document, workflowStepActionId);

        ValidateFile(fileName, fileStream.Length);

        var now = _clock.UtcNow;
        var extension = Path.GetExtension(fileName);
        var storageKey = $"{documentId:N}/{Guid.NewGuid():N}{extension}";

        await using var scanStream = new MemoryStream();
        await fileStream.CopyToAsync(scanStream, cancellationToken);
        scanStream.Position = 0;

        var scanStatus = await _scanner.ScanAsync(scanStream, fileName, cancellationToken);
        if (scanStatus == AttachmentScanStatus.Quarantined)
        {
            throw new DomainException("File failed malware scanning and was quarantined.");
        }

        if (scanStatus == AttachmentScanStatus.Pending &&
            _options.VirusScanEnabled &&
            _options.FailUploadWhenScannerUnavailable)
        {
            throw new DomainException("Virus scanner is unavailable or returned an invalid result. Upload blocked.");
        }

        if (scanStatus == AttachmentScanStatus.Pending && !_options.VirusScanEnabled)
        {
            scanStatus = AttachmentScanStatus.Clean;
        }

        scanStream.Position = 0;
        await _fileStorage.SaveAsync(scanStream, storageKey, cancellationToken);

        string? previewKey = null;
        string? previewContentType = null;
        scanStream.Position = 0;
        var preview = await _previewGenerator.GenerateAsync(scanStream, contentType, fileName, cancellationToken);
        if (preview is not null)
        {
            previewKey = preview.StorageKey;
            previewContentType = preview.ContentType;
            await _fileStorage.SaveAsync(preview.Content, preview.StorageKey, cancellationToken);
            await preview.Content.DisposeAsync();
        }

        if (!string.IsNullOrWhiteSpace(logicalName) && document.Status is DocumentStatus.Draft or DocumentStatus.ReturnedForCorrection)
        {
            var existing = await _db.Attachments
                .Where(a => a.DocumentId == documentId && a.LogicalName == logicalName && a.IsLatest)
                .ToListAsync(cancellationToken);

            foreach (var prior in existing)
            {
                prior.IsLatest = false;
                prior.SupersededAtUtc = now;
                prior.UpdatedAtUtc = now;
            }
        }

        var versionNumber = 1;
        if (!string.IsNullOrWhiteSpace(logicalName))
        {
            versionNumber = await _db.Attachments
                .Where(a => a.DocumentId == documentId && a.LogicalName == logicalName)
                .Select(a => (int?)a.VersionNumber)
                .MaxAsync(cancellationToken) ?? 0;
            versionNumber += 1;
        }

        var attachment = new Attachment
        {
            DocumentId = documentId,
            UploadedByUserId = _currentUser.UserId,
            WorkflowStepActionId = workflowStepActionId,
            FileName = fileName,
            LogicalName = logicalName,
            ContentType = contentType,
            StorageKey = storageKey,
            FileSizeBytes = scanStream.Length,
            VersionNumber = versionNumber,
            IsLatest = true,
            ScanStatus = scanStatus,
            PreviewStorageKey = previewKey,
            PreviewContentType = previewContentType,
            DownloadRestricted = _currentUser.IsInRole(RoleNames.Auditor),
            CreatedAtUtc = now
        };

        _db.Add(attachment);
        await SaveAsync(cancellationToken);

        return MapAttachment(attachment);
    }

    public async Task<(Stream Content, string ContentType, string FileName)> GetPreviewAsync(
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        var attachment = await _db.Attachments
            .Include(a => a.Document)
                .ThenInclude(d => d.WorkflowSteps)
                    .ThenInclude(s => s.Actions)
            .FirstOrDefaultAsync(a => a.Id == attachmentId, cancellationToken)
            ?? throw new DomainException("Attachment not found.");

        EnsureCanView(attachment.Document);
        EnsureAttachmentAccessible(attachment);

        var key = attachment.PreviewStorageKey ?? attachment.StorageKey;
        var contentType = attachment.PreviewContentType ?? attachment.ContentType;
        var stream = await _fileStorage.OpenReadAsync(key, cancellationToken);
        return (stream, contentType, attachment.FileName);
    }

    public async Task<IReadOnlyList<AttachmentDto>> ListForDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await LoadDocumentAsync(documentId, cancellationToken);
        EnsureCanView(document);

        var attachments = await _db.Attachments
            .Where(a => a.DocumentId == documentId && a.IsLatest)
            .OrderBy(a => a.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return attachments.Select(MapAttachment).ToList();
    }

    private void ValidateFile(string fileName, long size)
    {
        if (size <= 0 || size > _options.MaxFileSizeBytes)
        {
            throw new DomainException($"File size must be between 1 byte and {_options.MaxFileSizeBytes} bytes.");
        }

        var extension = Path.GetExtension(fileName);
        if (!_options.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new DomainException($"File type '{extension}' is not allowed.");
        }
    }

    private void EnsureCanUpload(Document document, Guid? workflowStepActionId)
    {
        if (workflowStepActionId.HasValue)
        {
            if (document.Status is not (DocumentStatus.InApproval or DocumentStatus.ReturnedForCorrection))
            {
                throw new DomainException("Comment attachments are only allowed during active approval.");
            }

            return;
        }

        if (document.OwnerUserId != _currentUser.UserId && !_currentUser.IsInRole(RoleNames.SuperAdmin))
        {
            throw new DomainException("Only the document owner can upload attachments.");
        }

        if (document.Status is not (DocumentStatus.Draft or DocumentStatus.ReturnedForCorrection))
        {
            throw new DomainException("Attachments can only be added before submission or during correction.");
        }
    }

    private void EnsureCanView(Document document)
    {
        if (document.OwnerUserId == _currentUser.UserId ||
            _currentUser.IsInRole(RoleNames.SuperAdmin) ||
            _currentUser.IsInRole(RoleNames.Auditor))
        {
            return;
        }

        var userId = _currentUser.UserId;
        var canView = document.WorkflowSteps.Any(s =>
            s.ApproverUserId == userId ||
            s.Actions.Any(a => a.ActorUserId == userId));

        if (!canView && document.Status != DocumentStatus.Draft)
        {
            throw new DomainException("You are not authorized to view attachments for this document.");
        }

        if (document.Status == DocumentStatus.Draft && document.OwnerUserId != userId)
        {
            throw new DomainException("Draft document attachments are visible to the owner only.");
        }
    }

    private static void EnsureAttachmentAccessible(Attachment attachment)
    {
        if (attachment.ScanStatus != AttachmentScanStatus.Clean)
        {
            throw new DomainException("Attachment is not available until scanning completes successfully.");
        }
    }

    private async Task<Document> LoadDocumentAsync(Guid documentId, CancellationToken cancellationToken)
    {
        return await _db.Documents
            .Include(d => d.WorkflowSteps)
                .ThenInclude(s => s.Actions)
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken)
            ?? throw new DomainException("Document not found.");
    }

    private static AttachmentDto MapAttachment(Attachment attachment) =>
        new(
            attachment.Id,
            attachment.DocumentId,
            attachment.FileName,
            attachment.LogicalName,
            attachment.ContentType,
            attachment.FileSizeBytes,
            attachment.VersionNumber,
            attachment.IsLatest,
            attachment.ScanStatus.ToString(),
            attachment.DownloadRestricted,
            attachment.CreatedAtUtc);

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (_db is IUnitOfWork unitOfWork)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
