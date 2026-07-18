using WDAS.Domain.Common;

namespace WDAS.Domain.Entities;

public class ExternalApproverSession : Entity
{
    public int WorkflowStepId { get; set; }
    public string ApproverName { get; set; } = string.Empty;
    public string ApproverEmail { get; set; } = string.Empty;
    public string SecureTokenHash { get; set; } = string.Empty;
    public string? OtpHash { get; set; }
    public DateTime LinkExpiresAtUtc { get; set; }
    public DateTime? OtpExpiresAtUtc { get; set; }
    public bool OtpVerified { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
    public string? VerifiedFromIp { get; set; }

    public WorkflowStep WorkflowStep { get; set; } = null!;
}
