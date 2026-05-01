using Microsoft.EntityFrameworkCore;
using Nextflow.Application.Filters;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Interfaces.UseCases;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.PurchaseOrders;

public class GetAllPurchaseOrdersUseCase(IPurchaseOrderRepository repository)
    : GetAllUseCaseBase<PurchaseOrder, IPurchaseOrderRepository, PurchaseOrderResponseDto>(repository)
{
    protected override PurchaseOrderResponseDto MapToResponseDto(PurchaseOrder entity) => new(entity);

    protected override Func<IQueryable<PurchaseOrder>, IQueryable<PurchaseOrder>>? GetInclude() =>
        query => query.Include(p => p.Supplier).Include(p => p.PurchaseItems).ThenInclude(i => i.Product);

    protected override void ApplyFilters(FilterExpressionBuilder<PurchaseOrder> builder, FilterSet filters)
    {
        if (filters.TryGetGuid("supplierId", out var supplierId))
            builder.And(x => x.SupplierId == supplierId);

        if (filters.TryGetString("status", out var statusStr) && Enum.TryParse<PurchaseStatus>(statusStr, true, out var status))
            builder.And(x => x.PurchaseStatus == status);

        if (filters.TryGetString("active", out var activeStr) && FilterValueParsers.TryParseBool(activeStr, out var active))
            builder.And(x => x.Active == active);
    }
}
