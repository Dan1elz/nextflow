using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.CalendarEvents;
using Nextflow.Domain.Interfaces.Application.UseCases.Base;
using Nextflow.Domain.Models;
using Nextflow.Utils;

namespace Nextflow.Controllers;

[Authorize]
[Route("api/calendar-events")]
public class CalendarEventsController(
    IGetAllUseCase<CalendarEvent, CalendarEventResponseDto> getAllUseCase,
    IGetByIdUseCase<CalendarEvent, CalendarEventResponseDto> getByIdUseCase,
    IGetOptionsUseCase<CalendarEvent, Option> getOptionsUseCase,
    ICreateUseCase<CalendarEventRequestDto, CalendarEventResponseDto> createUseCase,
    IUpdateUseCase<CalendarEventRequestDto, CalendarEventResponseDto> updateUseCase,
    IDeleteUseCase<CalendarEvent> deleteUseCase,
    IReactivateUseCase<CalendarEvent> reactivateUseCase) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int offset = 0, [FromQuery] int limit = 20, [FromQuery] string? filters = null,
        [FromQuery] string? sort = null, [FromQuery] string? order = null, CancellationToken ct = default)
        => Handle(await getAllUseCase.Execute(offset, limit, FilterHelper.Parse(filters), sort, order, ct));

    [HttpGet("options")]
    public async Task<IActionResult> Options([FromQuery] string? search = null, CancellationToken ct = default)
        => Handle(await getOptionsUseCase.Execute(search, null, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => Handle(await getByIdUseCase.Execute(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CalendarEventRequestDto dto, CancellationToken ct)
        => HandleCreated(await createUseCase.Execute(dto, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CalendarEventRequestDto dto, CancellationToken ct)
        => Handle(await updateUseCase.Execute(id, dto, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => HandleNoContent(await deleteUseCase.Execute(id, UserId, ct));

    [HttpPatch("{id:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct)
        => HandleNoContent(await reactivateUseCase.Execute(id, UserId, ct));
}
