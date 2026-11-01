namespace Nextflow.Domain.Models;

/// <summary>Evento que puxa a venda do produto (RN-CE21). A antecedência fica no evento, não aqui (RN-CE47, RN-CE61).</summary>
public class ProductEvent : BaseModel
{
    public Guid ProductId { get; private set; }
    public Guid EventId { get; private set; }

    public virtual Product Product { get; private set; } = null!;
    public virtual CalendarEvent Event { get; private set; } = null!;

    public override string Preposition => "o";
    public override string Singular => "vínculo de evento";
    public override string Plural => "vínculos de evento";

    private ProductEvent() { }

    public ProductEvent(Guid productId, Guid eventId)
    {
        ProductId = productId;
        EventId = eventId;
    }
}
