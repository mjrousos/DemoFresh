namespace DemoFresh.Models;

public record ActionResult(
    ActionResultType Type,
    string? PrUrl,
    string? DelegationConfirmation,
    string? ErrorMessage);

public enum ActionResultType
{
    PrCreated,
    Delegated,
    NoActionNeeded,
    Failed
}
