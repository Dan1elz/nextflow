using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Exceptions;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Interfaces.UseCases.Base;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.StockMovements;

public class DeleteStockMovementUseCase(IStockMovementRepository repository, IProductRepository productRepository)
     : DeleteUseCaseBase<StockMovement, IStockMovementRepository>(repository)
{
    private readonly IProductRepository _productRepository = productRepository;

    protected override void ValidateBusinessRules(StockMovement entity)
    {
        if (entity.CreateAt.AddHours(24) < DateTime.UtcNow)
            throw new BadRequestException("Não é possível excluir movimentações criadas há mais de 24 horas.");

        if (entity.MovementType == MovementType.Sales)
            throw new BadRequestException("Não é possível excluir movimentações do tipo venda.");
    }

    protected override async Task PerformSideEffects(StockMovement entity, CancellationToken ct, Guid? userId = null)
    {
        var product = await _productRepository.GetByIdAsync(entity.ProductId, ct)
            ?? throw new NotFoundException("Produto não encontrado ao reverter a movimentação.");
            
        product.RevertMovementStock(entity);
        await _productRepository.UpdateAsync(product, ct);
    }
}