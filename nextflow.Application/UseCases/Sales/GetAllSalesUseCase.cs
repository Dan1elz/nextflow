using Microsoft.EntityFrameworkCore;
using Nextflow.Application.Filters;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Sales;

public class GetAllSalesUseCase(ISaleRepository repository)
    : GetAllUseCaseBase<Sale, ISaleRepository, SaleResponseDto>(repository)
{
    protected override SaleResponseDto MapToResponseDto(Sale entity) => new(entity);

    protected override void ApplyFilters(FilterExpressionBuilder<Sale> builder, FilterSet filters)
    {
        if (filters.TryGetGuid("orderId", out var orderId))
            builder.And(x => x.OrderId == orderId);

        if (filters.TryGetGuid("userId", out var userId))
            builder.And(x => x.UserId == userId);
    }
}