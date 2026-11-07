using Nextflow.Domain.Utils;

namespace Nextflow.Domain.Models;

/// <summary>Dado de referência, só leitura (decisão 5 de 29/10): municípios do IBGE.</summary>
public class City : BaseModel
{
    public string Name { get; private set; } = string.Empty;
    public string IbgeCode { get; private set; } = string.Empty;
    public Guid StateId { get; private set; }

    /// <summary>Chave de busca do nome (<see cref="SearchKey"/>): sem acento, caixa, espaço nem pontuação.</summary>
    public string SearchName { get; private set; } = string.Empty;

    public virtual State State { get; private set; } = null!;

    public override string Preposition => "a";
    public override string Singular => "cidade";
    public override string Plural => "cidades";

    private City() { }

    public City(string name, string ibgeCode)
    {
        IbgeCode = ibgeCode;
        Rename(name);
    }

    /// <summary>Acerta o nome pela base do IBGE (o seeder corrige bancos já carregados) e recalcula a chave de busca.</summary>
    public void Rename(string name)
    {
        Name = name;
        SearchName = SearchKey.From(name);
    }
}
