namespace StorageLicences.Application.Units;

/// <summary>
/// Availability of a single placement class (pallet or box) for a unit as of a given date.
/// </summary>
public sealed record ClassAvailabilityDto(bool CanAccept, IReadOnlyList<string> Reasons);

/// <summary>
/// Response for GET /api/units/{id}/availability?asOf={date}.
///
/// Only rules that can be meaningfully evaluated without requester/item/request-date context are
/// applied: unit existence, licence validity for <c>asOf</c>, capacity, and pallet spacing.
/// Requester identity, item intake date, and the 24-month scheduling window require information
/// this endpoint does not have and are therefore never evaluated here.
/// </summary>
public sealed record UnitAvailabilityDto(
    int UnitId,
    DateOnly AsOf,
    ClassAvailabilityDto Pallet,
    ClassAvailabilityDto Box);
