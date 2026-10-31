namespace Nextflow.Domain.Dtos.Suppliers;

public class SupplierRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string Cnpj { get; set; } = string.Empty;

    /// <summary>Prazo padrão de entrega, de 1 a 180 dias (RN-CD03).</summary>
    public int LeadTimeDays { get; set; } = 7;

    public string? Note { get; set; }
}
