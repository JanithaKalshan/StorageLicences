using Microsoft.EntityFrameworkCore;
using StorageLicences.Application.Placements;
using StorageLicenses.Domain.Entities;
using StorageLicenses.Domain.Enums;
using StorageLicenses.Infastructure.Services;

namespace StorageLicences.Test.API;

public sealed class PlacementsServiceTests : IDisposable
{
    private readonly SqliteDbContextFixture _fixture = new();
    private readonly PlacementsService _sut;

    public PlacementsServiceTests()
    {
        _sut = new PlacementsService(_fixture.Context);
    }

    public void Dispose() => _fixture.Dispose();

    private static readonly DateOnly RequestDate = new(2024, 6, 1);

    private async Task<(Unit Unit, Item Item)> SeedLicensedUnitAndItemAsync(int palletCapacity = 1, int boxCapacity = 1)
    {
        var unit = new Unit(palletCapacity, boxCapacity);
        var item = new Item("REF-1", "desc", RequestDate.AddYears(-1));
        _fixture.Context.Units.Add(unit);
        _fixture.Context.Items.Add(item);
        await _fixture.Context.SaveChangesAsync();

        _fixture.Context.StorageLicences.Add(new StorageLicence(unit, "holder-1", RequestDate.AddMonths(-1), 5));
        await _fixture.Context.SaveChangesAsync();

        return (unit, item);
    }

    [Fact]
    public async Task ScheduleAsync_PersistsPlacement_WhenAllRulesPass()
    {
        var (unit, item) = await SeedLicensedUnitAndItemAsync();

        var request = new CreatePlacementRequest(unit.Id, item.Id, "Pallet", RequestDate, "holder-1");
        var result = await _sut.ScheduleAsync(request, RequestDate, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Pallet", result.Value.PlacementClass);
        Assert.Equal("Scheduled", result.Value.Status);

        var persisted = await _fixture.Context.Placements.SingleAsync<Placement>();
        Assert.Equal(unit.Id, persisted.UnitId);
        Assert.Equal(item.Id, persisted.ItemId);
    }

    [Fact]
    public async Task ScheduleAsync_ReturnsUnitNotFound_WhenUnitMissing()
    {
        var item = new Item("REF-1", "desc", RequestDate.AddYears(-1));
        _fixture.Context.Items.Add(item);
        await _fixture.Context.SaveChangesAsync();

        var request = new CreatePlacementRequest(999, item.Id, "Pallet", RequestDate, "holder-1");
        var result = await _sut.ScheduleAsync(request, RequestDate, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("UNIT_NOT_FOUND", result.Error.Code);
        Assert.Empty(_fixture.Context.Placements);
    }

    [Fact]
    public async Task ScheduleAsync_ReturnsItemNotFound_WhenItemMissing()
    {
        var unit = new Unit(1, 1);
        _fixture.Context.Units.Add(unit);
        await _fixture.Context.SaveChangesAsync();

        var request = new CreatePlacementRequest(unit.Id, 999, "Pallet", RequestDate, "holder-1");
        var result = await _sut.ScheduleAsync(request, RequestDate, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("ITEM_NOT_FOUND", result.Error.Code);
        Assert.Empty(_fixture.Context.Placements);
    }

    [Fact]
    public async Task ScheduleAsync_ReturnsInvalidPlacementClass_ForUnrecognizedClass()
    {
        var (unit, item) = await SeedLicensedUnitAndItemAsync();

        var request = new CreatePlacementRequest(unit.Id, item.Id, "Crate", RequestDate, "holder-1");
        var result = await _sut.ScheduleAsync(request, RequestDate, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("INVALID_PLACEMENT_CLASS", result.Error.Code);
        Assert.Empty(_fixture.Context.Placements);
    }

    [Fact]
    public async Task ScheduleAsync_FailsAndDoesNotPersist_WhenNoLicenceCoversDate()
    {
        var unit = new Unit(1, 1);
        var item = new Item("REF-1", "desc", RequestDate.AddYears(-1));
        _fixture.Context.Units.Add(unit);
        _fixture.Context.Items.Add(item);
        await _fixture.Context.SaveChangesAsync();

        var request = new CreatePlacementRequest(unit.Id, item.Id, "Pallet", RequestDate, "holder-1");
        var result = await _sut.ScheduleAsync(request, RequestDate, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, e => e.Code == "LICENCE_NOT_COVERING_DATE");
        Assert.Empty(_fixture.Context.Placements);
    }

    [Fact]
    public async Task ScheduleAsync_ReturnsCombinedFailures_WhenMultipleRulesViolated()
    {
        // Wrong holder AND capacity full simultaneously should surface both error codes together.
        var (unit, item) = await SeedLicensedUnitAndItemAsync(palletCapacity: 1);
        var otherItem = new Item("REF-2", "desc", RequestDate.AddYears(-1));
        _fixture.Context.Items.Add(otherItem);
        await _fixture.Context.SaveChangesAsync();

        _fixture.Context.Placements.Add(new Placement(unit, item, PlacementClass.Pallet, RequestDate.AddDays(-1)));
        await _fixture.Context.SaveChangesAsync();

        var request = new CreatePlacementRequest(unit.Id, otherItem.Id, "Pallet", RequestDate, "wrong-holder");
        var result = await _sut.ScheduleAsync(request, RequestDate, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, e => e.Code == "NOT_CURRENT_HOLDER");
        Assert.Contains(result.Errors, e => e.Code == "PALLET_CAPACITY_EXCEEDED");
        Assert.Contains(result.Errors, e => e.Code == "PALLET_GAP_TOO_SMALL");
        Assert.Single(await _fixture.Context.Placements.ToListAsync<Placement>());
    }
}
