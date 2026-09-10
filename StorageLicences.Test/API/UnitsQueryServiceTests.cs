using StorageLicences.Application.Units;
using StorageLicenses.Domain.Entities;
using StorageLicenses.Domain.Enums;
using StorageLicenses.Infastructure.Services;

namespace StorageLicences.Test.API;

public sealed class UnitsQueryServiceTests : IDisposable
{
    private readonly SqliteDbContextFixture _fixture = new();
    private readonly UnitsQueryService _sut;

    public UnitsQueryServiceTests()
    {
        _sut = new UnitsQueryService(_fixture.Context);
    }

    public void Dispose() => _fixture.Dispose();

    private static readonly DateOnly Today = new(2024, 6, 1);

    [Fact]
    public async Task GetUnitsAsync_FiltersByHasActiveLicence()
    {
        var withLicence = new Unit(1, 2);
        var withoutLicence = new Unit(1, 2);
        _fixture.Context.Units.AddRange(withLicence, withoutLicence);
        await _fixture.Context.SaveChangesAsync();

        _fixture.Context.StorageLicences.Add(new StorageLicence(withLicence, "holder-1", Today.AddMonths(-1), 5));
        await _fixture.Context.SaveChangesAsync();

        var query = new UnitListQuery(HasActiveLicence: true);
        var result = await _sut.GetUnitsAsync(query, CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(withLicence.Id, result.Items[0].Id);
        Assert.True(result.Items[0].HasActiveLicence);
    }

    [Fact]
    public async Task GetUnitsAsync_FiltersByMinRemainingPalletCapacity()
    {
        var full = new Unit(1, 0);
        var available = new Unit(2, 0);
        var item = new Item("REF-1", "desc");
        _fixture.Context.Units.AddRange(full, available);
        _fixture.Context.Items.Add(item);
        await _fixture.Context.SaveChangesAsync();

        // Occupy the single pallet slot on `full`.
        _fixture.Context.Placements.Add(new Placement(full, item, PlacementClass.Pallet, Today));
        await _fixture.Context.SaveChangesAsync();

        var query = new UnitListQuery(MinRemainingPalletCapacity: 1);
        var result = await _sut.GetUnitsAsync(query, CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(available.Id, result.Items[0].Id);
    }

    [Fact]
    public async Task GetUnitsAsync_PagesResultsAndReportsTotalCountOfFilteredSet()
    {
        for (var i = 0; i < 5; i++)
        {
            _fixture.Context.Units.Add(new Unit(1, 1));
        }
        await _fixture.Context.SaveChangesAsync();

        var query = new UnitListQuery(Page: 2, PageSize: 2);
        var result = await _sut.GetUnitsAsync(query, CancellationToken.None);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
    }

    [Fact]
    public async Task GetUnitsAsync_ExcludesCancelledPlacementsFromOccupancy()
    {
        var unit = new Unit(1, 0);
        var item = new Item("REF-1", "desc");
        _fixture.Context.Units.Add(unit);
        _fixture.Context.Items.Add(item);
        await _fixture.Context.SaveChangesAsync();

        var placement = new Placement(unit, item, PlacementClass.Pallet, Today);
        placement.Cancel();
        _fixture.Context.Placements.Add(placement);
        await _fixture.Context.SaveChangesAsync();

        var result = await _sut.GetUnitsAsync(new UnitListQuery(), CancellationToken.None);

        Assert.Equal(0, result.Items[0].OccupiedPalletCount);
        Assert.Equal(1, result.Items[0].RemainingPalletCapacity);
    }

    [Fact]
    public async Task GetUnitDetailAsync_ReturnsSuccess_ForExistingUnit()
    {
        var unit = new Unit(2, 3);
        var item = new Item("REF-1", "desc");
        _fixture.Context.Units.Add(unit);
        _fixture.Context.Items.Add(item);
        await _fixture.Context.SaveChangesAsync();

        _fixture.Context.StorageLicences.Add(new StorageLicence(unit, "holder-1", Today.AddYears(-1), 5));
        _fixture.Context.Placements.Add(new Placement(unit, item, PlacementClass.Box, Today));
        await _fixture.Context.SaveChangesAsync();

        var result = await _sut.GetUnitDetailAsync(unit.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(unit.Id, result.Value.Id);
        Assert.Single(result.Value.Licences);
        Assert.Single(result.Value.Placements);
        Assert.Equal(0, result.Value.OccupiedPalletCount);
        Assert.Equal(1, result.Value.OccupiedBoxCount);
        Assert.Equal(2, result.Value.RemainingPalletCapacity);
        Assert.Equal(2, result.Value.RemainingBoxCapacity);
    }

    [Fact]
    public async Task GetUnitDetailAsync_ExcludesCancelledPlacementsFromOccupancy()
    {
        var unit = new Unit(1, 1);
        var item = new Item("REF-1", "desc");
        _fixture.Context.Units.Add(unit);
        _fixture.Context.Items.Add(item);
        await _fixture.Context.SaveChangesAsync();

        var cancelled = new Placement(unit, item, PlacementClass.Pallet, Today);
        cancelled.Cancel();
        _fixture.Context.Placements.Add(cancelled);
        await _fixture.Context.SaveChangesAsync();

        var result = await _sut.GetUnitDetailAsync(unit.Id, CancellationToken.None);

        Assert.Equal(0, result.Value.OccupiedPalletCount);
        Assert.Equal(1, result.Value.RemainingPalletCapacity);
    }

    [Fact]
    public async Task GetUnitDetailAsync_ReturnsUnitNotFound_ForMissingUnit()
    {
        var result = await _sut.GetUnitDetailAsync(999, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("UNIT_NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task GetUnitAvailabilityAsync_ReturnsUnitNotFound_ForMissingUnit()
    {
        var result = await _sut.GetUnitAvailabilityAsync(999, Today, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("UNIT_NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task GetUnitAvailabilityAsync_ReportsLicenceNotCoveringDate_WhenNoLicenceInForce()
    {
        var unit = new Unit(1, 1);
        _fixture.Context.Units.Add(unit);
        await _fixture.Context.SaveChangesAsync();

        var result = await _sut.GetUnitAvailabilityAsync(unit.Id, Today, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Pallet.CanAccept);
        Assert.Contains("LICENCE_NOT_COVERING_DATE", result.Value.Pallet.Reasons);
        Assert.False(result.Value.Box.CanAccept);
        Assert.Contains("LICENCE_NOT_COVERING_DATE", result.Value.Box.Reasons);
    }

    [Fact]
    public async Task GetUnitAvailabilityAsync_CanAccept_WhenLicensedAndCapacityAvailable()
    {
        var unit = new Unit(1, 1);
        _fixture.Context.Units.Add(unit);
        await _fixture.Context.SaveChangesAsync();

        _fixture.Context.StorageLicences.Add(new StorageLicence(unit, "holder-1", Today.AddMonths(-1), 5));
        await _fixture.Context.SaveChangesAsync();

        var result = await _sut.GetUnitAvailabilityAsync(unit.Id, Today, CancellationToken.None);

        Assert.True(result.Value.Pallet.CanAccept);
        Assert.Empty(result.Value.Pallet.Reasons);
        Assert.True(result.Value.Box.CanAccept);
        Assert.Empty(result.Value.Box.Reasons);
    }

    [Fact]
    public async Task GetUnitAvailabilityAsync_OnlyEvaluatesLicenceCapacityAndSpacingReasons()
    {
        // Only requester/item/request-date-dependent rules (Rules 3, 5, 6) must never appear
        // here, since this endpoint has no requester/item/request-date context.
        var disallowedReasons = new[]
        {
            "NOT_CURRENT_HOLDER",
            "ITEM_INTAKE_DATE_MISSING",
            "ITEM_NOT_AVAILABLE_ON_DATE",
            "SCHEDULING_DATE_TOO_FAR"
        };

        var unit = new Unit(0, 0);
        _fixture.Context.Units.Add(unit);
        await _fixture.Context.SaveChangesAsync();

        var result = await _sut.GetUnitAvailabilityAsync(unit.Id, Today, CancellationToken.None);

        var allReasons = result.Value.Pallet.Reasons.Concat(result.Value.Box.Reasons);
        Assert.DoesNotContain(allReasons, r => disallowedReasons.Contains(r));
    }

    [Fact]
    public async Task GetUnitAvailabilityAsync_ReportsPalletCapacityExceeded_WhenFull()
    {
        var unit = new Unit(1, 1);
        var item = new Item("REF-1", "desc");
        _fixture.Context.Units.Add(unit);
        _fixture.Context.Items.Add(item);
        await _fixture.Context.SaveChangesAsync();

        _fixture.Context.StorageLicences.Add(new StorageLicence(unit, "holder-1", Today.AddMonths(-1), 5));
        _fixture.Context.Placements.Add(new Placement(unit, item, PlacementClass.Pallet, Today.AddDays(-30)));
        await _fixture.Context.SaveChangesAsync();

        var result = await _sut.GetUnitAvailabilityAsync(unit.Id, Today, CancellationToken.None);

        Assert.False(result.Value.Pallet.CanAccept);
        Assert.Contains("PALLET_CAPACITY_EXCEEDED", result.Value.Pallet.Reasons);
        // Box capacity is independent and should remain unaffected.
        Assert.True(result.Value.Box.CanAccept);
    }
}
