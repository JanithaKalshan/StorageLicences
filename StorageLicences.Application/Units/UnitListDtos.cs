namespace StorageLicences.Application.Units;

/// <summary>
/// A single row in the paged units list, including occupancy/remaining capacity so the API can
/// project directly to DTOs in SQL without loading full entity graphs.
/// </summary>
public sealed record UnitListItemDto(
    int Id,
    int PalletCapacity,
    int BoxCapacity,
    int OccupiedPalletCount,
    int OccupiedBoxCount,
    int RemainingPalletCapacity,
    int RemainingBoxCapacity,
    bool HasActiveLicence);

/// <summary>
/// Server-side filter/paging options for GET /api/units.
/// </summary>
public sealed record UnitListQuery(
    int Page = 1,
    int PageSize = 50,
    bool? HasActiveLicence = null,
    int? MinRemainingPalletCapacity = null,
    int? MinRemainingBoxCapacity = null);

/// <summary>
/// Required response shape for GET /api/units.
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
