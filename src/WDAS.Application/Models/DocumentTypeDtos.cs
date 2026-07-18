namespace WDAS.Application.Models;

public record DocumentTypeDto(
    string Id,
    string Name,
    string Code,
    string? Description,
    string Category,
    bool AmountRequired,
    bool IsActive);

public record CreateDocumentTypeRequest(
    string Name,
    string Code,
    string? Description,
    string? Category,
    bool? AmountRequired);

public record UpdateDocumentTypeRequest(
    string? Name,
    string? Description,
    string? Category,
    bool? AmountRequired,
    bool? IsActive);
