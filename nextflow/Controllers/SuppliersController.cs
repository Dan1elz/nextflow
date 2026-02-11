using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nextflow.Attributes;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.UseCases.Base;
using Nextflow.Domain.Models;
using Nextflow.Utils;

namespace Nextflow.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class SuppliersController(
    ICreateUseCase<CreateSupplierDto, SupplierResponseDto> createUseCase,
    IUpdateUseCase<UpdateSupplierDto, SupplierResponseDto> updateUseCase,
    IDeleteUseCase<Supplier> deleteUseCase,
    IGetAllUseCase<Supplier, SupplierResponseDto> getAllSuppliersUseCase,
    IGetByIdUseCase<SupplierResponseDto> getSupplierByIdUseCase
) : ControllerBase
{
    [HttpPost]
    [RoleAuthorize(RoleEnum.Admin)]
    public async Task<IActionResult> Create([FromBody] CreateSupplierDto? dto, CancellationToken ct)
    {
        if (dto is null)
            return BadRequest("O corpo da requisição é obrigatório. Envie um JSON com Name e CNPJ.");
        var createdSupplier = await createUseCase.Execute(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = createdSupplier.Id }, createdSupplier);
    }

    [HttpPut("{id:guid}")]
    [RoleAuthorize(RoleEnum.Admin)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateSupplierDto dto, CancellationToken ct)
    {
        return Ok(new ApiResponse<SupplierResponseDto>
        {
            Status = 200,
            Message = "Fornecedor atualizado com sucesso.",
            Data = await updateUseCase.Execute(id, dto, ct)
        });
    }

    [HttpDelete("{id:guid}")]
    [RoleAuthorize(RoleEnum.Admin)]
    public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken ct)
    {
        await deleteUseCase.Execute(id, ct);

        return NoContent();
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int offset = 0, [FromQuery] int limit = 10, [FromQuery] string? filters = null, CancellationToken ct = default)
    {
        var filtersDict = FilterHelper.EnsureDefault(FilterHelper.Parse(filters), "isActive", "true");
        return Ok(new ApiResponse<ApiResponseTable<SupplierResponseDto>>
        {
            Status = 200,
            Message = "Fornecedor encontrados com sucesso.",
            Data = await getAllSuppliersUseCase.Execute(offset, limit, filtersDict, ct)
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
    {
        return Ok(new ApiResponse<SupplierResponseDto>
        {
            Status = 200,
            Message = "Fornecedor encontrado com sucesso.",
            Data = await getSupplierByIdUseCase.Execute(id, ct)
        });
    }
}


