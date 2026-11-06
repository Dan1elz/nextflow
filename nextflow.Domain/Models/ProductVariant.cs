using Nextflow.Domain.Dtos.Products;

namespace Nextflow.Domain.Models;

/// <summary>Unidade de estoque, venda e compra (ADR 0008). Custo médio e último custo são escritos só pelo estoque (RN-CE14, RN-CE31).</summary>
public class ProductVariant : BaseModel
{
    public Guid ProductId { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public string? Barcode { get; private set; }
    public decimal Price { get; private set; }
    public short SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;
    /// <summary>Saiu do formulário: fica inativa e fora das telas de variação, para o histórico de preço não se perder (RN-CE58).</summary>
    public DateOnly? RemovedOn { get; private set; }
    /// <summary>Custo médio ponderado da variação na empresa inteira (RN-CE31). Nulo até a primeira entrada com custo.</summary>
    public decimal? AverageCost { get; private set; }
    /// <summary>Custo da última entrada com custo: só informação, não entra em cálculo (RN-CE31).</summary>
    public decimal? LastCost { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public virtual Product Product { get; private set; } = null!;
    public virtual ICollection<ProductVariantValue> AttributeValues { get; private set; } = [];
    public virtual ICollection<PriceHistory> PriceHistory { get; private set; } = [];
    public virtual ICollection<StockBalance> Balances { get; private set; } = [];
    public virtual ICollection<StockLocationMinimum> LocationMinimums { get; private set; } = [];

    public override string Preposition => "a";
    public override string Singular => "variação";
    public override string Plural => "variações";

    private ProductVariant() { }

    public ProductVariant(ProductVariantRequestDto dto, Guid productId, short sortOrder, Guid changedById, DateOnly today)
    {
        ProductId = productId;
        Apply(dto, sortOrder);
        PriceHistory.Add(new PriceHistory(Id, Price, today, changedById));
    }

    /// <summary>Preço igual não grava nada; preço diferente fecha a faixa aberta e abre outra (RN-CE25, RN-CE59).</summary>
    public void Update(ProductVariantRequestDto dto, short sortOrder, Guid changedById, DateOnly today)
    {
        var previousPrice = Price;
        Apply(dto, sortOrder);
        UpdatedAt = DateTime.UtcNow;

        if (Price == previousPrice)
            return;

        CloseCurrentPrice(today);
        PriceHistory.Add(new PriceHistory(Id, Price, today, changedById));
    }

    /// <summary>Remoção sem apagar: toda variação gravada tem histórico de preço, e histórico não se apaga (RN-CE58).</summary>
    public void Remove(DateOnly today)
    {
        IsActive = false;
        RemovedOn = today;
        UpdatedAt = DateTime.UtcNow;
        CloseCurrentPrice(today);
    }

    /// <summary>
    /// Entrada com custo (RN-CE31): médio = (qtd atual × médio + qtd entrada × custo) / qtd total, com a quantidade da
    /// empresa inteira antes da entrada. Sem médio anterior, ou com saldo zerado ou negativo, não há base para ponderar e o
    /// médio passa a ser o custo da entrada. Chamado só pelo <c>IStockLedger</c>.
    /// </summary>
    public void ApplyInboundCost(decimal companyQuantityBefore, decimal quantity, decimal unitCost)
    {
        LastCost = unitCost;
        AverageCost = AverageCost is not { } average || companyQuantityBefore <= 0
            ? unitCost
            : Math.Round((companyQuantityBefore * average + quantity * unitCost) / (companyQuantityBefore + quantity), 4, MidpointRounding.AwayFromZero);
    }

    public void SyncValues(List<Guid> attributeValueIds)
    {
        foreach (var link in AttributeValues.Where(link => !attributeValueIds.Contains(link.AttributeValueId)).ToList())
            AttributeValues.Remove(link);

        var linked = AttributeValues.Select(link => link.AttributeValueId).ToHashSet();
        foreach (var valueId in attributeValueIds.Where(id => !linked.Contains(id)))
            AttributeValues.Add(new ProductVariantValue(Id, valueId));
    }

    /// <summary>
    /// Substitui os mínimos por local pelos do formulário (RN-CE33): o local que saiu volta a usar o mínimo padrão do
    /// produto; o que ficou preserva a linha.
    /// </summary>
    public void SyncMinimums(IReadOnlyDictionary<Guid, decimal> minimumsByLocation)
    {
        foreach (var minimum in LocationMinimums.Where(m => !minimumsByLocation.ContainsKey(m.LocationId)).ToList())
            LocationMinimums.Remove(minimum);

        foreach (var (locationId, quantity) in minimumsByLocation)
        {
            var minimum = LocationMinimums.FirstOrDefault(m => m.LocationId == locationId);
            if (minimum is null)
                LocationMinimums.Add(new StockLocationMinimum(Id, locationId, quantity));
            else
                minimum.Update(quantity);
        }
    }

    private void CloseCurrentPrice(DateOnly today)
    {
        foreach (var current in PriceHistory.Where(entry => entry.ValidTo is null))
            current.Close(today);
    }

    private void Apply(ProductVariantRequestDto dto, short sortOrder)
    {
        Sku = dto.Sku.Trim();
        Barcode = string.IsNullOrWhiteSpace(dto.Barcode) ? null : dto.Barcode.Trim();
        Price = dto.Price;
        SortOrder = sortOrder;
        IsActive = dto.IsActive;
    }
}
