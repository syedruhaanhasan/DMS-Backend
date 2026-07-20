using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WDAS.Application;
using WDAS.Application.Abstractions;
using WDAS.Application.Audit;
using WDAS.Application.Models;
using WDAS.Domain.Entities;
using WDAS.Domain.Enums;
using WDAS.Domain.Exceptions;

namespace WDAS.Application.Services;

public class DocumentTypeService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IAuditWriter _auditWriter;

    public DocumentTypeService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IClock clock,
        IAuditWriter auditWriter)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _auditWriter = auditWriter;
    }

    public async Task<IReadOnlyList<DocumentTypeDto>> ListAsync(string? query = null, bool? isActive = null, CancellationToken cancellationToken = default)
    {
        var types = _db.DocumentTypeDefinitions.AsNoTracking();

        if (isActive == true)
        {
            types = types.Where(t => t.IsActive);
        }
        else if (isActive == false)
        {
            types = types.Where(t => !t.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLowerInvariant();
            types = types.Where(t =>
                t.Name.ToLower().Contains(term) ||
                t.Code.ToLower().Contains(term) ||
                (t.Description != null && t.Description.ToLower().Contains(term)));
        }

        return await types
            .OrderBy(t => t.Name)
            .Select(t => new DocumentTypeDto(IdParsing.ToApi(t.Id), t.Name, t.Code, t.Description, t.Category, t.AmountRequired, t.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<DocumentTypeDto> CreateAsync(CreateDocumentTypeRequest request, CancellationToken cancellationToken = default)
    {
        EnsureSuperAdmin();

        var name = request.Name.Trim();
        var code = NormalizeCode(request.Code);
        var category = NormalizeCategory(request.Category);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Document type name is required.");
        }

        if (string.IsNullOrWhiteSpace(code) || code.Length < 2)
        {
            throw new DomainException("Document type code must be at least 2 characters.");
        }

        if (await _db.DocumentTypeDefinitions.AnyAsync(t => t.Code == code, cancellationToken))
        {
            throw new ConflictException("code_taken", "A document type with this code already exists.");
        }

        var now = _clock.UtcNow;
        var amountRequired = request.AmountRequired ?? category == "financial";
        var documentType = new DocumentTypeDefinition
        {
            Name = name,
            Code = code,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Category = category,
            AmountRequired = amountRequired,
            IsActive = true,
            CreatedAtUtc = now,
        };

        _db.Add(documentType);
        await SaveAsync(cancellationToken);

        await _auditWriter.WriteAsync(new AuditWriteRequest(
            AuditEventType.Update,
            "Document type created",
            ActorUserId: _currentUser.UserId,
            EntityType: "DocumentType",
            EntityId: documentType.Id.ToString(),
            DetailsJson: AuditDetailsBuilder.Create()
                .Set("documentTypeId", documentType.Id)
                .TrackCreated("Name", documentType.Name)
                .TrackCreated("Code", documentType.Code)
                .TrackCreated("Category", documentType.Category)
                .ToJson()),
            cancellationToken);

        return MapDocumentType(documentType);
    }

    public async Task<DocumentTypeDto> UpdateAsync(int documentTypeId, UpdateDocumentTypeRequest request, CancellationToken cancellationToken = default)
    {
        EnsureSuperAdmin();

        var documentType = await _db.DocumentTypeDefinitions.FirstOrDefaultAsync(t => t.Id == documentTypeId, cancellationToken)
            ?? throw new DomainException("Document type not found.");

        var oldName = documentType.Name;
        var oldDescription = documentType.Description;
        var oldCategory = documentType.Category;
        var oldAmountRequired = documentType.AmountRequired;
        var oldActive = documentType.IsActive;

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            documentType.Name = request.Name.Trim();
        }

        if (request.Description is not null)
        {
            documentType.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            documentType.Category = NormalizeCategory(request.Category);
            if (documentType.Category != "financial")
            {
                documentType.AmountRequired = false;
            }
        }

        if (request.AmountRequired.HasValue && documentType.Category == "financial")
        {
            documentType.AmountRequired = request.AmountRequired.Value;
        }

        if (request.IsActive.HasValue)
        {
            documentType.IsActive = request.IsActive.Value;
        }

        documentType.UpdatedAtUtc = _clock.UtcNow;
        await SaveAsync(cancellationToken);

        await _auditWriter.WriteAsync(new AuditWriteRequest(
            AuditEventType.Update,
            "Document type updated",
            ActorUserId: _currentUser.UserId,
            EntityType: "DocumentType",
            EntityId: documentTypeId.ToString(),
            DetailsJson: AuditDetailsBuilder.Create()
                .Set("documentTypeId", documentTypeId)
                .Track("Name", oldName, documentType.Name)
                .Track("Description", oldDescription, documentType.Description)
                .Track("Category", oldCategory, documentType.Category)
                .Track("Amount required", oldAmountRequired, documentType.AmountRequired)
                .Track("Active", oldActive, documentType.IsActive)
                .ToJson()),
            cancellationToken);

        return MapDocumentType(documentType);
    }

    public async Task DeleteAsync(int documentTypeId, CancellationToken cancellationToken = default)
    {
        EnsureSuperAdmin();

        var documentType = await _db.DocumentTypeDefinitions.FirstOrDefaultAsync(t => t.Id == documentTypeId, cancellationToken)
            ?? throw new DomainException("Document type not found.");

        if (await _db.Workflows.AnyAsync(w => w.DocumentType == documentType.Code && w.IsActive, cancellationToken))
        {
            throw new DomainException("Cannot delete a document type that is used by active workflows. Deactivate it instead.");
        }

        documentType.IsActive = false;
        documentType.UpdatedAtUtc = _clock.UtcNow;
        await SaveAsync(cancellationToken);

        await _auditWriter.WriteAsync(new AuditWriteRequest(
            AuditEventType.Update,
            "Document type deactivated",
            ActorUserId: _currentUser.UserId,
            DetailsJson: JsonSerializer.Serialize(new { documentTypeId })),
            cancellationToken);
    }

    private void EnsureSuperAdmin()
    {
        if (!_currentUser.IsInRole(RoleNames.SuperAdmin) &&
            !_currentUser.HasPermission(PermissionCatalog.Actions.DocumentTypesManage) &&
            !_currentUser.HasPermission(PermissionCatalog.Config.DocumentTypes) &&
            !_currentUser.HasPermission(PermissionCatalog.Config.DocumentTypesMake) &&
            !_currentUser.HasPermission(PermissionCatalog.Config.DocumentTypesCheck))
        {
            throw new DomainException("You do not have permission to manage document types.");
        }
    }

    private static string NormalizeCode(string code) =>
        new string(code.Trim().Where(c => char.IsLetterOrDigit(c)).ToArray());

    private static string NormalizeCategory(string? category) =>
        string.Equals(category, "financial", StringComparison.OrdinalIgnoreCase) ? "financial" : "non_financial";

    private static DocumentTypeDto MapDocumentType(DocumentTypeDefinition documentType) =>
        new(
            IdParsing.ToApi(documentType.Id),
            documentType.Name,
            documentType.Code,
            documentType.Description,
            documentType.Category,
            documentType.AmountRequired,
            documentType.IsActive);

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (_db is IUnitOfWork unitOfWork)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
