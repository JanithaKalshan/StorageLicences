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
/// </summary>
public static class PlacementSchedulingPolicy
{
    public static Result Evaluate(PlacementSchedulingRequest request)
    {
        var errors = new List<Error>();

        // Rule 1 (unit exists) is handled by the application layer before this policy runs.

        // Rule 2 — Licence covers scheduled date.
        var licence = request.CurrentLicence;
        if (licence is null || licence.UnitId != request.Unit.Id || !licence.CoversDate(request.ScheduledDate))
        {
            errors.Add(SchedulingErrors.LicenceNotCoveringDate);
        }
        else if (licence.SurrenderedDate.HasValue && request.ScheduledDate >= licence.SurrenderedDate.Value)
        {
            errors.Add(SchedulingErrors.LicenceSurrendered);
        }

        // Rule 3 — Current licence holder.
        if (licence is not null && !string.Equals(licence.HolderId, request.AssertedRequesterId, StringComparison.Ordinal))
        {
            errors.Add(SchedulingErrors.NotCurrentHolder);
        }

        var activePlacements = request.ExistingPlacements.Where(p => p.CountsTowardCapacity).ToList();

        // Rule 4 — Capacity.
        if (request.PlacementClass == PlacementClass.Pallet)
        {
            var palletCount = activePlacements.Count(p => p.PlacementClass == PlacementClass.Pallet);
            if (palletCount >= request.Unit.PalletCapacity)
                errors.Add(SchedulingErrors.PalletCapacityExceeded);
        }
        else
        {
            var boxCount = activePlacements.Count(p => p.PlacementClass == PlacementClass.Box);
            if (boxCount >= request.Unit.BoxCapacity)
                errors.Add(SchedulingErrors.BoxCapacityExceeded);
        }

        // Rule 5 — Pallet spacing (pallets only; ignores boxes and cancelled placements).
        if (request.PlacementClass == PlacementClass.Pallet)
        {
            var tooClose = activePlacements
                .Where(p => p.PlacementClass == PlacementClass.Pallet)
                .Any(p => Math.Abs(MonthsBetween(p.ScheduledDate, request.ScheduledDate)) < 12);

            if (tooClose)
                errors.Add(SchedulingErrors.PalletGapTooSmall);
        }

        // Rule 6 — Item intake date.
        if (request.Item.IntakeDate is null)
        {
            errors.Add(SchedulingErrors.ItemIntakeDateMissing);
        }
        else if (request.ScheduledDate < request.Item.IntakeDate.Value)
        {
            errors.Add(SchedulingErrors.ItemNotAvailableOnDate);
        }

        // Rule 7 — Scheduling window.
        if (request.ScheduledDate > request.RequestDate.AddMonths(24))
        {
            errors.Add(SchedulingErrors.SchedulingDateTooFar);
        }

        return errors.Count == 0 ? Result.Success() : Result.Failure(errors);
    }

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
