namespace WDAS.Domain.Enums;

public enum DocumentStatus
{
    Draft = 1,
    Submitted = 2,
    InApproval = 3,
    ReturnedForCorrection = 4,
    Rejected = 5,
    ReadyForFinalization = 6,
    Cancelled = 7,
    Finalized = 8
}
