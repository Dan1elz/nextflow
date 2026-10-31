using Nextflow.Domain.Dtos.Addresses;
using Nextflow.Domain.Dtos.Contacts;
using Nextflow.Domain.Enums;

namespace Nextflow.Domain.Interfaces.Application.Services;

/// <summary>
/// Liga clientes e fornecedores aos endereços e contatos por consulta separada: a referência é polimórfica, sem FK nem
/// navegação (DOMAIN_LEVELS, RN-CD05).
/// </summary>
public interface IOwnerDirectory
{
    Task<bool> Exists(OwnerTypeEnum ownerType, Guid ownerId, CancellationToken ct = default);
    Task<bool> IsActive(OwnerTypeEnum ownerType, Guid ownerId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, OwnerSummaryDto>> GetSummaries(OwnerTypeEnum ownerType, IReadOnlyCollection<Guid> ownerIds, CancellationToken ct = default);
    Task<AddressResponseDto?> GetMainAddress(OwnerTypeEnum ownerType, Guid ownerId, CancellationToken ct = default);
    Task<ContactResponseDto?> GetMainContact(OwnerTypeEnum ownerType, Guid ownerId, CancellationToken ct = default);
}
