using Nextflow.Domain.Dtos.Categories;
using Nextflow.Domain.Dtos.Stores;
using Nextflow.Domain.Dtos.Suppliers;

namespace Nextflow.Domain.Dtos.Products;

/// <summary>Opções do formulário T2: os ativos e os já vinculados ao produto, mesmo inativos.</summary>
public class ProductFormOptionsDto
{
    public List<CategoryOptionDto> Categories { get; set; } = [];
    public List<Option> Collections { get; set; } = [];
    public List<Option> Events { get; set; } = [];
    public List<SupplierOptionDto> Suppliers { get; set; } = [];
    /// <summary>Colunas da tabela de mínimos (RN-CE33): locais ativos de lojas ativas e os que já têm mínimo no produto.</summary>
    public List<LocationOptionDto> Locations { get; set; } = [];
}
