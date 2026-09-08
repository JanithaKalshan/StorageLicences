namespace StorageLicences.Application.Units;

public sealed record LicenceSummaryDto(
    int Id,
    string HolderId,
    DateOnly GrantDate,
    int TermYears,
    DateOnly CoverageEndDateExclusive,
    DateOnly? SurrenderedDate,
    bool IsCurrentlyInForce);

public sealed record PlacementSummaryDto(
    int Id,
    int ItemId,
    string ItemReference,
    string PlacementClass,
    DateOnly ScheduledDate,
    string Status);

/// <summary>
/// Full detail for GET /api/units/{id}: capacities, licence history and all placements.
/// </summary>
public sealed record UnitDetailDto(
    int Id,
    int PalletCapacity,
    int BoxCapacity,
    IReadOnlyList<LicenceSummaryDto> Licences,
    IReadOnlyList<PlacementSummaryDto> Placements);
