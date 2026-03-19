using Microsoft.EntityFrameworkCore;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Exceptions;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Interfaces.UseCases;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.PurchaseOrders;

public class UpdatePurchaseOrderUseCase(
    IPurchaseOrderRepository repository,
    IPurchaseItemRepository itemRepository,
    IProductRepository productRepository,
    ICreateStockMovementUseCase stockMovementUseCase
) : UpdateUseCaseBase<PurchaseOrder, IPurchaseOrderRepository, UpdatePurchaseOrderDto, PurchaseOrderResponseDto>(repository)
{
    private readonly IPurchaseItemRepository _itemRepository = itemRepository;
    private readonly IProductRepository _productRepository = productRepository;
    private readonly ICreateStockMovementUseCase _stockMovementUseCase = stockMovementUseCase;

    protected override PurchaseOrderResponseDto MapToResponseDto(PurchaseOrder entity) => new(entity);

    protected override Func<IQueryable<PurchaseOrder>, IQueryable<PurchaseOrder>>? GetInclude() =>
        query => query.Include(p => p.PurchaseItems);

    protected override async Task ValidateBusinessRules(PurchaseOrder entity, UpdatePurchaseOrderDto dto, CancellationToken ct)
    {
        if (entity.PurchaseStatus == PurchaseStatus.Received && dto.Status.HasValue && dto.Status.Value != PurchaseStatus.Received)
            throw new BadRequestException("Não é possível alterar o status de um pedido que já foi recebido.");

        if (dto.Items != null && (entity.PurchaseStatus == PurchaseStatus.Received || entity.PurchaseStatus == PurchaseStatus.Canceled))
            throw new BadRequestException("Não é possível atualizar itens de um pedido recebido ou cancelado.");
    }

    protected override async Task BeforePersistence(PurchaseOrder entity, UpdatePurchaseOrderDto dto, CancellationToken ct)
    {
        // Se Items forem fornecidos, substitui (abordagem simplificada)
        if (dto.Items != null && dto.Items.Count != 0)
        {
            await _itemRepository.RemoveRangeAsync(entity.PurchaseItems, ct);
            entity.PurchaseItems.Clear();

            decimal totalAmount = 0;
            foreach (var itemDto in dto.Items)
            {
                var item = new PurchaseItem(
                    entity.Id,
                    itemDto.ProductId,
                    itemDto.Quantity,
                    itemDto.Discount,
                    itemDto.CostPrice
                );
                totalAmount += ((decimal)item.Quantity * item.CostPrice) - item.Discount;
                entity.PurchaseItems.Add(item);
            }
            entity.SetTotalAmount(totalAmount);
        }
    }

    protected override async Task AfterPersistence(PurchaseOrder entity, UpdatePurchaseOrderDto dto, CancellationToken ct)
    {
        // Se o status mudou para Received, aumenta estoque
        if (dto.Status.HasValue && dto.Status.Value == PurchaseStatus.Received)
        {
            foreach (var item in entity.PurchaseItems)
            {
                await _stockMovementUseCase.Execute(new CreateStockMovementDto
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    MovementType = MovementType.Entry,
                    Description = $"Entrada de estoque via Pedido de Compra {entity.Id}",
                    Quote = item.CostPrice,
                    IsSystemGenerated = true
                }, ct);
            }
        }
    }
}
