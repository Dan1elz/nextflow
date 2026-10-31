namespace Nextflow.Domain.Dtos.Auth;

/// <summary>Loja do usuário. <see cref="LocationCount"/> conta os locais ativos, para o seletor do topo (RN-CE12).</summary>
public class CurrentUserStoreDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int LocationCount { get; set; }

    public CurrentUserStoreDto() { }

    public CurrentUserStoreDto(Guid id, string name, int locationCount)
    {
        Id = id;
        Name = name;
        LocationCount = locationCount;
    }
}
