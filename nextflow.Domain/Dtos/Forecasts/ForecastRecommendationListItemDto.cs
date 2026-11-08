using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>
/// Linha da tabela da previsão (T-DF2), uma por produto. Todo número vem do que o motor gravou (ADR 0005); o motivo curto corta o
/// motivo final em 160 caracteres (o completo fica no detalhe, T-DF3). Transferir (F3.4b) ainda não existe: <c>Transfer</c> é sempre nulo.
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
    public decimal? CoverageDays { get; set; }
    public decimal ProjectedDemand { get; set; }
    public string SaleUnit { get; set; } = string.Empty;
    public int DecimalPlaces { get; set; }
    public decimal? SuggestedQuantity { get; set; }
    public decimal? SuggestedDiscountPercent { get; set; }
    public int? SuggestedPromotionDays { get; set; }
    public object? Transfer { get; set; }
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
    }

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
