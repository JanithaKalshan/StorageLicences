using Microsoft.AspNetCore.Mvc;
using StorageLicences.Application.Placements;

namespace StorageLicences.API.Controllers;

[ApiController]
[Route("api/placements")]
public sealed class PlacementsController(IPlacementsService placementsService) : ControllerBase
{
    /// <summary>
    /// POST /api/placements — schedules a placement after validating all seven Domain
    /// scheduling rules; persistence only occurs when validation succeeds.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PlacementDto>> CreatePlacement(
        [FromBody] CreatePlacementRequest request,
        CancellationToken cancellationToken)
    {
        var requestDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await placementsService.ScheduleAsync(request, requestDate, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new { errors = result.Errors });

        return CreatedAtAction(nameof(CreatePlacement), new { id = result.Value.Id }, result.Value);
    }
}
