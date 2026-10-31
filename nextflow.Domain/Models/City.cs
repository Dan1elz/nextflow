namespace Nextflow.Domain.Models;

/// <summary>Dado de referência, só leitura (decisão 5 de 29/10): municípios do IBGE.</summary>
public class City : BaseModel
{
    public string Name { get; private set; } = string.Empty;
    public string IbgeCode { get; private set; } = string.Empty;
    public Guid StateId { get; private set; }

    public virtual State State { get; private set; } = null!;

    public override string Preposition => "a";
    public override string Singular => "cidade";
    public override string Plural => "cidades";

    private City() { }

    public City(string name, string ibgeCode)
    {
        Name = name;
        IbgeCode = ibgeCode;
    }
}
