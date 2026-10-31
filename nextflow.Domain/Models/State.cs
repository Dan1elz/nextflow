namespace Nextflow.Domain.Models;

/// <summary>Dado de referência, só leitura (decisão 5 de 29/10).</summary>
public class State : BaseModel
{
    public string Name { get; private set; } = string.Empty;
    public string Acronym { get; private set; } = string.Empty;
    public string IbgeCode { get; private set; } = string.Empty;
    public Guid CountryId { get; private set; }

    public virtual Country Country { get; private set; } = null!;
    public virtual ICollection<City> Cities { get; private set; } = [];

    public override string Preposition => "o";
    public override string Singular => "estado";
    public override string Plural => "estados";

    private State() { }

    public State(string name, string acronym, string ibgeCode)
    {
        Name = name;
        Acronym = acronym;
        IbgeCode = ibgeCode;
    }
}
