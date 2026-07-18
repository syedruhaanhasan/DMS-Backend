namespace WDAS.Domain.Enums;

public enum NotificationEventType
{
    SubmittedForApproval = 1,
    ApprovalRecorded = 2,
    Rejected = 3,
    ReturnedForCorrection = 4,
    Cancelled = 5,
    Finalized = 6,
    SlaBreach = 7,
    ExternalOtp = 8,
    DelegationNotice = 9,
    AddedAsReviewer = 10
}
