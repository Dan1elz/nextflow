using Nextflow.Domain.Dtos.Forecasts;
using Nextflow.Domain.Enums;

namespace Nextflow.Domain.Models;

/// <summary>
/// Recomendação por produto de uma análise (RN-DF21). Nasce <c>Pending</c>; a nova análise expira as pendentes da anterior
/// (RN-DF14). O documento gerado ao aplicar (pedido de compra ou promoção) fica nas FKs <c>Applied*</c>, preenchidas na F3.4.
/// </summary>
public class ForecastRecommendation : BaseModel
{
    public Guid ForecastRunId { get; private set; }
    public Guid ProductId { get; private set; }
    public RecommendationActionEnum Action { get; private set; }
    public RecommendationPriorityEnum Priority { get; private set; }
    /// <summary>Sempre do cálculo: a IA não altera a confiança (RN-DF10).</summary>
    public ForecastConfidenceEnum Confidence { get; private set; }
    public RecommendationStatusEnum Status { get; private set; } = RecommendationStatusEnum.Pending;
    public decimal? SuggestedQuantity { get; private set; }
    /// <summary>Quantidade do cálculo antes do ajuste da IA (0,5× a 1,5×, RN-DF12): a T-DF3 mostra as duas.</summary>
    public decimal? CalculatedQuantity { get; private set; }
    public decimal? SuggestedDiscountPercent { get; private set; }
    public int? SuggestedPromotionDays { get; private set; }
    public DateOnly? Deadline { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string ReasonCalculated { get; private set; } = string.Empty;
    public bool AiAdjusted { get; private set; }
    public decimal CurrentStock { get; private set; }
    public decimal IncomingStock { get; private set; }
    public decimal ProjectedDemand { get; private set; }
    public decimal NextMonthDemand { get; private set; }
    public decimal? CoverageDays { get; private set; }
    public decimal NextMonthSeasonalIndex { get; private set; }
    public decimal SafetyStock { get; private set; }
    public int LeadTimeDays { get; private set; }
    public LeadTimeSourceEnum LeadTimeSource { get; private set; }
    public short MonthsOfHistory { get; private set; }
    /// <summary>Projeção pelo mesmo mês do ano anterior (RN-DF04).</summary>
    public bool BasedOnLastYear { get; private set; }
    public DateTime? AppliedAt { get; private set; }
    public Guid? AppliedById { get; private set; }
    public Guid? AppliedPurchaseOrderId { get; private set; }
    public Guid? AppliedPromotionId { get; private set; }
    /// <summary>Transferência gerada pelo "Transferir" (F3.4b, RN-CE62).</summary>
    public Guid? AppliedTransferId { get; private set; }
    /// <summary>"Transferir" (RN-DF16): de onde (excesso) e para onde (abaixo do mínimo); as quantidades por variação ficam nos itens.</summary>
    public Guid? TransferFromLocationId { get; private set; }
    public Guid? TransferToLocationId { get; private set; }
    /// <summary>Texto livre do descarte (T-DF8); obrigatório com o motivo "Outro".</summary>
    public string? DismissReason { get; private set; }
    public DismissReasonEnum? DismissReasonCode { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public virtual ForecastRun ForecastRun { get; private set; } = null!;
    public virtual Product Product { get; private set; } = null!;
    public virtual PurchaseOrder? AppliedPurchaseOrder { get; private set; }
    public virtual Promotion? AppliedPromotion { get; private set; }
    public virtual StockLocation? TransferFromLocation { get; private set; }
    public virtual StockLocation? TransferToLocation { get; private set; }
    public virtual ICollection<ForecastRecommendationItem> Items { get; private set; } = [];

    /// <summary>Pedidos gerados pelo "Comprar" (RN-DF28); <see cref="AppliedPurchaseOrderId"/> guarda o primeiro.</summary>
    public virtual ICollection<ForecastRecommendationPurchaseOrder> PurchaseOrders { get; private set; } = [];

    /// <summary>"Comprar": quanto vai para cada loja, pela análise (RN-DF27).</summary>
    public virtual ICollection<ForecastRecommendationStore> Stores { get; private set; } = [];

    public override string Preposition => "a";
    public override string Singular => "recomendação";
    public override string Plural => "recomendações";

    private ForecastRecommendation() { }

    public ForecastRecommendation(NewForecastRecommendationDto dto)
    {
        ForecastRunId = dto.ForecastRunId;
        ProductId = dto.ProductId;
        TransferFromLocationId = dto.TransferFromLocationId;
        TransferToLocationId = dto.TransferToLocationId;
        Action = dto.Action;
        Priority = dto.Priority;
        Confidence = dto.Confidence;
        SuggestedQuantity = dto.SuggestedQuantity;
        CalculatedQuantity = dto.CalculatedQuantity;
        SuggestedDiscountPercent = dto.SuggestedDiscountPercent;
        SuggestedPromotionDays = dto.SuggestedPromotionDays;
        Deadline = dto.Deadline;
        Reason = dto.Reason;
        ReasonCalculated = dto.ReasonCalculated;
        AiAdjusted = dto.AiAdjusted;
        CurrentStock = dto.CurrentStock;
        IncomingStock = dto.IncomingStock;
        ProjectedDemand = dto.ProjectedDemand;
        NextMonthDemand = dto.NextMonthDemand;
        CoverageDays = dto.CoverageDays;
        NextMonthSeasonalIndex = dto.NextMonthSeasonalIndex;
        SafetyStock = dto.SafetyStock;
        LeadTimeDays = dto.LeadTimeDays;
        LeadTimeSource = dto.LeadTimeSource;
        MonthsOfHistory = dto.MonthsOfHistory;
        BasedOnLastYear = dto.BasedOnLastYear;

        foreach (var item in dto.Items)
            Items.Add(new ForecastRecommendationItem(Id, item));
        foreach (var (storeId, share, shortfall, quantity) in dto.Stores)
            Stores.Add(new ForecastRecommendationStore(Id, storeId, share, shortfall, quantity));
    }

    public bool IsPending => Status == RecommendationStatusEnum.Pending;

    /// <summary>
    /// Comprar aplicado (T-DF4, RN-DF15): o pedido de compra em Orçamento gerado. Com a compra dividida entre lojas (RN-DF27) a
    /// mesma recomendação recebe um pedido por loja: cada um entra em <see cref="PurchaseOrders"/> e o primeiro fica em
    /// <see cref="AppliedPurchaseOrderId"/>.
    /// </summary>
    public void ApplyPurchase(Guid purchaseOrderId, Guid appliedById, DateTime utcNow)
    {
        if (Status != RecommendationStatusEnum.Applied)
            MarkApplied(appliedById, utcNow);
        AppliedPurchaseOrderId ??= purchaseOrderId;
        if (PurchaseOrders.All(link => link.PurchaseOrderId != purchaseOrderId))
            PurchaseOrders.Add(new ForecastRecommendationPurchaseOrder(Id, purchaseOrderId));
    }

    /// <summary>Promover aplicado (T-DF6): a promoção criada (a primeira, quando o escopo gera uma por variação).</summary>
    public void ApplyPromotion(Guid promotionId, Guid appliedById, DateTime utcNow)
    {
        MarkApplied(appliedById, utcNow);
        AppliedPromotionId = promotionId;
    }

    /// <summary>Transferir aplicado (F3.4b): a transferência de estoque gerada.</summary>
    public void ApplyTransfer(Guid transferId, Guid appliedById, DateTime utcNow)
    {
        MarkApplied(appliedById, utcNow);
        AppliedTransferId = transferId;
    }

    /// <summary>Descartar (T-DF8), com motivo opcional.</summary>
    public void Dismiss(DismissReasonEnum? reasonCode, string? note, Guid dismissedById, DateTime utcNow)
    {
        Status = RecommendationStatusEnum.Dismissed;
        DismissReasonCode = reasonCode;
        DismissReason = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        AppliedById = dismissedById;
        AppliedAt = utcNow;
        UpdatedAt = utcNow;
    }

    /// <summary>
    /// Um pedido gerado foi cancelado (F3.4, RN-DF28). Com outro pedido dela ainda aberto, a recomendação continua Aplicada e
    /// passa a apontar para ele. Sem nenhum, volta a Pendente se for da análise vigente; de análise antiga, expira (a nova análise
    /// já tem a recomendação atual do produto).
    /// </summary>
    /// <param name="stillOpenOrderId">Outro pedido da recomendação que não foi cancelado, se houver.</param>
    public void RevertPurchase(Guid canceledOrderId, Guid? stillOpenOrderId, bool fromLatestRun, DateTime utcNow)
    {
        if (Status != RecommendationStatusEnum.Applied || AppliedPurchaseOrderId is null)
            return;

        if (stillOpenOrderId is { } open)
        {
            if (AppliedPurchaseOrderId == canceledOrderId)
                AppliedPurchaseOrderId = open;
            UpdatedAt = utcNow;
            return;
        }

        Status = fromLatestRun ? RecommendationStatusEnum.Pending : RecommendationStatusEnum.Expired;
        AppliedPurchaseOrderId = null;
        AppliedById = null;
        AppliedAt = null;
        UpdatedAt = utcNow;
    }

    private void MarkApplied(Guid appliedById, DateTime utcNow)
    {
        Status = RecommendationStatusEnum.Applied;
        AppliedById = appliedById;
        AppliedAt = utcNow;
        UpdatedAt = utcNow;
    }

    /// <summary>Nova análise expira as pendentes da anterior (RN-DF14); recomendação já aplicada ou descartada não muda.</summary>
    public void Expire(DateTime utcNow)
    {
        if (!IsPending)
            return;

        Status = RecommendationStatusEnum.Expired;
        UpdatedAt = utcNow;
    }
}
