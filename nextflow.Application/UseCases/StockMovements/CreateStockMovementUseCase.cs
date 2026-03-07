using Nextflow.Domain.Dtos;
using Nextflow.Domain.Exceptions;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Interfaces.UseCases;
using Nextflow.Domain.Models;
using Nextflow.Application.UseCases.Base;

namespace Nextflow.Application.UseCases.StockMovements;

public class CreateStockMovementUseCase(
    IStockMovementRepository repository,
    IProductRepository productRepository
    )
: CreateUseCaseBase<StockMovement, IStockMovementRepository, CreateStockMovementDto, StockMovementResponseDto>(repository),
  ICreateStockMovementUseCase
{
    private readonly IProductRepository _productRepository = productRepository;

    protected override StockMovement MapToEntity(CreateStockMovementDto dto) => new(dto);
    protected override StockMovementResponseDto MapToResponseDto(StockMovement entity) => new(entity);

    protected override async Task BeforePersistence(StockMovement entity, CreateStockMovementDto dto, CancellationToken ct)
    {
        if (!dto.IsSystemGenerated &&
            dto.MovementType != Nextflow.Domain.Enums.MovementType.Entry &&
            dto.MovementType != Nextflow.Domain.Enums.MovementType.Exit &&
            dto.MovementType != Nextflow.Domain.Enums.MovementType.Adjustment)
        {
            throw new BadRequestException("Este tipo de movimentação não pode ser criado manualmente. As movimentações de vendas ou compras ocorrem automaticamente por seus módulos.");
        }

        var product = await _productRepository.GetByIdAsync(dto.ProductId, ct) ?? throw new NotFoundException("Produto não encontrado.");
        entity.SetQuote(dto.Quote ?? product.Price);
        product.SetMovementStock(dto);
        await _productRepository.UpdateAsync(product, ct);
    }
}