using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nextflow.Domain.Dtos.Stores;
using Nextflow.Domain.Interfaces.Application.UseCases.Base;
using Nextflow.Domain.Models;
using Nextflow.Utils;

namespace Nextflow.Controllers;

[Authorize]
[Route("api/stock-locations")]
public class StockLocationsController(
    IGetOptionsUseCase<StockLocation, LocationOptionDto> getOptionsUseCase,
    ICreateUseCase<LocationRequestDto, LocationResponseDto> createUseCase,
    IUpdateUseCase<UpdateLocationRequestDto, LocationResponseDto> updateUseCase,
    IDeleteUseCase<StockLocation> deleteUseCase,
    IReactivateUseCase<StockLocation> reactivateUseCase) : ApiController
{
    [HttpGet("options")]
    public async Task<IActionResult> Options([FromQuery] string? search = null, [FromQuery] string? filters = null, CancellationToken ct = default)
        => Handle(await getOptionsUseCase.Execute(search, FilterHelper.Parse(filters), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] LocationRequestDto dto, CancellationToken ct)
        => HandleCreated(await createUseCase.Execute(dto, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLocationRequestDto dto, CancellationToken ct)
        => Handle(await updateUseCase.Execute(id, dto, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => HandleNoContent(await deleteUseCase.Execute(id, UserId, ct));

    [HttpPatch("{id:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct)
        => HandleNoContent(await reactivateUseCase.Execute(id, UserId, ct));
}
