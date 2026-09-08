namespace StorageLicenses.Domain.Entities;

/// <summary>
/// A physical storage unit that can hold pallet and/or box placements and is covered by
/// at most one storage licence at any point in time.
/// </summary>
public class Unit
{
    private readonly List<StorageLicence> _licences = new();
    private readonly List<Placement> _placements = new();

    /// <summary>Reserved for EF Core materialization.</summary>
    protected Unit()
    {
    }

    public Unit(int palletCapacity, int boxCapacity)
    {
        if (palletCapacity is < 0 or > 3)
            throw new ArgumentOutOfRangeException(nameof(palletCapacity), "Pallet capacity must be between 0 and 3.");

        if (boxCapacity is < 0 or > 8)
            throw new ArgumentOutOfRangeException(nameof(boxCapacity), "Box capacity must be between 0 and 8.");

        PalletCapacity = palletCapacity;
        BoxCapacity = boxCapacity;
    }

    public int Id { get; private set; }

    public int PalletCapacity { get; private set; }

    public int BoxCapacity { get; private set; }

    public IReadOnlyCollection<StorageLicence> Licences => _licences;

    public IReadOnlyCollection<Placement> Placements => _placements;

    /// <summary>
    /// Returns the licence (if any) in force for this unit on the given date.
    /// A unit must never have more than one licence in force at the same time.
    /// </summary>
    public StorageLicence? GetLicenceInForce(DateOnly onDate) =>
        _licences.SingleOrDefault(l => l.CoversDate(onDate));
}
