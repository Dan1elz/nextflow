using Nextflow.Domain.Dtos.Categories;

namespace Nextflow.Domain.Dtos.Products;

/// <summary>Opções do formulário T2: as ativas e as já vinculadas ao produto, mesmo inativas. Eventos entram na F1.4.</summary>
public class ProductFormOptionsDto
{
    public List<CategoryOptionDto> Categories { get; set; } = [];
    public List<Option> Collections { get; set; } = [];
}
