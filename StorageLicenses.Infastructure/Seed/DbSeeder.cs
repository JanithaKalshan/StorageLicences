using Microsoft.EntityFrameworkCore;
using StorageLicenses.Domain.Entities;
using StorageLicenses.Domain.Enums;
using StorageLicenses.Infastructure.Repositories;

namespace StorageLicenses.Infastructure.Seed;

/// <summary>
/// Deterministically seeds the database with a realistic dataset (units, items, licences and
/// placements) large enough to exercise scheduling rules and query performance.
/// </summary>
public static class DbSeeder
{
    private const int UnitCount = 5_000;
    private const int Seed = 42;

    public static async Task SeedAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        if (await context.Units.AnyAsync(cancellationToken))
            return;

        var today = new DateOnly(2026, 1, 1);

        var units = new List<Unit>(UnitCount);
        var items = new List<Item>();
        var licences = new List<StorageLicence>();
        var placements = new List<Placement>();

        for (var i = 0; i < UnitCount; i++)
        {
            var random = new Random(Seed + i);

            var (palletCapacity, boxCapacity) = PickCapacities(i, random);
            var unit = new Unit(palletCapacity, boxCapacity);
            units.Add(unit);

            var licence = CreateLicenceForUnit(unit, i, random, today);
            if (licence is not null)
                licences.Add(licence);

            CreatePlacementsForUnit(unit, licence, i, random, today, items, placements);
        }

        context.Units.AddRange(units);
        context.StorageLicences.AddRange(licences);
        context.Items.AddRange(items);
        context.Placements.AddRange(placements);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static (int PalletCapacity, int BoxCapacity) PickCapacities(int index, Random random)
    {
        // Ensure some units have zero capacity for a class, and a spread across allowed ranges.
        return (index % 20) switch
        {
            0 => (0, random.Next(1, 9)),   // no pallet capacity
            1 => (random.Next(1, 4), 0),   // no box capacity
            _ => (random.Next(0, 4), random.Next(0, 9))
        };
    }

    private static StorageLicence? CreateLicenceForUnit(Unit unit, int index, Random random, DateOnly today)
    {
        // Roughly 1 in 10 units has no licence at all.
        if (index % 10 == 0)
            return null;

        var holderId = $"COMPANY-{index % 500:D4}";
        var termYears = random.Next(1, 11);
        var grantDate = today.AddYears(-random.Next(0, 12)).AddMonths(-random.Next(0, 12));

        switch (index % 15)
        {
            case 1:
                // Licence close to lapse: term ends within the next couple of months.
                grantDate = today.AddMonths(-((termYears * 12) - 2));
                return new StorageLicence(unit, holderId, grantDate, termYears);
            case 2:
                // Lapsed licence: term already ended in the past.
                grantDate = today.AddYears(-(termYears + 2));
                return new StorageLicence(unit, holderId, grantDate, termYears);
            case 3:
                // Surrendered/historical licence.
                var licence = new StorageLicence(unit, holderId, grantDate, termYears);
                var surrenderDate = grantDate.AddMonths(random.Next(1, Math.Max(2, termYears * 12 - 1)));
                if (surrenderDate < licence.CoverageEndDateExclusive)
                {
                    // Use Result-based surrender; ignore result during seeding.
                    _ = licence.Surrender(surrenderDate);
                }
                return licence;
            default:
                return new StorageLicence(unit, holderId, grantDate, termYears);
        }
    }

    private static void CreatePlacementsForUnit(
        Unit unit,
        StorageLicence? licence,
        int index,
        Random random,
        DateOnly today,
        List<Item> items,
        List<Placement> placements)
    {
        if (licence is null || !licence.CoversDate(today))
            return;

        // Recent pallet placement, used to exercise pallet-spacing scenarios.
        if (unit.PalletCapacity > 0)
        {
            var item = CreateItem(items, index, "PLT", random, today);
            var scheduledDate = today.AddMonths(-random.Next(0, 6));
            var placement = new Placement(unit, item, PlacementClass.Pallet, scheduledDate);
            ApplyStatus(placement, PickStatus(index, random));
            placements.Add(placement);
        }

        var boxPlacementsToCreate = Math.Min(unit.BoxCapacity, index % 4);
        for (var b = 0; b < boxPlacementsToCreate; b++)
        {
            var item = CreateItem(items, index * 10 + b, $"BOX{b}", random, today);
            var scheduledDate = today.AddDays(-random.Next(0, 400));
            var placement = new Placement(unit, item, PlacementClass.Box, scheduledDate);
            ApplyStatus(placement, PickStatus(index + b, random));
            placements.Add(placement);
        }
    }

    private static Item CreateItem(List<Item> items, int index, string prefix, Random random, DateOnly today)
    {
        // Roughly 1 in 8 items has no recorded intake date.
        DateOnly? intakeDate = index % 8 == 0
            ? null
            : today.AddDays(-random.Next(0, 800));

        var item = new Item($"{prefix}-{index:D6}", $"Seeded item {prefix} #{index}", intakeDate);
        items.Add(item);
        return item;
    }

    private static PlacementStatus PickStatus(int index, Random random) => (index % 9) switch
    {
        0 => PlacementStatus.Cancelled,
        1 or 2 => PlacementStatus.Completed,
        _ => PlacementStatus.Scheduled
    };

    private static void ApplyStatus(Placement placement, PlacementStatus status)
    {
        switch (status)
        {
            case PlacementStatus.Completed:
                placement.Complete();
                break;
            case PlacementStatus.Cancelled:
                placement.Cancel();
                break;
        }
    }
}
