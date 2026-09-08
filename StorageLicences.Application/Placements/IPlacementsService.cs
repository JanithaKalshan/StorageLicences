using StorageLicenses.Domain.Common;

namespace StorageLicences.Application.Placements;

/// <summary>
/// Orchestrates placement scheduling: loads the required domain state, evaluates the
/// <c>PlacementSchedulingPolicy</c>, and persists the placement transactionally only when the
/// policy succeeds. Implemented in Infrastructure so EF Core concerns stay out of Application
/// and Domain.
/// </summary>
public interface IPlacementsService
{
    Task<Result<PlacementDto>> ScheduleAsync(CreatePlacementRequest request, DateOnly requestDate, CancellationToken cancellationToken);
}
