using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.PurchaseOrders;

namespace Nextflow.Domain.Interfaces.Application.UseCases;

/// <summary>
/// Listagem de pedidos de compra (T-CP1), da loja selecionada. Filtros: search (nº, fornecedor), status, supplierId,
/// receiveLocationId, from, to (dia de criação), onlyLate.
/// </summary>
public interface IGetPurchaseOrdersUseCase
{
    Task<ResultDto<PaginationDto<PurchaseOrderListItemDto>>> Execute(
        int offset, int limit, IReadOnlyDictionary<string, string>? filters, string? sort, string? order, Guid? storeId,
        CancellationToken ct = default);
}

/// <summary>Formulário de edição (T-CP2) com a sugestão de cada linha.</summary>
public interface IGetPurchaseOrderFormUseCase
{
    Task<ResultDto<PurchaseOrderFormDto>> Execute(Guid id, CancellationToken ct = default);
}

/// <summary>Sugestão da linha ao escolher a variação (T-CP2).</summary>
public interface IGetPurchaseItemSuggestionUseCase
{
    Task<ResultDto<PurchaseItemSuggestionDto>> Execute(Guid supplierId, Guid variantId, Guid storeId, CancellationToken ct = default);
}

/// <summary>Busca de itens do pedido: só os do fornecedor ou todos ("mostrar todos", T-CP2).</summary>
public interface IGetPurchaseVariantOptionsUseCase
{
    Task<ResultDto<List<PurchaseVariantOptionDto>>> Execute(Guid supplierId, string? search, bool onlyLinked, int limit, CancellationToken ct = default);
}

/// <summary>Enviar ao fornecedor (RN-CP08): Orçamento → Pendente.</summary>
public interface ISendPurchaseOrderUseCase
{
    Task<ResultDto<PurchaseOrderDetailDto>> Execute(Guid id, PurchaseOrderVersionDto? dto, CancellationToken ct = default);
}

/// <summary>Receber (T-CP4): Pendente → Recebido, o pedido inteiro, com entrada no estoque pelo livro-razão.</summary>
public interface IReceivePurchaseOrderUseCase
{
    Task<ResultDto<PurchaseOrderDetailDto>> Execute(Guid id, ReceivePurchaseOrderDto? dto, Guid userId, CancellationToken ct = default);
}

/// <summary>Cancelar (T-CP5): Orçamento ou Pendente, com motivo.</summary>
public interface ICancelPurchaseOrderUseCase
{
    Task<ResultDto<PurchaseOrderDetailDto>> Execute(Guid id, CancelPurchaseOrderDto? dto, CancellationToken ct = default);
}
