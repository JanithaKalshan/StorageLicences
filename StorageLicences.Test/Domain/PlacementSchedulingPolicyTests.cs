using StorageLicenses.Domain.Common;
using StorageLicenses.Domain.Entities;
using StorageLicenses.Domain.Enums;
using StorageLicenses.Domain.Scheduling;

namespace StorageLicences.Test.Domain;

public class PlacementSchedulingPolicyTests
{
    private const string HolderId = "COMPANY-ABC";
    private static readonly DateOnly RequestDate = new(2026, 1, 1);

    private static Unit CreateUnit(int palletCapacity = 3, int boxCapacity = 8) => new(palletCapacity, boxCapacity);

    private static Item CreateItem(DateOnly? intakeDate) => new("ITEM-001", "Test item", intakeDate);

    private static StorageLicence CreateLicence(
        Unit unit,
        DateOnly grantDate,
        int termYears,
        string holderId = HolderId,
        DateOnly? surrenderedDate = null)
    {
        var licence = new StorageLicence(unit, holderId, grantDate, termYears);
        if (surrenderedDate.HasValue)
            licence.Surrender(surrenderedDate.Value);

        return licence;
    }

    private static PlacementSchedulingRequest CreateRequest(
        Unit unit,
        Item item,
        StorageLicence? licence,
        PlacementClass placementClass,
        DateOnly scheduledDate,
        IReadOnlyCollection<Placement>? existingPlacements = null,
        string assertedRequesterId = HolderId,
        DateOnly? requestDate = null) => new()
    {
        Unit = unit,
        Item = item,
        CurrentLicence = licence,
        ExistingPlacements = existingPlacements ?? [],
        AssertedRequesterId = assertedRequesterId,
        PlacementClass = placementClass,
        ScheduledDate = scheduledDate,
        RequestDate = requestDate ?? RequestDate
    };

    // ----- Valid scenarios -----

