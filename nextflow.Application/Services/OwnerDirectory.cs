using Nextflow.Domain.Dtos.Addresses;
using Nextflow.Domain.Dtos.Contacts;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;

namespace Nextflow.Application.Services;

public class OwnerDirectory(
    IClientRepository clientRepository,
    ISupplierRepository supplierRepository,
    IAddressRepository addressRepository,
    ICityRepository cityRepository,
    IContactRepository contactRepository) : IOwnerDirectory
{
    public Task<bool> Exists(OwnerTypeEnum ownerType, Guid ownerId, CancellationToken ct = default)
        => ownerType == OwnerTypeEnum.Client
            ? clientRepository.Exists(c => c.Id == ownerId, ct)
            : supplierRepository.Exists(s => s.Id == ownerId, ct);

    public Task<bool> IsActive(OwnerTypeEnum ownerType, Guid ownerId, CancellationToken ct = default)
        => ownerType == OwnerTypeEnum.Client
            ? clientRepository.Exists(c => c.Id == ownerId && c.IsActive, ct)
            : supplierRepository.Exists(s => s.Id == ownerId && s.IsActive, ct);

    public async Task<IReadOnlyDictionary<Guid, OwnerSummaryDto>> GetSummaries(
        OwnerTypeEnum ownerType, IReadOnlyCollection<Guid> ownerIds, CancellationToken ct = default)
    {
        if (ownerIds.Count == 0)
            return new Dictionary<Guid, OwnerSummaryDto>();

        var cityStates = await addressRepository.GetMainCityStates(ownerType, ownerIds, ct);
        var contacts = (await contactRepository.GetByOwners(ownerType, ownerIds, ct)).ToLookup(c => c.OwnerId);

        return ownerIds.Distinct().ToDictionary(id => id, id => new OwnerSummaryDto(
            cityStates.GetValueOrDefault(id),
            contacts[id].FirstOrDefault(c => c.IsMain)?.Value,
            contacts[id].FirstOrDefault(c => c.IsMain && c.IsPhone)?.Value));
    }

    public async Task<AddressResponseDto?> GetMainAddress(OwnerTypeEnum ownerType, Guid ownerId, CancellationToken ct = default)
    {
        var address = await addressRepository.Get(a => a.OwnerType == ownerType && a.OwnerId == ownerId && a.IsMain, ct: ct);

        return address is null ? null : new AddressResponseDto(address, await cityRepository.GetSummary(address.CityId, ct));
    }

    public async Task<ContactResponseDto?> GetMainContact(OwnerTypeEnum ownerType, Guid ownerId, CancellationToken ct = default)
    {
        var contact = await contactRepository.Get(c => c.OwnerType == ownerType && c.OwnerId == ownerId && c.IsMain, ct: ct);
        return contact is null ? null : new ContactResponseDto(contact);
    }
}
