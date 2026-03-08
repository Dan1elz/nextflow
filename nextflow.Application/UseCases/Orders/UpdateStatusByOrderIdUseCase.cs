using Nextflow.Domain.Interfaces.UseCases;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Exceptions;
using Nextflow.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Nextflow.Application.UseCases.Orders;

public class UpdateStatusByOrderIdUseCase(
    IOrderRepository repository,
    ICreateStockMovementUseCase createStockMovementUseCase,
    IProductRepository productRepository) : IUpdateStatusByOrderIdUseCase
{
    private readonly IOrderRepository _repository = repository;
    private readonly ICreateStockMovementUseCase _createStockMovementUseCase = createStockMovementUseCase;
    private readonly IProductRepository _productRepository = productRepository;

    public async Task<OrderResponseDto> Execute(Guid orderId, Guid userId, OrderStatus status, string? reason, CancellationToken ct)
    {
        var entity = await _repository.GetByIdAsync(orderId, ct, q => q.Include(o => o.OrderItems))
            ?? throw new NotFoundException($"Pedido não encontrado com o Id: {orderId}");

        var previousStatus = entity.Status;

        if (status == OrderStatus.PaymentConfirmed && entity.Type == OrderType.Budget)
        {
            var productIds = entity.OrderItems.Select(x => x.ProductId).Distinct().ToList();
            var products = await _productRepository.GetAllAsync(p => productIds.Contains(p.Id), 0, int.MaxValue, ct);
            var productMap = products.ToDictionary(p => p.Id);

            foreach (var item in entity.OrderItems)
            {
                if (!productMap.TryGetValue(item.ProductId, out var product))
                    throw new BadRequestException($"Produto com ID {item.ProductId} não encontrado.");

                if (item.Quantity > product.Quantity)
                    throw new BadRequestException($"Estoque insuficiente para o produto {product.Name}. Quantidade requerida: {item.Quantity}, Estoque atual: {product.Quantity}");
            }

            foreach (var item in entity.OrderItems)
            {
                await _createStockMovementUseCase.Execute(new CreateStockMovementDto
                {
                    ProductId = item.ProductId,
                    Quantity = (double)item.Quantity,
                    MovementType = MovementType.Sales,
                    Description = $"Efetivação do orçamento {entity.Id}",
                    UserId = userId,
                    Quote = item.UnitPrice,
                    IsSystemGenerated = true
                }, ct);
            }
        }

        entity.UpdateStatus(status, reason);

        bool shouldReturnStock = status == OrderStatus.Refunded || (status == OrderStatus.Canceled && entity.Type != OrderType.Budget);

        if (shouldReturnStock)
        {
            var actionName = status == OrderStatus.Refunded ? "Reembolso" : "Cancelamento";
            await ReturnStock(entity, userId, actionName, ct);
        }

        await _repository.UpdateAsync(entity, ct);

        return new OrderResponseDto(entity);
    }

    private async Task ReturnStock(Nextflow.Domain.Models.Order entity, Guid userId, string actionName, CancellationToken ct)
    {
        foreach (var item in entity.OrderItems)
        {
            await _createStockMovementUseCase.Execute(new CreateStockMovementDto
            {
                ProductId = item.ProductId,
                Quantity = (double)item.Quantity,
                MovementType = MovementType.Return,
                Description = $"Estorno ({actionName}) do pedido {entity.Id}",
                UserId = userId,
                IsSystemGenerated = true
            }, ct);
        }
    }
}
