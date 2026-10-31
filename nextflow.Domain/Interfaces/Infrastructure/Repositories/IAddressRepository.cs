using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Interfaces.Infrastructure.Repositories;

public interface IAddressRepository : IBaseRepository<Address>
{
    /// <summary>"Cidade/UF" do endereço principal de cada dono da lista: o dono não tem navegação para o endereço.</summary>
    Task<Dictionary<Guid, string>> GetMainCityStates(OwnerTypeEnum ownerType, IReadOnlyCollection<Guid> ownerIds, CancellationToken ct = default);

    Task<List<Guid>> GetOwnerIdsByCity(OwnerTypeEnum ownerType, Guid cityId, CancellationToken ct = default);
}
