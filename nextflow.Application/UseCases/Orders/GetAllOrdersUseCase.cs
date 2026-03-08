using Microsoft.EntityFrameworkCore;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Application.Filters;
using Nextflow.Domain.Enums;

namespace Nextflow.Application.UseCases.Orders;

public class GetAllOrdersUseCase(IOrderRepository repository) : GetAllUseCaseBase<Order, IOrderRepository, OrderResponseDto>(repository)
{
    protected override OrderResponseDto MapToResponseDto(Order entity) => new(entity);
    protected override Func<IQueryable<Order>, IQueryable<Order>>? GetInclude() => query => query
            .Include(c => c.Client)
            .Include(u => u.User)
            .Include(oi => oi.OrderItems)
                .ThenInclude(p => p.Product);

    protected override void ApplyFilters(FilterExpressionBuilder<Order> builder, FilterSet filters)
    {
        if (filters.TryGetString("statusGroup", out var group))
        {
            var twoWeeksAgo = DateTime.UtcNow.AddDays(-14);
            switch (group.ToLower())
            {
                case "open":
                    // Em aberto: PendingPayment e tempo de criação <= 2 semanas, e Type = Sale
                    builder.And(o => o.Status == OrderStatus.PendingPayment && o.Type == OrderType.Sale && o.CreateAt >= twoWeeksAgo);
                    break;
                case "completed":
                    // Vendas concluídas
                    builder.And(o => o.Status == OrderStatus.PaymentConfirmed && o.Type == OrderType.Sale);
                    break;
                case "canceled":
                    // Cancelados
                    builder.And(o => o.Status == OrderStatus.Canceled);
                    break;
                case "refunded":
                    // Reembolsados
                    builder.And(o => o.Status == OrderStatus.Refunded);
                    break;
                case "expired":
                    // Expirados: pedidos em aberto com tempo de criação maior que 2 semanas
                    builder.And(o => o.Status == OrderStatus.PendingPayment && o.Type == OrderType.Sale && o.CreateAt < twoWeeksAgo);
                    break;
                case "budget":
                    // Orçamentos
                    builder.And(o => o.Type == OrderType.Budget);
                    break;
            }
        }

        if (filters.TryGetString("clientId", out var clientIdRaw) && Guid.TryParse(clientIdRaw, out var clientId))
            builder.And(o => o.ClientId == clientId);

        if (filters.TryGetString("userId", out var userIdRaw) && Guid.TryParse(userIdRaw, out var userId))
            builder.And(o => o.UserId == userId);

        if (filters.TryGetString("minAmount", out var minAmtRaw) && decimal.TryParse(minAmtRaw, out var minAmt))
            builder.And(o => o.TotalAmount >= minAmt);

        if (filters.TryGetString("maxAmount", out var maxAmtRaw) && decimal.TryParse(maxAmtRaw, out var maxAmt))
            builder.And(o => o.TotalAmount <= maxAmt);

        if (filters.TryGetString("minUpdateAt", out var minUpdateAtRaw) && DateTime.TryParse(minUpdateAtRaw, out var minUpdateAt))
            builder.And(o => o.UpdateAt >= minUpdateAt);

        if (filters.TryGetString("maxUpdateAt", out var maxUpdateAtRaw) && DateTime.TryParse(maxUpdateAtRaw, out var maxUpdateAt))
            builder.And(o => o.UpdateAt <= maxUpdateAt);
    }
}