namespace WDAS.Domain.Enums;

public enum AuditEventType
{
    Create = 1,
    View = 2,
    Update = 3,
    Approve = 4,
    Reject = 5,
    ReturnForCorrection = 6,
    Comment = 7,
    Cancel = 8,
    Finalize = 9,
    Reassign = 10,
    Login = 11,
    Sync = 12,
    Export = 13,
    Search = 14,
    AttachmentUpload = 15,
    Delegation = 16,
    ExternalAccess = 17
}
