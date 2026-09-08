using StorageLicenses.Domain.Common;

namespace StorageLicences.Application.Units;

/// <summary>
/// Read-side orchestration for unit listing, detail and availability. Implemented in
/// Infrastructure so EF Core/SQL concerns stay out of the Application and Domain layers.
/// </summary>
public interface IUnitsQueryService
{
    Task<PagedResult<UnitListItemDto>> GetUnitsAsync(UnitListQuery query, CancellationToken cancellationToken);

    Task<Result<UnitDetailDto>> GetUnitDetailAsync(int unitId, CancellationToken cancellationToken);

    Task<Result<UnitAvailabilityDto>> GetUnitAvailabilityAsync(int unitId, DateOnly asOf, CancellationToken cancellationToken);
}
