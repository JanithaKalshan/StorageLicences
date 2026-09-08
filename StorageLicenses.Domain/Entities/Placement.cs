using StorageLicenses.Domain.Enums;

namespace StorageLicenses.Domain.Entities;

/// <summary>
/// A scheduled/completed/cancelled placement of an item into a unit on a specific date.
/// Cancelled placements do not count toward capacity or pallet-spacing rules.
/// </summary>
public class Placement
{
    /// <summary>Reserved for EF Core materialization.</summary>
    protected Placement()
    {
    }

    public Placement(int unitId, int itemId, PlacementClass placementClass, DateOnly scheduledDate)
    {
        UnitId = unitId;
        ItemId = itemId;
        PlacementClass = placementClass;
        ScheduledDate = scheduledDate;
        Status = PlacementStatus.Scheduled;
    }

    /// <summary>
    /// Creates a placement linked via navigation properties, allowing EF Core to fix up the
    /// foreign keys even when <paramref name="unit"/> or <paramref name="item"/> have not yet
    /// been persisted.
    /// </summary>
    public Placement(Unit unit, Item item, PlacementClass placementClass, DateOnly scheduledDate)
        : this(unit.Id, item.Id, placementClass, scheduledDate)
    {
        Unit = unit;
        Item = item;
    }

    public int Id { get; private set; }

    public int UnitId { get; private set; }

    public Unit? Unit { get; private set; }

    public int ItemId { get; private set; }

    public Item? Item { get; private set; }

    public PlacementClass PlacementClass { get; private set; }

    public DateOnly ScheduledDate { get; private set; }

    public PlacementStatus Status { get; private set; }

    public void Complete() => Status = PlacementStatus.Completed;

    public void Cancel() => Status = PlacementStatus.Cancelled;

    /// <summary>
    /// True when this placement counts toward capacity/spacing rules, i.e. it is
    /// <see cref="PlacementStatus.Scheduled"/> or <see cref="PlacementStatus.Completed"/>.
    /// </summary>
    public bool CountsTowardCapacity => Status is PlacementStatus.Scheduled or PlacementStatus.Completed;
}
