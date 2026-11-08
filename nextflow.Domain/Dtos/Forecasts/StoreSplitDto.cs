namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>Sugestão de divisão da compra entre lojas (RN-DF27, F3.9): por loja e, dentro de cada variação, a quantidade de cada loja.</summary>
public class StoreSplitDto
{
    public Guid RecommendationId { get; set; }
    public decimal? OrderMultiple { get; set; }
    public List<StoreSplitStoreDto> Stores { get; set; } = [];
    public List<StoreSplitItemDto> Items { get; set; } = [];
}

public class StoreSplitStoreDto
{
    public Guid StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    /// <summary>Local de recebimento padrão da loja; nulo se a loja não tem (ela não recebe a sugestão).</summary>
    public Guid? ReceiveLocationId { get; set; }
    public string? ReceiveLocationName { get; set; }
    /// <summary>Participação da loja nas vendas do produto nos últimos 90 dias.</summary>
    public decimal SharePercent { get; set; }
    public decimal CurrentStock { get; set; }
    public decimal IncomingStock { get; set; }
    public decimal Shortfall { get; set; }
    /// <summary>Soma das variações para a loja.</summary>
    public decimal Quantity { get; set; }
}

public class StoreSplitItemDto
{
    public Guid VariantId { get; set; }
    public decimal Quantity { get; set; }
    public List<StoreSplitPartDto> ByStore { get; set; } = [];
}

public class StoreSplitPartDto
{
    public Guid StoreId { get; set; }
    public decimal Quantity { get; set; }
}
