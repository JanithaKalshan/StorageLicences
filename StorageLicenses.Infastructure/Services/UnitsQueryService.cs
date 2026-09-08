using Microsoft.EntityFrameworkCore;
using StorageLicences.Application.Common;
using StorageLicences.Application.Units;
using StorageLicenses.Domain.Common;
using StorageLicenses.Domain.Enums;
using StorageLicenses.Infastructure.Repositories;

namespace StorageLicenses.Infastructure.Services;

public sealed class UnitsQueryService(ApplicationDbContext context) : IUnitsQueryService
{
    public async Task<PagedResult<UnitListItemDto>> GetUnitsAsync(UnitListQuery query, CancellationToken cancellationToken)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 50 : query.PageSize;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 1. Project into an ANONYMOUS TYPE first. 
        // This allows EF Core to track the computed expressions and map them to SQL aliases.
        var baseQuery = context.Units
            .AsNoTracking()
            .Select(u => new
            {
                u.Id,
                u.PalletCapacity,
                u.BoxCapacity,
                OccupiedPallets = u.Placements.Count(p => p.PlacementClass == PlacementClass.Pallet
                    && (p.Status == PlacementStatus.Scheduled || p.Status == PlacementStatus.Completed)),
                OccupiedBoxes = u.Placements.Count(p => p.PlacementClass == PlacementClass.Box
                    && (p.Status == PlacementStatus.Scheduled || p.Status == PlacementStatus.Completed)),
                HasActiveLicence = u.Licences.Any(l => l.GrantDate <= today
                    && today < l.GrantDate.AddYears(l.TermYears)
                    && (l.SurrenderedDate == null || today < l.SurrenderedDate))
            });

        // 2. Apply dynamic filters to the anonymous properties
        if (query.HasActiveLicence.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.HasActiveLicence == query.HasActiveLicence.Value);
        }

        if (query.MinRemainingPalletCapacity.HasValue)
        {
            baseQuery = baseQuery.Where(x => (x.PalletCapacity - x.OccupiedPallets) >= query.MinRemainingPalletCapacity.Value);
        }

        if (query.MinRemainingBoxCapacity.HasValue)
        {
            baseQuery = baseQuery.Where(x => (x.BoxCapacity - x.OccupiedBoxes) >= query.MinRemainingBoxCapacity.Value);
        }

        // 3. Get the total count for pagination based on the filtered SQL query
        var totalCount = await baseQuery.CountAsync(cancellationToken);

        // 4. Apply pagination and finally project to your C# record/DTO using the constructor
        var items = await baseQuery
            .OrderBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new UnitListItemDto(
                x.Id,
                x.PalletCapacity,
                x.BoxCapacity,
                x.OccupiedPallets,
                x.OccupiedBoxes,
                x.PalletCapacity - x.OccupiedPallets, // Calculate remaining pallet capacity
                x.BoxCapacity - x.OccupiedBoxes,      // Calculate remaining box capacity
                x.HasActiveLicence
            ))
            .ToListAsync(cancellationToken);

        return new PagedResult<UnitListItemDto>(items, totalCount, page, pageSize);
    }

    public async Task<Result<UnitDetailDto>> GetUnitDetailAsync(int unitId, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var dto = await context.Units
            .AsNoTracking()
            .Where(u => u.Id == unitId)
            .Select(u => new UnitDetailDto(
                u.Id,
                u.PalletCapacity,
                u.BoxCapacity,
                u.Licences
                    .OrderByDescending(l => l.GrantDate)
                    .Select(l => new LicenceSummaryDto(
                        l.Id,
                        l.HolderId,
                        l.GrantDate,
                        l.TermYears,
                        l.GrantDate.AddYears(l.TermYears),
                        l.SurrenderedDate,
                        l.GrantDate <= today && today < l.GrantDate.AddYears(l.TermYears)
                            && (l.SurrenderedDate == null || today < l.SurrenderedDate)))
                    .ToList(),
                u.Placements
                    .OrderByDescending(p => p.ScheduledDate)
                    .Select(p => new PlacementSummaryDto(
                        p.Id,
                        p.ItemId,
                        p.Item!.Reference,
                        p.PlacementClass.ToString(),
                        p.ScheduledDate,
                        p.Status.ToString()))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<UnitDetailDto>(ApplicationErrors.UnitNotFound)
            : Result.Success(dto);
    }

    public async Task<Result<UnitAvailabilityDto>> GetUnitAvailabilityAsync(int unitId, DateOnly asOf, CancellationToken cancellationToken)
    {
        var unit = await context.Units
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == unitId, cancellationToken);

        if (unit is null)
            return Result.Failure<UnitAvailabilityDto>(ApplicationErrors.UnitNotFound);

        var licence = await context.StorageLicences
            .AsNoTracking()
            .Where(l => l.UnitId == unitId
                && l.GrantDate <= asOf
                && asOf < l.GrantDate.AddYears(l.TermYears))
            .SingleOrDefaultAsync(cancellationToken);

        var relevantPlacements = await context.Placements
            .AsNoTracking()
            .Where(p => p.UnitId == unitId
                && (p.Status == PlacementStatus.Scheduled || p.Status == PlacementStatus.Completed))
            .ToListAsync(cancellationToken);

        var pallet = EvaluateClassAvailability(unit, licence, relevantPlacements, PlacementClass.Pallet, asOf);
        var box = EvaluateClassAvailability(unit, licence, relevantPlacements, PlacementClass.Box, asOf);

        return Result.Success(new UnitAvailabilityDto(unitId, asOf, pallet, box));
    }

    private static ClassAvailabilityDto EvaluateClassAvailability(
        Domain.Entities.Unit unit,
        Domain.Entities.StorageLicence? licence,
        IReadOnlyCollection<Domain.Entities.Placement> existingPlacements,
        PlacementClass placementClass,
        DateOnly asOf)
    {
        var reasons = new List<string>();

        var licenceErrors = Domain.Scheduling.PlacementSchedulingPolicy.CheckLicenceCoverage(licence, unit.Id, asOf);
        reasons.AddRange(licenceErrors.Select(e => e.Code));

        var capacityErrors = Domain.Scheduling.PlacementSchedulingPolicy.CheckCapacity(unit, existingPlacements, placementClass);
        reasons.AddRange(capacityErrors.Select(e => e.Code));

        var spacingErrors = Domain.Scheduling.PlacementSchedulingPolicy.CheckPalletSpacing(existingPlacements, placementClass, asOf);
        reasons.AddRange(spacingErrors.Select(e => e.Code));

        return new ClassAvailabilityDto(reasons.Count == 0, reasons);
    }
}
