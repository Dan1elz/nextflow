namespace Nextflow.Domain.Dtos.Orders;

/// <summary>Referência curta (loja, local, vendedor) no detalhe do pedido.</summary>
public class OrderRefDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public OrderRefDto() { }

    public OrderRefDto(Guid id, string name)
    {
        Id = id;
        Name = name;
    }
}
