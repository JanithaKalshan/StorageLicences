using Microsoft.AspNetCore.Mvc;
using StorageLicences.Application.Placements;
using StorageLicences.Application.Units;
using StorageLicences.API.Controllers;
using StorageLicenses.Domain.Common;

namespace StorageLicences.Test.API;

/// <summary>Minimal hand-written fake; no mocking library is referenced by this project.</summary>
public sealed class FakeUnitsQueryService : IUnitsQueryService
{
    public PagedResult<UnitListItemDto>? UnitsResult { get; set; }
    public Result<UnitDetailDto>? DetailResult { get; set; }
    public Result<UnitAvailabilityDto>? AvailabilityResult { get; set; }

    public Task<PagedResult<UnitListItemDto>> GetUnitsAsync(UnitListQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(UnitsResult!);

    public Task<Result<UnitDetailDto>> GetUnitDetailAsync(int unitId, CancellationToken cancellationToken) =>
        Task.FromResult(DetailResult!);

    public Task<Result<UnitAvailabilityDto>> GetUnitAvailabilityAsync(int unitId, DateOnly asOf, CancellationToken cancellationToken) =>
        Task.FromResult(AvailabilityResult!);
}

public sealed class UnitsControllerTests
{
    [Fact]
    public async Task GetUnits_ReturnsOk_WithPagedResult()
    {
        var paged = new PagedResult<UnitListItemDto>(
            [new UnitListItemDto(1, 1, 1, 0, 0, 1, 1, true)], 1, 1, 50);
        var fake = new FakeUnitsQueryService { UnitsResult = paged };
        var controller = new UnitsController(fake);

        var response = await controller.GetUnits();

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(paged, ok.Value);
    }

    [Fact]
    public async Task GetUnitDetail_ReturnsOk_WhenFound()
    {
        var detail = new UnitDetailDto(1, 1, 1, 0, 0, 1, 1, [], []);
        var fake = new FakeUnitsQueryService { DetailResult = Result.Success(detail) };
        var controller = new UnitsController(fake);

        var response = await controller.GetUnitDetail(1, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(detail, ok.Value);
    }

    [Fact]
    public async Task GetUnitDetail_ReturnsNotFound_WhenMissing()
    {
        var fake = new FakeUnitsQueryService
        {
            DetailResult = Result.Failure<UnitDetailDto>(StorageLicences.Application.Common.ApplicationErrors.UnitNotFound)
        };
        var controller = new UnitsController(fake);

        var response = await controller.GetUnitDetail(999, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(response.Result);
    }

    [Fact]
    public async Task GetUnitAvailability_ReturnsOk_WhenFound()
    {
        var availability = new UnitAvailabilityDto(1, new DateOnly(2024, 6, 1),
            new ClassAvailabilityDto(true, []), new ClassAvailabilityDto(true, []));
        var fake = new FakeUnitsQueryService { AvailabilityResult = Result.Success(availability) };
        var controller = new UnitsController(fake);

        var response = await controller.GetUnitAvailability(1, new DateOnly(2024, 6, 1), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(availability, ok.Value);
    }

    [Fact]
    public async Task GetUnitAvailability_ReturnsNotFound_WhenUnitMissing()
    {
        var fake = new FakeUnitsQueryService
        {
            AvailabilityResult = Result.Failure<UnitAvailabilityDto>(StorageLicences.Application.Common.ApplicationErrors.UnitNotFound)
        };
        var controller = new UnitsController(fake);

        var response = await controller.GetUnitAvailability(999, new DateOnly(2024, 6, 1), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(response.Result);
    }
}

public sealed class FakePlacementsService : IPlacementsService
{
    public Result<PlacementDto>? Result { get; set; }
    public CreatePlacementRequest? ReceivedRequest { get; private set; }
    public DateOnly ReceivedRequestDate { get; private set; }

    public Task<Result<PlacementDto>> ScheduleAsync(CreatePlacementRequest request, DateOnly requestDate, CancellationToken cancellationToken)
    {
        ReceivedRequest = request;
        ReceivedRequestDate = requestDate;
        return Task.FromResult(Result!);
    }
}

public sealed class PlacementsControllerTests
{
    [Fact]
    public async Task CreatePlacement_ReturnsCreated_WhenSchedulingSucceeds()
    {
        var dto = new PlacementDto(1, 1, 1, "Pallet", new DateOnly(2024, 6, 1), "Scheduled");
        var fake = new FakePlacementsService { Result = Result.Success(dto) };
        var controller = new PlacementsController(fake);

        var request = new CreatePlacementRequest(1, 1, "Pallet", new DateOnly(2024, 6, 1), "holder-1");
        var response = await controller.CreatePlacement(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(response.Result);
        Assert.Same(dto, created.Value);
        Assert.Equal(1, ((dynamic)created.RouteValues!["id"]!));
    }

    [Fact]
    public async Task CreatePlacement_ReturnsBadRequest_WhenValidationFails()
    {
        var fake = new FakePlacementsService
        {
            Result = StorageLicenses.Domain.Common.Result.Failure<PlacementDto>(
                StorageLicenses.Domain.Scheduling.SchedulingErrors.LicenceNotCoveringDate)
        };
        var controller = new PlacementsController(fake);

        var request = new CreatePlacementRequest(1, 1, "Pallet", new DateOnly(2024, 6, 1), "holder-1");
        var response = await controller.CreatePlacement(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response.Result);
    }
}
