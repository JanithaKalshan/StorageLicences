using Microsoft.EntityFrameworkCore;
using StorageLicences.Application.Common;
using StorageLicences.Application.Placements;
using StorageLicenses.Domain.Common;
using StorageLicenses.Domain.Entities;
using StorageLicenses.Domain.Enums;
using StorageLicenses.Domain.Scheduling;
using StorageLicenses.Infastructure.Repositories;

namespace StorageLicenses.Infastructure.Services;

public sealed class PlacementsService(ApplicationDbContext context) : IPlacementsService
{
    private static readonly Error InvalidPlacementClass = new("INVALID_PLACEMENT_CLASS", "Placement class must be 'Pallet' or 'Box'.");

    public async Task<Result<PlacementDto>> ScheduleAsync(CreatePlacementRequest request, DateOnly requestDate, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<PlacementClass>(request.PlacementClass, ignoreCase: true, out var placementClass))
        {
            return Result.Failure<PlacementDto>(InvalidPlacementClass);
        }

        // Rule 1 — unit existence is handled here, before invoking the domain policy.
        var unit = await context.Units.SingleOrDefaultAsync(u => u.Id == request.UnitId, cancellationToken);
        if (unit is null)
            return Result.Failure<PlacementDto>(ApplicationErrors.UnitNotFound);

        var item = await context.Items.SingleOrDefaultAsync(i => i.Id == request.ItemId, cancellationToken);
        if (item is null)
            return Result.Failure<PlacementDto>(ApplicationErrors.ItemNotFound);

        var currentLicence = await context.StorageLicences
            .Where(l => l.UnitId == request.UnitId
                && l.GrantDate <= request.ScheduledDate
                && request.ScheduledDate < l.GrantDate.AddYears(l.TermYears))
            .SingleOrDefaultAsync(cancellationToken);

        var existingPlacements = await context.Placements
            .Where(p => p.UnitId == request.UnitId
                && (p.Status == PlacementStatus.Scheduled || p.Status == PlacementStatus.Completed))
            .ToListAsync(cancellationToken);

        var schedulingRequest = new PlacementSchedulingRequest
        {
            Unit = unit,
            Item = item,
            CurrentLicence = currentLicence,
            ExistingPlacements = existingPlacements,
            AssertedRequesterId = request.AssertedRequesterId,
            PlacementClass = placementClass,
            ScheduledDate = request.ScheduledDate,
            RequestDate = requestDate
        };

        var policyResult = PlacementSchedulingPolicy.Evaluate(schedulingRequest);
        if (policyResult.IsFailure)
            return Result.Failure<PlacementDto>(policyResult.Errors);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var placement = new Placement(unit, item, placementClass, request.ScheduledDate);
        context.Placements.Add(placement);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success(new PlacementDto(
            placement.Id,
            placement.UnitId,
            placement.ItemId,
            placement.PlacementClass.ToString(),
            placement.ScheduledDate,
            placement.Status.ToString()));
    }
}
