using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using WDAS.Application;
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
    private readonly IServiceScopeFactory _scopeFactory;

    public DocumentsController(
        DocumentService documentService,
        CancellationService cancellationService,
        FinalizationService finalizationService,
        IServiceScopeFactory scopeFactory)
    {
        _documentService = documentService;
        _cancellationService = cancellationService;
        _finalizationService = finalizationService;
        _scopeFactory = scopeFactory;
    }

    [HttpPost]
    public async Task<ActionResult<DocumentDto>> CreateDocument([FromBody] CreateDocumentRequest request, CancellationToken cancellationToken)
    {
        var document = await _documentService.CreateDocumentAsync(request, cancellationToken);
        if (request.Submit)
        {
            SchedulePostSubmitSideEffects(IdParsing.ParseRequired(document.Id, "Document id"));
        }

        return CreatedAtAction(nameof(GetDocument), new { id = document.Id }, document);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DocumentDto>> GetDocument(int id, CancellationToken cancellationToken)
    {
        return Ok(await _documentService.GetDocumentAsync(id, cancellationToken));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<DocumentDto>> UpdateDocument(int id, [FromBody] UpdateDocumentRequest request, CancellationToken cancellationToken)
    {
        var document = await _documentService.UpdateDocumentAsync(id, request, cancellationToken);
        if (request.Submit)
        {
            SchedulePostSubmitSideEffects(IdParsing.ParseRequired(document.Id, "Document id"));
        }

        return Ok(document);
    }

    [HttpPost("{id:int}/submit")]
    public async Task<ActionResult<DocumentDto>> SubmitDocument(int id, [FromBody] SubmitDocumentRequest? request, CancellationToken cancellationToken)
    {
        var document = await _documentService.SubmitDocumentAsync(id, request?.IdempotencyKey, cancellationToken);
        SchedulePostSubmitSideEffects(IdParsing.ParseRequired(document.Id, "Document id"));
        return Ok(document);
    }

    [HttpPost("{id:int}/revise")]
    public async Task<ActionResult<DocumentDto>> ReviseRejectedDocument(int id, CancellationToken cancellationToken)
    {
        return Ok(await _documentService.ReviseRejectedDocumentAsync(id, cancellationToken));
    }

    [HttpPost("{id:int}/reviewers")]
    public async Task<ActionResult<DocumentDto>> AddReviewer(int id, [FromBody] AddReviewerRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _documentService.AddReviewerAsync(id, request, cancellationToken));
    }

    private void SchedulePostSubmitSideEffects(int documentId)
    {
        // Run indexing/notifications after the response so submit feels instant even if SMTP is slow/down.
        var scopeFactory = _scopeFactory;
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var docs = scope.ServiceProvider.GetRequiredService<DocumentService>();
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                await docs.RunPostSubmitSideEffectsAsync(documentId, cts.Token);
            }
            catch
            {
                // Side effects are best-effort.
            }
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteDocument(int id, CancellationToken cancellationToken)
    {
        await _documentService.DeleteDocumentAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<DocumentDto>> Cancel(int id, [FromBody] CancelDocumentRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _cancellationService.CancelAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:int}/finalize")]
    public async Task<ActionResult<RepositoryDocumentDto>> Finalize(int id, [FromBody] FinalizeDocumentRequest request, CancellationToken cancellationToken)
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

    [HttpPost("{id:int}/approve")]
    public async Task<ActionResult<DocumentDto>> Approve(int id, [FromBody] WorkflowActionRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _workflowEngineService.ApproveAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:int}/reject")]
    public async Task<ActionResult<DocumentDto>> Reject(int id, [FromBody] WorkflowActionRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _workflowEngineService.RejectAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:int}/return")]
    public async Task<ActionResult<DocumentDto>> Return(int id, [FromBody] WorkflowActionRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _workflowEngineService.ReturnAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:int}/comment")]
    public async Task<ActionResult<DocumentDto>> Comment(int id, [FromBody] WorkflowActionRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _workflowEngineService.CommentAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:int}/reassign")]
    [Authorize(Policy = "WorkflowAdmin")]
    public async Task<ActionResult<DocumentDto>> Reassign(int id, [FromBody] ReassignStepRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _delegationService.ReassignStepAsync(id, request, cancellationToken));
    }
}
