using System.ComponentModel.DataAnnotations;
using Nextflow.Domain.Attributes;
using Nextflow.Domain.Enums;

namespace Nextflow.Dtos;

/// <summary>
/// DTO para atualização de produto (PUT). Não inclui Image; use as rotas PUT .../image e DELETE .../image.
/// </summary>
public class UpdateProductRequestDto
{
    [NotEmptyGuid(ErrorMessage = "O Fornecedor é obrigatório.")]
    public Guid SupplierId { get; set; }

    [Required(ErrorMessage = "O código é obrigatório")]
    public string ProductCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "O nome é obrigatório"), StringLength(100, ErrorMessage = "O nome não pode exceder 100 caracteres")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "A descrição é obrigatória"), StringLength(500, ErrorMessage = "A descrição não pode exceder 500 caracteres")]
    public string Description { get; set; } = string.Empty;

    public List<Guid>? CategoryIds { get; set; }

    [Required(ErrorMessage = "A quantidade em estoque é obrigatória"), Range(0, double.MaxValue, ErrorMessage = "A quantidade em estoque não pode ser negativa")]
    public decimal Quantity { get; set; }

    [Required(ErrorMessage = "O tipo de unidade é obrigatório")]
    public UnitType UnitType { get; set; }

    [Required(ErrorMessage = "O preço é obrigatório"), Range(0.0, double.MaxValue, ErrorMessage = "O preço não pode ser negativo")]
    public decimal Price { get; set; }

    public DateOnly? Validity { get; set; }
}
