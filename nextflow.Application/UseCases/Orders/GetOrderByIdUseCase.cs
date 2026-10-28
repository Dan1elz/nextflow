using Microsoft.EntityFrameworkCore;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Domain.Utils;

namespace Nextflow.Application.UseCases.Orders;

public class GetOrderByIdUseCase(IOrderRepository repository)
    : GetByIdUseCaseBase<Order, IOrderRepository, OrderResponseDto>(repository)
{
    protected override OrderResponseDto MapToResponseDto(Order entity) => new(entity);

    protected override Func<IQueryable<Order>, IQueryable<Order>>? GetInclude()
    {
        // Promoções vigentes dos produtos: o formulário de edição do pedido usa product.effectivePrice,
        // o mesmo preço que o UpdateOrderUseCase cobra.
        var today = BusinessClock.Today;
        return query => query
            .Include(c => c.Client)
            .Include(u => u.User)
            .Include(oi => oi.OrderItems)
                .ThenInclude(p => p.Product)
                    .ThenInclude(p => p!.Promotions.Where(pr => pr.IsActive && pr.StartDate <= today && pr.EndDate >= today));
    }
}
