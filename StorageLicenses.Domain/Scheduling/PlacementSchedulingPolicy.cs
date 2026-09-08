using StorageLicenses.Domain.Common;
using StorageLicenses.Domain.Entities;
using StorageLicenses.Domain.Enums;

namespace StorageLicenses.Domain.Scheduling;

/// <summary>
/// Input required to evaluate a placement scheduling request.
/// Note: unit existence (Rule 1) is expected to be handled by the application layer as a
/// not-found result before this policy is invoked.
/// </summary>
public sealed class PlacementSchedulingRequest
{
    public required Unit Unit { get; init; }

    public required Item Item { get; init; }

    /// <summary>The licence in force for the unit on <see cref="ScheduledDate"/>, if any.</summary>
    public StorageLicence? CurrentLicence { get; init; }

    /// <summary>All other pallet placements in the same unit (any status), used for spacing checks.</summary>
    public required IReadOnlyCollection<Placement> ExistingPlacements { get; init; }

    public required string AssertedRequesterId { get; init; }

    public required PlacementClass PlacementClass { get; init; }

    public required DateOnly ScheduledDate { get; init; }

    public required DateOnly RequestDate { get; init; }
}

public static class SchedulingErrors
{
    public static readonly Error LicenceNotCoveringDate = new("LICENCE_NOT_COVERING_DATE", "No licence covers the requested scheduled date.");
    public static readonly Error LicenceSurrendered = new("LICENCE_SURRENDERED", "The licence has been surrendered as of the requested scheduled date.");
    public static readonly Error NotCurrentHolder = new("NOT_CURRENT_HOLDER", "The asserted requester is not the current licence holder.");
    public static readonly Error PalletCapacityExceeded = new("PALLET_CAPACITY_EXCEEDED", "The unit has no remaining pallet capacity.");
    public static readonly Error BoxCapacityExceeded = new("BOX_CAPACITY_EXCEEDED", "The unit has no remaining box capacity.");
    public static readonly Error PalletGapTooSmall = new("PALLET_GAP_TOO_SMALL", "Pallet placements in the same unit must be at least 12 calendar months apart.");
    public static readonly Error ItemIntakeDateMissing = new("ITEM_INTAKE_DATE_MISSING", "The item does not have a recorded intake date.");
    public static readonly Error ItemNotAvailableOnDate = new("ITEM_NOT_AVAILABLE_ON_DATE", "The scheduled date is earlier than the item's intake date.");
    public static readonly Error SchedulingDateTooFar = new("SCHEDULING_DATE_TOO_FAR", "The scheduled date is more than 24 calendar months after the request date.");
}

/// <summary>
/// Evaluates the seven business rules governing whether an item may be scheduled for
/// placement into a unit. All applicable rule failures are returned together.
///
/// Individual rule checks are exposed as public methods so callers that only have partial
/// context (e.g. the unit availability endpoint, which has no requester/item/request-date)
/// can evaluate a meaningful subset of rules without fabricating missing information.
/// </summary>
public static class PlacementSchedulingPolicy
{
    public static Result Evaluate(PlacementSchedulingRequest request)
    {
        var errors = new List<Error>();

        // Rule 1 (unit exists) is handled by the application layer before this policy runs.

        errors.AddRange(CheckLicenceCoverage(request.CurrentLicence, request.Unit.Id, request.ScheduledDate));
        errors.AddRange(CheckCurrentHolder(request.CurrentLicence, request.AssertedRequesterId));
        errors.AddRange(CheckCapacity(request.Unit, request.ExistingPlacements, request.PlacementClass));
        errors.AddRange(CheckPalletSpacing(request.ExistingPlacements, request.PlacementClass, request.ScheduledDate));
        errors.AddRange(CheckItemIntake(request.Item, request.ScheduledDate));
        errors.AddRange(CheckSchedulingWindow(request.RequestDate, request.ScheduledDate));

        return errors.Count == 0 ? Result.Success() : Result.Failure(errors);
    }

