using Microsoft.EntityFrameworkCore;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Orders;

public class GetOrderByIdUseCase(IOrderRepository repository)
    : GetByIdUseCaseBase<Order, IOrderRepository, OrderResponseDto>(repository)
{
    protected override OrderResponseDto MapToResponseDto(Order entity) => new(entity);

    protected override Func<IQueryable<Order>, IQueryable<Order>>? GetInclude() => query => query
        .Include(c => c.Client)
        .Include(u => u.User)
        .Include(oi => oi.OrderItems)
            .ThenInclude(p => p.Product);
}