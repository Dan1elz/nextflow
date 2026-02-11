using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Interfaces.UseCases;
using Nextflow.Domain.Interfaces.UseCases.Base;
using Nextflow.Domain.Models;
using Nextflow.Dtos;
using Nextflow.Utils;

namespace Nextflow.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ProductsController(
    ICreateUseCase<CreateProductDto, ProductResponseDto> createUseCase,
    IUpdateUseCase<UpdateProductDto, ProductResponseDto> updateUseCase,
    IDeleteUseCase<Product> deleteUseCase,
    IGetAllUseCase<Product, ProductResponseDto> getAllProductsUseCase,
    IGetByIdUseCase<ProductResponseDto> getProductByIdUseCase,
    IUpdateProductImageUseCase updateProductImageUseCase,
    IRemoveProductImageUseCase removeProductImageUseCase
) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromForm] ProductRequestDto request, CancellationToken ct)
    {
        var dto = new CreateProductDto
        {
            SupplierId = request.SupplierId,
            ProductCode = request.ProductCode,
            Name = request.Name,
            Description = request.Description,
            Image = request.Image != null ? new FormFileAdapter(request.Image) : null,
            CategoryIds = request.CategoryIds,
            Quantity = request.Quantity,
            UnitType = request.UnitType,
            Price = request.Price,
            Validity = request.Validity
        };
        var entity = await createUseCase.Execute(dto, ct);

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateProductRequestDto request, CancellationToken ct)
    {
        var dto = new UpdateProductDto
        {
            SupplierId = request.SupplierId,
            ProductCode = request.ProductCode,
            Name = request.Name,
            Description = request.Description,
            Image = null,
            CategoryIds = request.CategoryIds,
            Quantity = request.Quantity,
            UnitType = request.UnitType,
            Price = request.Price,
            Validity = request.Validity
        };

        return Ok(new ApiResponse<ProductResponseDto>
        {
            Status = 200,
            Message = "Produto atualizado com sucesso.",
            Data = await updateUseCase.Execute(id, dto, ct)
        });
    }

    [HttpPut("{id:guid}/image")]
    public async Task<IActionResult> UpdateImage([FromRoute] Guid id, IFormFile image, CancellationToken ct)
    {
        if (image == null)
            return BadRequest("Imagem é obrigatória.");
        var result = await updateProductImageUseCase.Execute(id, new FormFileAdapter(image), ct);
        return Ok(new ApiResponse<ProductResponseDto>
        {
            Status = 200,
            Message = "Imagem do produto atualizada com sucesso.",
            Data = result
        });
    }

    [HttpDelete("{id:guid}/image")]
    public async Task<IActionResult> RemoveImage([FromRoute] Guid id, CancellationToken ct)
    {
        var result = await removeProductImageUseCase.Execute(id, ct);
        return Ok(new ApiResponse<ProductResponseDto>
        {
            Status = 200,
            Message = "Imagem do produto removida com sucesso.",
            Data = result
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken ct)
    {
        await deleteUseCase.Execute(id, ct);

        return NoContent();
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int offset = 0, [FromQuery] int limit = 10, [FromQuery] string? filters = null, CancellationToken ct = default)
    {
        var filtersDict = FilterHelper.EnsureDefault(FilterHelper.Parse(filters), "isActive", "true");
        return Ok(new ApiResponse<ApiResponseTable<ProductResponseDto>>
        {
            Status = 200,
            Message = "Produtos encontrados com sucesso.",
            Data = await getAllProductsUseCase.Execute(offset, limit, filtersDict, ct)
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
    {
        return Ok(new ApiResponse<ProductResponseDto>
        {
            Status = 200,
            Message = "Produto encontrado com sucesso.",
            Data = await getProductByIdUseCase.Execute(id, ct)
        });
    }
}