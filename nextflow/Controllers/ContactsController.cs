using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nextflow.Domain.Dtos.Contacts;
using Nextflow.Domain.Interfaces.Application.UseCases.Base;
using Nextflow.Domain.Models;
using Nextflow.Utils;

namespace Nextflow.Controllers;

[Authorize]
[Route("api/contacts")]
public class ContactsController(
    IGetAllUseCase<Contact, ContactResponseDto> getAllUseCase,
    ICreateUseCase<ContactRequestDto, ContactResponseDto> createUseCase,
    IUpdateUseCase<UpdateContactRequestDto, ContactResponseDto> updateUseCase,
    IDeleteUseCase<Contact> deleteUseCase) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int offset = 0, [FromQuery] int limit = 20, [FromQuery] string? filters = null,
        [FromQuery] string? sort = null, [FromQuery] string? order = null, CancellationToken ct = default)
        => Handle(await getAllUseCase.Execute(offset, limit, FilterHelper.Parse(filters), sort, order, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ContactRequestDto dto, CancellationToken ct)
        => HandleCreated(await createUseCase.Execute(dto, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateContactRequestDto dto, CancellationToken ct)
        => Handle(await updateUseCase.Execute(id, dto, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => HandleNoContent(await deleteUseCase.Execute(id, UserId, ct));
}
