using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WDAS.Application.Models;
using WDAS.Application.Services;

namespace WDAS.Api.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly DocumentService _documentService;
    private readonly CancellationService _cancellationService;
    private readonly FinalizationService _finalizationService;

    public DocumentsController(
        DocumentService documentService,
        CancellationService cancellationService,
        FinalizationService finalizationService)
    {
        _documentService = documentService;
        _cancellationService = cancellationService;
        _finalizationService = finalizationService;
    }

    [HttpPost]
    public async Task<ActionResult<DocumentDto>> CreateDocument([FromBody] CreateDocumentRequest request, CancellationToken cancellationToken)
    {
        var document = await _documentService.CreateDocumentAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetDocument), new { id = document.Id }, document);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DocumentDto>> GetDocument(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _documentService.GetDocumentAsync(id, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DocumentDto>> UpdateDocument(Guid id, [FromBody] UpdateDocumentRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _documentService.UpdateDocumentAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid id, CancellationToken cancellationToken)
    {
        await _documentService.DeleteDocumentAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<DocumentDto>> Cancel(Guid id, [FromBody] CancelDocumentRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _cancellationService.CancelAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:guid}/finalize")]
    public async Task<ActionResult<RepositoryDocumentDto>> Finalize(Guid id, [FromBody] FinalizeDocumentRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _finalizationService.FinalizeAsync(id, request, cancellationToken));
    }
}

[ApiController]
[Route("api/workflow-steps")]
[Authorize]
public class WorkflowStepsController : ControllerBase
{
    private readonly WorkflowEngineService _workflowEngineService;
    private readonly DelegationService _delegationService;

    public WorkflowStepsController(
        WorkflowEngineService workflowEngineService,
        DelegationService delegationService)
    {
        _workflowEngineService = workflowEngineService;
        _delegationService = delegationService;
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<DocumentDto>> Approve(Guid id, [FromBody] WorkflowActionRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _workflowEngineService.ApproveAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<DocumentDto>> Reject(Guid id, [FromBody] WorkflowActionRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _workflowEngineService.RejectAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:guid}/return")]
    public async Task<ActionResult<DocumentDto>> Return(Guid id, [FromBody] WorkflowActionRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _workflowEngineService.ReturnAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:guid}/comment")]
    public async Task<ActionResult<DocumentDto>> Comment(Guid id, [FromBody] WorkflowActionRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _workflowEngineService.CommentAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:guid}/reassign")]
    [Authorize(Policy = "WorkflowAdmin")]
    public async Task<ActionResult<DocumentDto>> Reassign(Guid id, [FromBody] ReassignStepRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _delegationService.ReassignStepAsync(id, request, cancellationToken));
    }
}
