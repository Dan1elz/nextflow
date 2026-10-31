namespace Nextflow.Domain.Models;

/// <summary>Dado de referência, só leitura (decisão 5 de 29/10): nasce no seed e não tem CRUD.</summary>
public class Country : BaseModel
{
    public string Name { get; private set; } = string.Empty;
    public string AcronymIso { get; private set; } = string.Empty;
    public string? BacenCode { get; private set; }

    public virtual ICollection<State> States { get; private set; } = [];

    public override string Preposition => "o";
    public override string Singular => "país";
    public override string Plural => "países";

    private Country() { }

    public Country(string name, string acronymIso, string? bacenCode)
    {
        Name = name;
        AcronymIso = acronymIso;
        BacenCode = bacenCode;
    }
}
