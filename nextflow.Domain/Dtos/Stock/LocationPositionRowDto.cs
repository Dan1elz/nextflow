namespace Nextflow.Domain.Dtos.Stock;

/// <summary>Variação ativa × local ativo: disponível e mínimo efetivo (do local, senão o padrão do produto, RN-CE33). Base do "Transferir" (RN-DF16).</summary>
public record LocationPositionRowDto(Guid ProductId, Guid VariantId, Guid LocationId, string LocationName, Guid StoreId, string StoreName,
    bool IsSellable, decimal Available, decimal Minimum);
