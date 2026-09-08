using Microsoft.AspNetCore.Mvc;
using StorageLicences.Application.Units;

namespace StorageLicences.API.Controllers;

[ApiController]
[Route("api/units")]
public sealed class UnitsController(IUnitsQueryService unitsQueryService) : ControllerBase
{
    /// <summary>
    /// GET /api/units — server-side filtered/paged list of units with occupancy and remaining capacity.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<UnitListItemDto>>> GetUnits(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] bool? hasActiveLicence = null,
        [FromQuery] int? minRemainingPalletCapacity = null,
        [FromQuery] int? minRemainingBoxCapacity = null,
        CancellationToken cancellationToken = default)
    {
        var query = new UnitListQuery(page, pageSize, hasActiveLicence, minRemainingPalletCapacity, minRemainingBoxCapacity);
        var result = await unitsQueryService.GetUnitsAsync(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/units/{id} — unit detail including licence history and all placements.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<UnitDetailDto>> GetUnitDetail(int id, CancellationToken cancellationToken)
    {
        var result = await unitsQueryService.GetUnitDetailAsync(id, cancellationToken);

        if (result.IsFailure)
            return NotFound(new { errors = result.Errors });

        return Ok(result.Value);
    }

    /// <summary>
    /// GET /api/units/{id}/availability?asOf={date} — whether the unit can accept a pallet/box
    /// as of the given date, based only on rules meaningfully evaluable without
    /// requester/item/request-date context (licence validity, capacity, pallet spacing).
    /// </summary>
    [HttpGet("{id:int}/availability")]
    public async Task<ActionResult<UnitAvailabilityDto>> GetUnitAvailability(int id, [FromQuery] DateOnly asOf, CancellationToken cancellationToken)
    {
        var result = await unitsQueryService.GetUnitAvailabilityAsync(id, asOf, cancellationToken);

        if (result.IsFailure)
            return NotFound(new { errors = result.Errors });

        return Ok(result.Value);
    }
}
