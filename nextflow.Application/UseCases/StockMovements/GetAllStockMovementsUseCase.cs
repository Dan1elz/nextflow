using Microsoft.EntityFrameworkCore;
using Nextflow.Application.Filters;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.StockMovements;

public class GetAllStockMovementsUseCase(IStockMovementRepository repository)
    : GetAllUseCaseBase<StockMovement, IStockMovementRepository, StockMovementResponseDto>(repository)
{
    protected override StockMovementResponseDto MapToResponseDto(StockMovement entity) => new(entity);
    protected override Func<IQueryable<StockMovement>, IQueryable<StockMovement>>? GetInclude() => query => query.Include(u => u.User).Include(p => p.Product);

    protected override void ApplyFilters(FilterExpressionBuilder<StockMovement> builder, FilterSet filters)
    {
        builder
            .WhereStringContains(filters, "search", s => s.Description ?? string.Empty)
            .WhereGuidEquals(filters, "productId", s => s.ProductId)
            .WhereGuidEquals(filters, "userId", s => s.UserId)
            .WhereEnumEquals<MovementType>(filters, "movementType", s => s.MovementType)
            .WhereDateTimeGte(filters, "createAt", s => s.CreateAt);
    }
}