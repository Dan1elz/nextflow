using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Products;
using Nextflow.Domain.Interfaces.Application.UseCases;
using Nextflow.Domain.Interfaces.Application.UseCases.Base;
using Nextflow.Domain.Models;
using Nextflow.Utils;

namespace Nextflow.Controllers;

[Authorize]
[Route("api/products")]
public class ProductsController(
    IGetAllUseCase<Product, ProductListItemDto> getAllUseCase,
    IGetByIdUseCase<Product, ProductResponseDto> getByIdUseCase,
    IGetByIdUseCase<Product, List<ProductVariantResponseDto>> getVariantsUseCase,
    IGetProductFormUseCase getFormUseCase,
    ICreateUseCase<ProductRequestDto, ProductResponseDto> createUseCase,
    IUpdateUseCase<ProductRequestDto, ProductResponseDto> updateUseCase,
    IDeleteUseCase<Product> deleteUseCase,
    IReactivateUseCase<Product> reactivateUseCase,
    IUpdateProductLifecycleUseCase updateLifecycleUseCase,
    IUploadProductImageUseCase uploadImageUseCase,
    IRemoveProductImageUseCase removeImageUseCase) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int offset = 0, [FromQuery] int limit = 20, [FromQuery] string? filters = null,
        [FromQuery] string? sort = null, [FromQuery] string? order = null, CancellationToken ct = default)
        => Handle(await getAllUseCase.Execute(offset, limit, FilterHelper.Parse(filters), sort, order, ct));

    [HttpGet("form")]
    public async Task<IActionResult> NewForm(CancellationToken ct)
        => Handle(await getFormUseCase.Execute(null, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => Handle(await getByIdUseCase.Execute(id, ct));

    [HttpGet("{id:guid}/form")]
    public async Task<IActionResult> Form(Guid id, CancellationToken ct)
        => Handle(await getFormUseCase.Execute(id, ct));

    [HttpGet("{id:guid}/variants")]
    public async Task<IActionResult> Variants(Guid id, CancellationToken ct)
        => Handle(await getVariantsUseCase.Execute(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ProductRequestDto dto, CancellationToken ct)
        => HandleCreated(await createUseCase.Execute(dto, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ProductRequestDto dto, CancellationToken ct)
        => Handle(await updateUseCase.Execute(id, dto, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => HandleNoContent(await deleteUseCase.Execute(id, UserId, ct));

    [HttpPatch("{id:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct)
        => HandleNoContent(await reactivateUseCase.Execute(id, UserId, ct));

    [HttpPatch("{id:guid}/lifecycle")]
    public async Task<IActionResult> Lifecycle(Guid id, [FromBody] ProductLifecycleRequestDto dto, CancellationToken ct)
        => HandleNoContent(await updateLifecycleUseCase.Execute(id, dto, ct));

    [HttpPost("{id:guid}/image")]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile? image, CancellationToken ct)
    {
        await using var content = image?.OpenReadStream();
        var file = image is null ? null : new UploadFileDto(image.FileName, image.ContentType, image.Length, content!);
        return Handle(await uploadImageUseCase.Execute(id, file, ct));
    }

    [HttpDelete("{id:guid}/image")]
    public async Task<IActionResult> RemoveImage(Guid id, CancellationToken ct)
        => HandleNoContent(await removeImageUseCase.Execute(id, ct));
}
