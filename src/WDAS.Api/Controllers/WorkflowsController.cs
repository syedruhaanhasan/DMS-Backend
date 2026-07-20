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
    public async Task<ActionResult<IReadOnlyList<WorkflowDto>>> GetWorkflows([FromQuery] int? departmentId, [FromQuery] bool? isActive, CancellationToken cancellationToken)
    {
        return Ok(await _workflowService.GetWorkflowsAsync(departmentId, isActive, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = "perm:config.workflows.make")]
    public async Task<ActionResult<WorkflowDto>> CreateWorkflow([FromBody] CreateWorkflowRequest request, CancellationToken cancellationToken)
    {
        return CreatedAtAction(nameof(GetWorkflows), new { }, await _workflowService.CreateWorkflowAsync(request, cancellationToken));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "perm:config.workflows.check")]
    public async Task<ActionResult<WorkflowDto>> UpdateWorkflow(int id, [FromBody] UpdateWorkflowRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _workflowService.UpdateWorkflowAsync(id, request, cancellationToken));
    }

    [HttpGet("{id:int}/routing")]
    public async Task<ActionResult<WorkflowRoutingDto>> GetRouting(int id, CancellationToken cancellationToken)
    {
        return Ok(await _workflowService.GetWorkflowRoutingAsync(id, cancellationToken));
    }

    [HttpGet("{id:int}/versions")]
    [Authorize(Policy = "perm:config.workflows")]
    public async Task<ActionResult<IReadOnlyList<WorkflowVersionSummaryDto>>> GetVersions(int id, CancellationToken cancellationToken)
    {
        return Ok(await _workflowService.GetVersionsAsync(id, cancellationToken));
    }

    [HttpGet("{id:int}/matrix-tiers")]
    [Authorize(Policy = "perm:config.workflows")]
    public async Task<ActionResult<IReadOnlyList<MatrixTierDto>>> GetMatrixTiers(int id, CancellationToken cancellationToken)
    {
        return Ok(await _workflowService.GetMatrixTiersAsync(id, cancellationToken));
    }

    [HttpPost("{id:int}/matrix-tiers")]
    [Authorize(Policy = "perm:config.workflows.make")]
    public async Task<ActionResult<IReadOnlyList<MatrixTierDto>>> SaveMatrixTiers(int id, [FromBody] SaveMatrixTiersRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _workflowService.SaveMatrixTiersAsync(id, request, cancellationToken));
    }

    [HttpGet("{id:int}/approver-groups")]
    [Authorize(Policy = "perm:config.workflows")]
    public async Task<ActionResult<IReadOnlyList<ApproverGroupDto>>> GetApproverGroups(int id, CancellationToken cancellationToken)
    {
        return Ok(await _workflowService.GetApproverGroupsAsync(id, cancellationToken));
    }

    [HttpPost("{id:int}/approver-groups")]
    [Authorize(Policy = "perm:config.workflows.make")]
    public async Task<ActionResult<IReadOnlyList<ApproverGroupDto>>> SaveApproverGroups(int id, [FromBody] SaveApproverGroupsRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _workflowService.SaveApproverGroupsAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:int}/clone-matrix-from/{sourceId:int}")]
    [Authorize(Policy = "perm:config.workflows.make")]
    public async Task<ActionResult<IReadOnlyList<MatrixTierDto>>> CloneMatrix(int id, int sourceId, CancellationToken cancellationToken)
    {
        return Ok(await _workflowService.CloneMatrixFromWorkflowAsync(id, sourceId, cancellationToken));
    }

    [HttpPut("{id:int}/status")]
    [Authorize(Policy = "perm:config.workflows.check")]
    public async Task<ActionResult<WorkflowDto>> SetWorkflowStatus(int id, [FromBody] SetActiveStatusRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _workflowService.SetWorkflowActiveStatusAsync(id, request.IsActive, cancellationToken));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "perm:config.workflows.check")]
    public async Task<IActionResult> DeleteWorkflow(int id, CancellationToken cancellationToken)
    {
        await _workflowService.DeleteWorkflowAsync(id, cancellationToken);
        return NoContent();
    }
}
