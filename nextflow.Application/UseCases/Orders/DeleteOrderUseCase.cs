using Microsoft.EntityFrameworkCore;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Interfaces.UseCases;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Orders;

public class DeleteOrderUseCase(
    IOrderRepository repository,
    ICreateStockMovementUseCase createStockMovement
    ) : DeleteUseCaseBase<Order, IOrderRepository>(repository)
{
    protected override Func<IQueryable<Order>, IQueryable<Order>>? GetInclude()
    {
        return q => q.Include(o => o.OrderItems);
    }
    protected override async Task PerformSideEffects(Order entity, CancellationToken ct, Guid? userId)
    {
        if (!userId.HasValue)
            throw new ArgumentNullException(nameof(userId), "Usuário é obrigatório para cancelar pedidos.");

        foreach (var item in entity.OrderItems)
        {
            var stockMovementDto = new CreateStockMovementDto
            {
                ProductId = item.ProductId,
                Quantity = (double)item.Quantity,
                MovementType = MovementType.Return,
                Description = $"Estorno do pedido {entity.Id}",
                UserId = userId.Value
            };

            await createStockMovement.Execute(stockMovementDto, ct);
        }
    }
}