    [Fact]
    public void Evaluate_ValidPalletPlacement_ReturnsSuccess()
    {
        var unit = CreateUnit();
        var licence = CreateLicence(unit, new DateOnly(2020, 1, 1), 10);
        var item = CreateItem(new DateOnly(2025, 1, 1));
        var request = CreateRequest(unit, item, licence, PlacementClass.Pallet, new DateOnly(2026, 1, 15));

        var result = PlacementSchedulingPolicy.Evaluate(request);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Evaluate_ValidBoxPlacement_ReturnsSuccess()
    {
        var unit = CreateUnit();
        var licence = CreateLicence(unit, new DateOnly(2020, 1, 1), 10);
        var item = CreateItem(new DateOnly(2025, 1, 1));
        var request = CreateRequest(unit, item, licence, PlacementClass.Box, new DateOnly(2026, 1, 15));

        var result = PlacementSchedulingPolicy.Evaluate(request);

        Assert.True(result.IsSuccess);
    }

    // ----- Rule 2: Licence covers scheduled date -----

    [Fact]
    public void Evaluate_NoLicence_ReturnsLicenceNotCoveringDate()
    {
        var unit = CreateUnit();
        var item = CreateItem(new DateOnly(2025, 1, 1));
        var request = CreateRequest(unit, item, licence: null, PlacementClass.Pallet, new DateOnly(2026, 1, 15));

        var result = PlacementSchedulingPolicy.Evaluate(request);

        Assert.True(result.IsFailure);
        Assert.Contains(SchedulingErrors.LicenceNotCoveringDate, result.Errors);
    }

    [Fact]
    public void Evaluate_ScheduledDateOutsideLicenceTerm_ReturnsLicenceNotCoveringDate()
    {
        var unit = CreateUnit();
        // Grant 2016-03-10, term 10 years -> covers through 2026-03-09 exclusive.
        var licence = CreateLicence(unit, new DateOnly(2016, 3, 10), 10);
        var item = CreateItem(new DateOnly(2025, 1, 1));
        var request = CreateRequest(unit, item, licence, PlacementClass.Pallet, new DateOnly(2026, 3, 10));

        var result = PlacementSchedulingPolicy.Evaluate(request);

        Assert.Contains(SchedulingErrors.LicenceNotCoveringDate, result.Errors);
    }

    [Fact]
    public void Evaluate_ScheduledDateOnLastDayOfLicenceTerm_LicenceCoversDate()
    {
        var unit = CreateUnit();
        var licence = CreateLicence(unit, new DateOnly(2016, 3, 10), 10);
        var item = CreateItem(new DateOnly(2025, 1, 1));
        var request = CreateRequest(unit, item, licence, PlacementClass.Pallet, new DateOnly(2026, 3, 9), requestDate: new DateOnly(2024, 3, 9));

        var result = PlacementSchedulingPolicy.Evaluate(request);

        Assert.DoesNotContain(SchedulingErrors.LicenceNotCoveringDate, result.Errors);
    }

    [Fact]
    public void Evaluate_SurrenderedLicenceBeforeScheduledDate_ReturnsLicenceSurrendered()
    {
        var unit = CreateUnit();
        var licence = CreateLicence(unit, new DateOnly(2020, 1, 1), 10, surrenderedDate: new DateOnly(2025, 6, 1));
        var item = CreateItem(new DateOnly(2024, 1, 1));
        var request = CreateRequest(unit, item, licence, PlacementClass.Pallet, new DateOnly(2025, 6, 1));

        var result = PlacementSchedulingPolicy.Evaluate(request);

        Assert.Contains(SchedulingErrors.LicenceSurrendered, result.Errors);
    }

    [Fact]
    public void Evaluate_SurrenderedLicenceAfterScheduledDate_DoesNotReturnLicenceSurrendered()
    {
        var unit = CreateUnit();
        var licence = CreateLicence(unit, new DateOnly(2020, 1, 1), 10, surrenderedDate: new DateOnly(2025, 6, 1));
        var item = CreateItem(new DateOnly(2024, 1, 1));
        var request = CreateRequest(unit, item, licence, PlacementClass.Pallet, new DateOnly(2025, 5, 1));

        var result = PlacementSchedulingPolicy.Evaluate(request);

        Assert.DoesNotContain(SchedulingErrors.LicenceSurrendered, result.Errors);
    }

    // ----- Rule 3: Current licence holder -----

    [Fact]
    public void Evaluate_AssertedRequesterNotCurrentHolder_ReturnsNotCurrentHolder()
    {
        var unit = CreateUnit();
        var licence = CreateLicence(unit, new DateOnly(2020, 1, 1), 10);
        var item = CreateItem(new DateOnly(2024, 1, 1));
        var request = CreateRequest(unit, item, licence, PlacementClass.Pallet, new DateOnly(2026, 1, 15), assertedRequesterId: "COMPANY-XYZ");

        var result = PlacementSchedulingPolicy.Evaluate(request);

        Assert.Contains(SchedulingErrors.NotCurrentHolder, result.Errors);
    }

    // ----- Rule 4: Capacity -----

    [Fact]
    public void Evaluate_PalletCapacityExceeded_ReturnsPalletCapacityExceeded()
    {
        var unit = CreateUnit(palletCapacity: 1);
        var licence = CreateLicence(unit, new DateOnly(2020, 1, 1), 10);
        var item = CreateItem(new DateOnly(2024, 1, 1));
        var otherItem = CreateItem(new DateOnly(2024, 1, 1));
        var existingPallet = new Placement(unit, otherItem, PlacementClass.Pallet, new DateOnly(2023, 1, 1));

        var request = CreateRequest(unit, item, licence, PlacementClass.Pallet, new DateOnly(2026, 1, 15),
            existingPlacements: [existingPallet]);

        var result = PlacementSchedulingPolicy.Evaluate(request);

        Assert.Contains(SchedulingErrors.PalletCapacityExceeded, result.Errors);
    }

    [Fact]
    public void Evaluate_BoxCapacityExceeded_ReturnsBoxCapacityExceeded()
    {
        var unit = CreateUnit(boxCapacity: 1);
        var licence = CreateLicence(unit, new DateOnly(2020, 1, 1), 10);
        var item = CreateItem(new DateOnly(2024, 1, 1));
        var otherItem = CreateItem(new DateOnly(2024, 1, 1));
        var existingBox = new Placement(unit, otherItem, PlacementClass.Box, new DateOnly(2023, 1, 1));

        var request = CreateRequest(unit, item, licence, PlacementClass.Box, new DateOnly(2026, 1, 15),
            existingPlacements: [existingBox]);

        var result = PlacementSchedulingPolicy.Evaluate(request);

        Assert.Contains(SchedulingErrors.BoxCapacityExceeded, result.Errors);
    }

    [Fact]
    public void Evaluate_CancelledPlacement_IsIgnoredForCapacity()
    {
        var unit = CreateUnit(palletCapacity: 1);
        var licence = CreateLicence(unit, new DateOnly(2020, 1, 1), 10);
        var item = CreateItem(new DateOnly(2024, 1, 1));
        var otherItem = CreateItem(new DateOnly(2024, 1, 1));
        var cancelledPallet = new Placement(unit, otherItem, PlacementClass.Pallet, new DateOnly(2023, 1, 1));
        cancelledPallet.Cancel();

        var request = CreateRequest(unit, item, licence, PlacementClass.Pallet, new DateOnly(2026, 1, 15),
            existingPlacements: [cancelledPallet]);

        var result = PlacementSchedulingPolicy.Evaluate(request);

        Assert.DoesNotContain(SchedulingErrors.PalletCapacityExceeded, result.Errors);
    }

    // ----- Rule 5: Pallet spacing -----

    [Fact]
    public void Evaluate_PalletGapLessThan12Months_ReturnsPalletGapTooSmall()
    {
        var unit = CreateUnit();
        var licence = CreateLicence(unit, new DateOnly(2020, 1, 1), 10);
        var item = CreateItem(new DateOnly(2024, 1, 1));
        var otherItem = CreateItem(new DateOnly(2024, 1, 1));
        var existingPallet = new Placement(unit, otherItem, PlacementClass.Pallet, new DateOnly(2025, 11, 1));

        var request = CreateRequest(unit, item, licence, PlacementClass.Pallet, new DateOnly(2026, 5, 15),
            existingPlacements: [existingPallet]);

        var result = PlacementSchedulingPolicy.Evaluate(request);

        Assert.Contains(SchedulingErrors.PalletGapTooSmall, result.Errors);
    }

    [Fact]
    public void Evaluate_PalletGapExactly12Months_IsValid()
    {
        var unit = CreateUnit();
        var licence = CreateLicence(unit, new DateOnly(2020, 1, 1), 10);
        var item = CreateItem(new DateOnly(2024, 1, 1));
        var otherItem = CreateItem(new DateOnly(2024, 1, 1));
        var existingPallet = new Placement(unit, otherItem, PlacementClass.Pallet, new DateOnly(2025, 1, 15));

        var request = CreateRequest(unit, item, licence, PlacementClass.Pallet, new DateOnly(2026, 1, 15),
            existingPlacements: [existingPallet]);

        var result = PlacementSchedulingPolicy.Evaluate(request);

        Assert.DoesNotContain(SchedulingErrors.PalletGapTooSmall, result.Errors);
    }

    [Fact]
    public void Evaluate_FuturePalletPlacementWithinGap_ReturnsPalletGapTooSmall()
    {
        var unit = CreateUnit();
        var licence = CreateLicence(unit, new DateOnly(2020, 1, 1), 10);
        var item = CreateItem(new DateOnly(2024, 1, 1));
        var otherItem = CreateItem(new DateOnly(2024, 1, 1));
        // Existing pallet placement is *after* the requested date, gap < 12 months.
        var futurePallet = new Placement(unit, otherItem, PlacementClass.Pallet, new DateOnly(2026, 8, 1));

        var request = CreateRequest(unit, item, licence, PlacementClass.Pallet, new DateOnly(2026, 1, 15),
            existingPlacements: [futurePallet]);

        var result = PlacementSchedulingPolicy.Evaluate(request);

        Assert.Contains(SchedulingErrors.PalletGapTooSmall, result.Errors);
    }

    [Fact]
    public void Evaluate_BoxPlacement_IgnoresPalletSpacingRule()
    {
        var unit = CreateUnit();
        var licence = CreateLicence(unit, new DateOnly(2020, 1, 1), 10);
        var item = CreateItem(new DateOnly(2024, 1, 1));
        var otherItem = CreateItem(new DateOnly(2024, 1, 1));
        var existingPallet = new Placement(unit, otherItem, PlacementClass.Pallet, new DateOnly(2025, 12, 20));

        var request = CreateRequest(unit, item, licence, PlacementClass.Box, new DateOnly(2026, 1, 15),
            existingPlacements: [existingPallet]);

        var result = PlacementSchedulingPolicy.Evaluate(request);

        Assert.DoesNotContain(SchedulingErrors.PalletGapTooSmall, result.Errors);
    }

    [Fact]
    public void Evaluate_CancelledPalletPlacement_IsIgnoredForSpacing()
    {
        var unit = CreateUnit();
        var licence = CreateLicence(unit, new DateOnly(2020, 1, 1), 10);
        var item = CreateItem(new DateOnly(2024, 1, 1));
        var otherItem = CreateItem(new DateOnly(2024, 1, 1));
        var cancelledPallet = new Placement(unit, otherItem, PlacementClass.Pallet, new DateOnly(2025, 12, 20));
        cancelledPallet.Cancel();

        var request = CreateRequest(unit, item, licence, PlacementClass.Pallet, new DateOnly(2026, 1, 15),
            existingPlacements: [cancelledPallet]);

        var result = PlacementSchedulingPolicy.Evaluate(request);

        Assert.DoesNotContain(SchedulingErrors.PalletGapTooSmall, result.Errors);
    }

    // ----- Rule 6: Item intake date -----

    [Fact]
    public void Evaluate_MissingIntakeDate_ReturnsItemIntakeDateMissing()
    {
        var unit = CreateUnit();
        var licence = CreateLicence(unit, new DateOnly(2020, 1, 1), 10);
        var item = CreateItem(intakeDate: null);
        var request = CreateRequest(unit, item, licence, PlacementClass.Pallet, new DateOnly(2026, 1, 15));

        var result = PlacementSchedulingPolicy.Evaluate(request);

        Assert.Contains(SchedulingErrors.ItemIntakeDateMissing, result.Errors);
    }

    [Fact]
    public void Evaluate_ScheduledDateBeforeIntakeDate_ReturnsItemNotAvailableOnDate()
    {
        var unit = CreateUnit();
        var licence = CreateLicence(unit, new DateOnly(2020, 1, 1), 10);
        var item = CreateItem(new DateOnly(2026, 2, 1));
        var request = CreateRequest(unit, item, licence, PlacementClass.Pallet, new DateOnly(2026, 1, 15));

        var result = PlacementSchedulingPolicy.Evaluate(request);

        Assert.Contains(SchedulingErrors.ItemNotAvailableOnDate, result.Errors);
    }

    [Fact]
    public void Evaluate_ScheduledDateEqualsIntakeDate_IsValid()
    {
        var unit = CreateUnit();
        var licence = CreateLicence(unit, new DateOnly(2020, 1, 1), 10);
        var item = CreateItem(new DateOnly(2026, 1, 15));
        var request = CreateRequest(unit, item, licence, PlacementClass.Pallet, new DateOnly(2026, 1, 15));

        var result = PlacementSchedulingPolicy.Evaluate(request);

        Assert.DoesNotContain(SchedulingErrors.ItemNotAvailableOnDate, result.Errors);
        Assert.DoesNotContain(SchedulingErrors.ItemIntakeDateMissing, result.Errors);
    }

    // ----- Rule 7: Scheduling window -----

    [Fact]
    public void Evaluate_ScheduledDateMoreThan24MonthsAfterRequestDate_ReturnsSchedulingDateTooFar()
    {
        var unit = CreateUnit();
        var licence = CreateLicence(unit, new DateOnly(2020, 1, 1), 20);
        var item = CreateItem(new DateOnly(2020, 1, 1));
        var requestDate = new DateOnly(2026, 1, 1);
        var scheduledDate = requestDate.AddMonths(24).AddDays(1);

        var request = CreateRequest(unit, item, licence, PlacementClass.Pallet, scheduledDate, requestDate: requestDate);

        var result = PlacementSchedulingPolicy.Evaluate(request);

        Assert.Contains(SchedulingErrors.SchedulingDateTooFar, result.Errors);
    }

    [Fact]
    public void Evaluate_ScheduledDateExactly24MonthsAfterRequestDate_IsValid()
    {
        var unit = CreateUnit();
        var licence = CreateLicence(unit, new DateOnly(2020, 1, 1), 20);
        var item = CreateItem(new DateOnly(2020, 1, 1));
        var requestDate = new DateOnly(2026, 1, 1);
        var scheduledDate = requestDate.AddMonths(24);

        var request = CreateRequest(unit, item, licence, PlacementClass.Pallet, scheduledDate, requestDate: requestDate);

        var result = PlacementSchedulingPolicy.Evaluate(request);

        Assert.DoesNotContain(SchedulingErrors.SchedulingDateTooFar, result.Errors);
    }

    // ----- Combined failures -----

    [Fact]
    public void Evaluate_MultipleViolations_ReturnsAllApplicableErrorsTogether()
    {
        var unit = CreateUnit(palletCapacity: 1);
        // Licence does not cover the scheduled date and requester mismatches.
        var licence = CreateLicence(unit, new DateOnly(2010, 1, 1), 5, holderId: "COMPANY-OTHER");
        var item = CreateItem(intakeDate: null);
        var otherItem = CreateItem(new DateOnly(2024, 1, 1));

        var requestDate = new DateOnly(2026, 1, 1);
        var scheduledDate = requestDate.AddMonths(30);
        // Existing pallet placed less than 12 months before the (far-future) scheduled date.
        var existingPallet = new Placement(unit, otherItem, PlacementClass.Pallet, scheduledDate.AddMonths(-6));

        var request = CreateRequest(unit, item, licence, PlacementClass.Pallet, scheduledDate,
            existingPlacements: [existingPallet], assertedRequesterId: HolderId, requestDate: requestDate);

        var result = PlacementSchedulingPolicy.Evaluate(request);

        Assert.True(result.IsFailure);
        Assert.Contains(SchedulingErrors.LicenceNotCoveringDate, result.Errors);
        Assert.Contains(SchedulingErrors.PalletCapacityExceeded, result.Errors);
        Assert.Contains(SchedulingErrors.PalletGapTooSmall, result.Errors);
        Assert.Contains(SchedulingErrors.ItemIntakeDateMissing, result.Errors);
        Assert.Contains(SchedulingErrors.SchedulingDateTooFar, result.Errors);
        Assert.True(result.Errors.Count >= 5);
    }
}
