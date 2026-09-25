namespace Surelance.Domain.Enums;

public enum UserRole
{
    Client = 1,
    Freelancer = 2,
    Arbitrator = 3
}

public enum ContractStatus
{
    Draft = 1,
    Active = 2,
    Completed = 3,
    Cancelled = 4
}

public enum MilestoneStatus
{
    Pending = 1,
    Funded = 2,
    Submitted = 3,
    Disputed = 4,
    Released = 5,
    Refunded = 6
}

public enum LedgerEntryType
{
    Fund = 1,
    Release = 2,
    Refund = 3
}

public enum DisputeStatus
{
    Open = 1,
    Resolved = 2
}

public enum DisputeResolution
{
    None = 0,
    RefundClient = 1,
    ReleaseFreelancer = 2
}
