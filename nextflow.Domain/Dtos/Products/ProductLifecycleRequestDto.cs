using Nextflow.Domain.Enums;

namespace Nextflow.Domain.Dtos.Products;

/// <summary>Marca o produto em linha ou fora de linha (RN-CE19).</summary>
public class ProductLifecycleRequestDto
{
    public LifecycleEnum Lifecycle { get; set; }
}
