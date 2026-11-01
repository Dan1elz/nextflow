using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nextflow.Domain.Dtos.Categories;
using Nextflow.Domain.Interfaces.Application.UseCases;
using Nextflow.Domain.Interfaces.Application.UseCases.Base;
using Nextflow.Domain.Models;
using Nextflow.Utils;

namespace Nextflow.Controllers;

[Authorize]
[Route("api/categories")]
public class CategoriesController(
    IGetCategoryTreeUseCase getTreeUseCase,
    IGetByIdUseCase<Category, CategoryResponseDto> getByIdUseCase,
    IGetOptionsUseCase<Category, CategoryOptionDto> getOptionsUseCase,
    ICreateUseCase<CategoryRequestDto, CategoryResponseDto> createUseCase,
    IUpdateUseCase<CategoryRequestDto, CategoryResponseDto> updateUseCase,
    IDeleteCategoryUseCase deleteUseCase,
    IReactivateUseCase<Category> reactivateUseCase) : ApiController
{
    [HttpGet("tree")]
    public async Task<IActionResult> Tree(CancellationToken ct)
        => Handle(await getTreeUseCase.Execute(ct));

    [HttpGet("options")]
    public async Task<IActionResult> Options([FromQuery] string? search = null, [FromQuery] string? filters = null, CancellationToken ct = default)
        => Handle(await getOptionsUseCase.Execute(search, FilterHelper.Parse(filters), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => Handle(await getByIdUseCase.Execute(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CategoryRequestDto dto, CancellationToken ct)
        => HandleCreated(await createUseCase.Execute(dto, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CategoryRequestDto dto, CancellationToken ct)
        => Handle(await updateUseCase.Execute(id, dto, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] bool confirmWarnings = false, CancellationToken ct = default)
        => HandleNoContent(await deleteUseCase.Execute(id, confirmWarnings, UserId, ct));

    [HttpPatch("{id:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct)
        => HandleNoContent(await reactivateUseCase.Execute(id, UserId, ct));
}
