using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.PurchaseOrders;

/// <summary>Fornecedor no detalhe do pedido (T-CP3). O contato principal vem do diretório de donos.</summary>
public class PurchaseOrderSupplierDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Cnpj { get; set; } = string.Empty;
    public string? MainContact { get; set; }

    public PurchaseOrderSupplierDto() { }

    public PurchaseOrderSupplierDto(Supplier entity)
    {
        Id = entity.Id;
        Name = entity.Name;
        Cnpj = entity.Cnpj.Masked;
    }
}
