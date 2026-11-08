using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Stock;

/// <summary>Transferência gravada (RN-CE62).</summary>
public class StockTransferDto
{
    public Guid Id { get; set; }
    public int Number { get; set; }
    public Guid FromLocationId { get; set; }
    public string FromLocationName { get; set; } = string.Empty;
    public Guid ToLocationId { get; set; }
    public string ToLocationName { get; set; } = string.Empty;
    public string? Note { get; set; }
    public Guid? ForecastRecommendationId { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<StockTransferItemDto> Items { get; set; } = [];

    public StockTransferDto() { }

    /// <param name="transfer">Com os locais (e a loja de cada um) e as variações com o produto carregados.</param>
    public StockTransferDto(StockTransfer transfer)
    {
        Id = transfer.Id;
        Number = transfer.Number;
        FromLocationId = transfer.FromLocationId;
        FromLocationName = LocationName(transfer.FromLocation);
        ToLocationId = transfer.ToLocationId;
        ToLocationName = LocationName(transfer.ToLocation);
        Note = transfer.Note;
        ForecastRecommendationId = transfer.ForecastRecommendationId;
        CreatedAt = transfer.CreatedAt;
        Items = [.. transfer.Items.Select(i => new StockTransferItemDto
        {
            VariantId = i.VariantId,
            ProductName = i.Variant != null && i.Variant.Product != null ? i.Variant.Product.Name : string.Empty,
            Sku = i.Variant != null ? i.Variant.Sku : string.Empty,
            Quantity = i.Quantity,
        })];
    }

    private static string LocationName(StockLocation? location)
        => location == null ? string.Empty : location.Store != null ? $"{location.Store.Name} › {location.Name}" : location.Name;
}

public class StockTransferItemDto
{
    public Guid VariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
}
