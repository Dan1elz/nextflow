using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>
/// Linha da tabela da previsão (T-DF2), uma por produto. Todo número vem do que o motor gravou (ADR 0005); o motivo curto corta o
/// motivo final em 160 caracteres (o completo fica no detalhe, T-DF3). <c>Transfer</c> só na ação "Transferir" (F3.4b, RN-DF16).
/// </summary>
public class ForecastRecommendationListItemDto
{
    public const int ReasonShortLength = 160;

    public Guid Id { get; set; }
    public Guid ForecastRunId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? CategoryPath { get; set; }
    public string? AbcClass { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Confidence { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal IncomingStock { get; set; }
    /// <summary>Saldo da loja pedida em <c>storeId</c>, lido na consulta; nulo sem loja (a análise é da empresa inteira).</summary>
    public decimal? StoreCurrentStock { get; set; }
    /// <summary>"A caminho" para a loja pedida em <c>storeId</c> (RN-CP05); nulo sem loja.</summary>
    public decimal? StoreIncomingStock { get; set; }
    public decimal? CoverageDays { get; set; }
    public decimal ProjectedDemand { get; set; }
    public string SaleUnit { get; set; } = string.Empty;
    public int DecimalPlaces { get; set; }
    public decimal? SuggestedQuantity { get; set; }
    public decimal? SuggestedDiscountPercent { get; set; }
    public int? SuggestedPromotionDays { get; set; }
    public ForecastTransferDto? Transfer { get; set; }
    /// <summary>"Comprar": quanto vai para cada loja, pela análise (RN-DF27); vazio com uma loja só.</summary>
    public List<ForecastStoreQuantityDto> StoreSplit { get; set; } = [];
    public DateOnly? Deadline { get; set; }
    public Guid? PreferredSupplierId { get; set; }
    public string? PreferredSupplierName { get; set; }
    public string ReasonShort { get; set; } = string.Empty;
    public bool AiAdjusted { get; set; }
    public ForecastAppliedLinkDto? AppliedLink { get; set; }

    public ForecastRecommendationListItemDto() { }

    public ForecastRecommendationListItemDto(ForecastRecommendation entity, string? imageUrl)
    {
        Id = entity.Id;
        ForecastRunId = entity.ForecastRunId;
        ProductId = entity.ProductId;
        Action = entity.Action.GetApiName();
        Priority = entity.Priority.GetApiName();
        Confidence = entity.Confidence.GetApiName();
        Status = entity.Status.GetApiName();
        CurrentStock = entity.CurrentStock;
        IncomingStock = entity.IncomingStock;
        CoverageDays = entity.CoverageDays;
        ProjectedDemand = entity.ProjectedDemand;
        SuggestedQuantity = entity.SuggestedQuantity;
        SuggestedDiscountPercent = entity.SuggestedDiscountPercent;
        SuggestedPromotionDays = entity.SuggestedPromotionDays;
        Deadline = entity.Deadline;
        ReasonShort = entity.Reason.Length <= ReasonShortLength ? entity.Reason : entity.Reason[..(ReasonShortLength - 1)].TrimEnd() + "…";
        AiAdjusted = entity.AiAdjusted;
        ImageUrl = imageUrl;

        if (entity.Product != null)
        {
            var product = entity.Product;
            ProductName = product.Name;
            AbcClass = product.AbcClass?.GetApiName();
            SaleUnit = product.SaleUnit.GetApiName();
            DecimalPlaces = product.DecimalPlaces;
            CategoryPath = CategoryPathOf(product);

            var preferred = product.Suppliers.FirstOrDefault(s => s.IsPreferred);
            if (preferred != null)
            {
                PreferredSupplierId = preferred.SupplierId;
                PreferredSupplierName = preferred.Supplier != null ? preferred.Supplier.Name : null;
            }
        }

        if (entity.AppliedPurchaseOrderId is { } purchaseOrderId)
            AppliedLink = new ForecastAppliedLinkDto(ForecastAppliedLinkDto.PurchaseOrderType, purchaseOrderId, entity.AppliedPurchaseOrder != null ? entity.AppliedPurchaseOrder.Number : null);
        else if (entity.AppliedPromotionId is { } promotionId)
            AppliedLink = new ForecastAppliedLinkDto(ForecastAppliedLinkDto.PromotionType, promotionId, null);
        else if (entity.AppliedTransferId is { } transferId)
            AppliedLink = new ForecastAppliedLinkDto(ForecastAppliedLinkDto.TransferType, transferId, null);

        StoreSplit = [.. entity.Stores
            .OrderBy(s => s.Store != null ? s.Store.Name : string.Empty)
            .Select(s => new ForecastStoreQuantityDto
            {
                StoreId = s.StoreId,
                StoreName = s.Store != null ? s.Store.Name : string.Empty,
                SharePercent = s.SharePercent,
                Shortfall = s.Shortfall,
                Quantity = s.Quantity,
            })];

        if (entity.TransferFromLocationId is { } from && entity.TransferToLocationId is { } to)
            Transfer = new ForecastTransferDto
            {
                FromLocationId = from,
                FromLocationName = LocationName(entity.TransferFromLocation),
                ToLocationId = to,
                ToLocationName = LocationName(entity.TransferToLocation),
                Quantity = entity.Items.Sum(i => i.Quantity),
            };
    }

    private static string LocationName(StockLocation? location)
        => location == null ? string.Empty : location.Store != null ? $"{location.Store.Name} › {location.Name}" : location.Name;

    /// <summary>"Pai > Categoria" da categoria ativa mais específica (subcategoria primeiro, depois ordem alfabética).</summary>
    private static string? CategoryPathOf(Product product)
    {
        var category = product.Categories
            .Where(link => link.Category != null && link.Category.IsActive)
            .Select(link => link.Category)
            .OrderBy(c => c.ParentId == null ? 1 : 0)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (category == null)
            return null;

        return category.Parent != null ? $"{category.Parent.Name} > {category.Name}" : category.Name;
    }
}
