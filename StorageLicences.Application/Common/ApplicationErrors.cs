using StorageLicenses.Domain.Common;

namespace StorageLicences.Application.Common;

/// <summary>
/// Application-level errors (not tied to a single domain rule), e.g. not-found conditions
/// that are typically resolved by the application layer before invoking domain policies.
/// </summary>
public static class ApplicationErrors
{
    public static readonly Error UnitNotFound = new("UNIT_NOT_FOUND", "The requested unit does not exist.");
    public static readonly Error ItemNotFound = new("ITEM_NOT_FOUND", "The requested item does not exist.");
}
