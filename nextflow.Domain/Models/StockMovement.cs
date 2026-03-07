using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Models.Base;

namespace Nextflow.Domain.Models;

[Table("stock_movements")]
public class StockMovement : BaseModel
{
    [ForeignKey("product"), Required(ErrorMessage = "O Produto é obrigatório.")]
    public Guid ProductId { get; private set; }
    public virtual Product? Product { get; set; }

    [Required(ErrorMessage = "A quantidade é obrigatória."), Range(0.01, double.MaxValue, ErrorMessage = "A quantidade deve ser maior que zero.")]
    public double Quantity { get; private set; }

    [Required(ErrorMessage = "O tipo de movimento é obrigatório.")]
    public MovementType MovementType { get; private set; }

    [StringLength(255, ErrorMessage = "A descrição não pode exceder 255 caracteres.")]
    public string? Description { get; private set; } = string.Empty;

    [ForeignKey("user"), Required(ErrorMessage = "O Usuário é obrigatório.")]
    public Guid UserId { get; private set; }
    public virtual User? User { get; set; }

    [Required(ErrorMessage = "A cotação é obrigatória."), Range(0.0, double.MaxValue, ErrorMessage = "A cotação não pode ser negativa.")]
    public decimal Quote { get; private set; }

    public override string Preposition => "a";
    public override string Singular => "movimentação de estoque";
    public override string Plural => "movimentações de estoque";

    private StockMovement() : base() { }

    public StockMovement(CreateStockMovementDto dto) : base()
    {
        ProductId = dto.ProductId;
        Quantity = dto.Quantity;
        MovementType = dto.MovementType;
        Description = dto.Description;
        UserId = dto.UserId;
        Quote = dto.Quote ?? 0;
    }

    public void SetQuote(decimal quote)
    {
        Quote = quote;
    }
}
