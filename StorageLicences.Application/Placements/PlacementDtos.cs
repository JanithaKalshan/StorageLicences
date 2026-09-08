namespace StorageLicences.Application.Placements;

/// <summary>
/// Request payload for POST /api/placements.
/// </summary>
public sealed record CreatePlacementRequest(
    int UnitId,
    int ItemId,
    string PlacementClass,
    DateOnly ScheduledDate,
    string AssertedRequesterId);

/// <summary>
/// Response payload once a placement has been successfully scheduled and persisted.
/// </summary>
public sealed record PlacementDto(
    int Id,
    int UnitId,
    int ItemId,
    string PlacementClass,
    DateOnly ScheduledDate,
    string Status);
