using Nextflow.Domain.Dtos.Stores;

namespace Nextflow.Domain.Dtos.PurchaseOrders;

/// <summary>Combos do formulário do pedido de compra.</summary>
public class PurchaseOrderFormOptionsDto
{
    public List<LocationOptionDto> Locations { get; set; } = [];
}