    /// <summary>
    /// Rule 2 — the licence must belong to the unit and cover the scheduled date, and must not
    /// have been surrendered as of that date.
    /// </summary>
    public static IReadOnlyList<Error> CheckLicenceCoverage(StorageLicence? licence, int unitId, DateOnly scheduledDate)
    {
        if (licence is null || licence.UnitId != unitId || !licence.IsWithinTerm(scheduledDate))
        {
            return [SchedulingErrors.LicenceNotCoveringDate];
        }

        if (licence.IsSurrenderedAsOf(scheduledDate))
        {
            return [SchedulingErrors.LicenceSurrendered];
        }

        return [];
    }

    /// <summary>
    /// Rule 3 — the asserted requester must match the current licence holder.
    /// Requires a licence to be known; produces no error when <paramref name="licence"/> is null
    /// since Rule 2 already reports the missing-coverage failure in that case.
    /// </summary>
    public static IReadOnlyList<Error> CheckCurrentHolder(StorageLicence? licence, string assertedRequesterId)
    {
        if (licence is not null && !string.Equals(licence.HolderId, assertedRequesterId, StringComparison.Ordinal))
        {
            return [SchedulingErrors.NotCurrentHolder];
        }

        return [];
    }

    /// <summary>
    /// Rule 4 — active (scheduled/completed) placements of the requested class must not already
    /// occupy the unit's full capacity for that class.
    /// </summary>
    public static IReadOnlyList<Error> CheckCapacity(Unit unit, IReadOnlyCollection<Placement> existingPlacements, PlacementClass placementClass)
    {
        var activeCount = existingPlacements.Count(p => p.CountsTowardCapacity && p.PlacementClass == placementClass);

        if (placementClass == PlacementClass.Pallet)
        {
            return activeCount >= unit.PalletCapacity ? [SchedulingErrors.PalletCapacityExceeded] : [];
        }

        return activeCount >= unit.BoxCapacity ? [SchedulingErrors.BoxCapacityExceeded] : [];
    }

    /// <summary>
    /// Rule 5 — pallet placements in the same unit must be at least 12 calendar months apart
    /// (in either direction). Does not apply to boxes.
    /// </summary>
    public static IReadOnlyList<Error> CheckPalletSpacing(IReadOnlyCollection<Placement> existingPlacements, PlacementClass placementClass, DateOnly scheduledDate)
    {
        if (placementClass != PlacementClass.Pallet)
            return [];

        var tooClose = existingPlacements
            .Where(p => p.CountsTowardCapacity && p.PlacementClass == PlacementClass.Pallet)
            .Any(p => Math.Abs(MonthsBetween(p.ScheduledDate, scheduledDate)) < 12);

        return tooClose ? [SchedulingErrors.PalletGapTooSmall] : [];
    }

    /// <summary>
    /// Rule 6 — the item must have a recorded intake date that is not later than the scheduled date.
    /// </summary>
    public static IReadOnlyList<Error> CheckItemIntake(Item item, DateOnly scheduledDate)
    {
        if (item.IntakeDate is null)
            return [SchedulingErrors.ItemIntakeDateMissing];

        return scheduledDate < item.IntakeDate.Value ? [SchedulingErrors.ItemNotAvailableOnDate] : [];
    }

    /// <summary>
    /// Rule 7 — the scheduled date must be no more than 24 calendar months after the request date.
    /// </summary>
    public static IReadOnlyList<Error> CheckSchedulingWindow(DateOnly requestDate, DateOnly scheduledDate) =>
        scheduledDate > requestDate.AddMonths(24) ? [SchedulingErrors.SchedulingDateTooFar] : [];

    /// <summary>
    /// Whole calendar months between two dates, computed so that exactly-N-month gaps
    /// (e.g. 12 months) are represented precisely, independent of day-count variations.
    /// </summary>
    private static int MonthsBetween(DateOnly from, DateOnly to)
    {
        var months = (to.Year - from.Year) * 12 + (to.Month - from.Month);

        // Adjust when the day-of-month of `to` hasn't yet reached that of `from`,
        // so a gap like 2025-11-15 -> 2026-11-01 counts as < 12 months, not exactly 12.
        if (to.Day < from.Day)
            months += to < from ? 1 : -1;

        return months;
    }
}
