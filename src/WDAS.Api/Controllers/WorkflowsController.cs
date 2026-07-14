using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WDAS.Application.Models;
using WDAS.Application.Services;

namespace WDAS.Api.Controllers;

[ApiController]
[Route("api/workflows")]
[Authorize]
public class WorkflowsController : ControllerBase
{
    private readonly WorkflowService _workflowService;

    public WorkflowsController(WorkflowService workflowService)
    {
        _workflowService = workflowService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkflowDto>>> GetWorkflows([FromQuery] Guid? departmentId, [FromQuery] bool? isActive, CancellationToken cancellationToken)
    {
        return Ok(await _workflowService.GetWorkflowsAsync(departmentId, isActive, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = "perm:config.workflows.make")]
    public async Task<ActionResult<WorkflowDto>> CreateWorkflow([FromBody] CreateWorkflowRequest request, CancellationToken cancellationToken)
    {
        return CreatedAtAction(nameof(GetWorkflows), new { }, await _workflowService.CreateWorkflowAsync(request, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:config.workflows.check")]
    public async Task<ActionResult<WorkflowDto>> UpdateWorkflow(Guid id, [FromBody] UpdateWorkflowRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _workflowService.UpdateWorkflowAsync(id, request, cancellationToken));
    }

    [HttpGet("{id:guid}/versions")]
    [Authorize(Policy = "perm:config.workflows")]
    public async Task<ActionResult<IReadOnlyList<WorkflowVersionSummaryDto>>> GetVersions(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _workflowService.GetVersionsAsync(id, cancellationToken));
    }

    [HttpGet("{id:guid}/matrix-tiers")]
    [Authorize(Policy = "perm:config.workflows")]
    public async Task<ActionResult<IReadOnlyList<MatrixTierDto>>> GetMatrixTiers(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _workflowService.GetMatrixTiersAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/matrix-tiers")]
    [Authorize(Policy = "perm:config.workflows.make")]
    public async Task<ActionResult<IReadOnlyList<MatrixTierDto>>> SaveMatrixTiers(Guid id, [FromBody] SaveMatrixTiersRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _workflowService.SaveMatrixTiersAsync(id, request, cancellationToken));
    }

    [HttpGet("{id:guid}/approver-groups")]
    [Authorize(Policy = "perm:config.workflows")]
    public async Task<ActionResult<IReadOnlyList<ApproverGroupDto>>> GetApproverGroups(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _workflowService.GetApproverGroupsAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/approver-groups")]
    [Authorize(Policy = "perm:config.workflows.make")]
    public async Task<ActionResult<IReadOnlyList<ApproverGroupDto>>> SaveApproverGroups(Guid id, [FromBody] SaveApproverGroupsRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _workflowService.SaveApproverGroupsAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:guid}/clone-matrix-from/{sourceId:guid}")]
    [Authorize(Policy = "perm:config.workflows.make")]
    public async Task<ActionResult<IReadOnlyList<MatrixTierDto>>> CloneMatrix(Guid id, Guid sourceId, CancellationToken cancellationToken)
    {
        return Ok(await _workflowService.CloneMatrixFromWorkflowAsync(id, sourceId, cancellationToken));
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Policy = "perm:config.workflows.check")]
    public async Task<ActionResult<WorkflowDto>> SetWorkflowStatus(Guid id, [FromBody] SetActiveStatusRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _workflowService.SetWorkflowActiveStatusAsync(id, request.IsActive, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "perm:config.workflows.check")]
    public async Task<IActionResult> DeleteWorkflow(Guid id, CancellationToken cancellationToken)
    {
        await _workflowService.DeleteWorkflowAsync(id, cancellationToken);
        return NoContent();
    }
}
