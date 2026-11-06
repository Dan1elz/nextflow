using Nextflow.Domain.Dtos.Products;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Stock;

/// <summary>
/// Linha do kardex (T8). <see cref="Quantity"/> leva o sinal e <see cref="BalanceAfter"/> é o saldo da variação no local
/// logo depois do lançamento, gravado pelo livro-razão (RN-CE01). Lote entra na F1.7.
/// </summary>
public class StockMovementListItemDto
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public Guid VariantId { get; set; }
    public string VariantLabel { get; set; } = string.Empty;
    public Guid LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public string? LotCode { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal? UnitCost { get; set; }
    public decimal BalanceAfter { get; set; }
    public StockMovementSourceDto? Source { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? Note { get; set; }

    public StockMovementListItemDto() { }

    public StockMovementListItemDto(StockMovement entity)
    {
        Id = entity.Id;
        CreatedAt = entity.CreatedAt;
        VariantId = entity.VariantId;
        if (entity.Variant != null)
        {
            ProductId = entity.Variant.ProductId;
            ProductName = entity.Variant.Product != null ? entity.Variant.Product.Name : string.Empty;
            VariantLabel = ProductVariantResponseDto.LabelOf(entity.Variant);
        }

        LocationId = entity.LocationId;
        LocationName = entity.Location != null ? entity.Location.Name : string.Empty;
        Type = entity.Type.GetApiName();
        Reason = entity.Reason.GetApiName();
        Quantity = entity.Quantity;
        UnitCost = entity.UnitCost;
        BalanceAfter = entity.BalanceAfter;
        Source = entity.SourceId is { } sourceId
            ? new StockMovementSourceDto { Type = entity.SourceType.GetApiName(), Id = sourceId }
            : null;
        UserName = entity.User != null ? entity.User.FullName : string.Empty;
        Note = entity.Note;
    }
}
