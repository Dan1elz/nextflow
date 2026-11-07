using Microsoft.Extensions.Configuration;
using Nextflow.Domain.Dtos.Addresses;
using Nextflow.Domain.Dtos.Clients;
using Nextflow.Domain.Dtos.Contacts;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Domain.ValueObjects;

namespace Nextflow.Infrastructure.Seeders.Demo;

/// <summary>
/// Cadastros do seed demo (só com <c>Seed:Demo:Enabled</c>): os clientes de <see cref="DemoRegistry"/> com telefone e
/// endereço principais, e o endereço principal dos fornecedores do catálogo que ainda não têm nenhum. Roda depois do
/// catálogo e antes do histórico, que liga parte das vendas a esses clientes. Cliente já cadastrado com o mesmo CPF é
/// mantido.
/// </summary>
public class DemoRegistrySeeder(
    IConfiguration configuration,
    IClientRepository clientRepository,
    ISupplierRepository supplierRepository,
    IAddressRepository addressRepository,
    IContactRepository contactRepository,
    ICityRepository cityRepository,
    IUnitOfWork unitOfWork) : ISeeder
{
    public int Order => 55;

    public async Task SeedAsync(CancellationToken ct)
    {
        if (!bool.TryParse(configuration[DemoCatalogSeeder.EnabledKey], out var enabled) || !enabled)
            return;

        var codes = DemoRegistry.Clients.Select(c => c.Address.CityIbgeCode)
            .Concat(DemoRegistry.SupplierAddresses.Select(a => a.CityIbgeCode)).Distinct().ToList();
        var cities = (await cityRepository.GetAll(c => codes.Contains(c.IbgeCode), 0, int.MaxValue, ct: ct))
            .ToDictionary(c => c.IbgeCode, c => c.Id);
        if (cities.Count == 0)
            return;

        await SeedClients(cities, ct);
        await SeedSupplierAddresses(cities, ct);
        await unitOfWork.CommitAsync(ct);
    }

    private async Task SeedClients(Dictionary<string, Guid> cities, CancellationToken ct)
    {
        var documents = DemoRegistry.Clients.Select(c => TaxDocument.ParseCpf(c.Cpf)).ToList();
        var existing = (await clientRepository.GetAll(c => c.Cpf != null && documents.Contains(c.Cpf), 0, int.MaxValue, ct: ct))
            .Select(c => c.Cpf!.Value).ToHashSet();

        foreach (var demo in DemoRegistry.Clients.Where((_, i) => !existing.Contains(documents[i].Value)))
        {
            var client = new Client(new ClientRequestDto
            {
                Name = demo.Name,
                LastName = demo.LastName,
                Cpf = demo.Cpf,
                BirthDate = demo.BirthDate,
            });
            await clientRepository.AddAsync(client, ct);
            await contactRepository.AddAsync(new Contact(new ContactRequestDto
            {
                OwnerType = OwnerTypeEnum.Client,
                OwnerId = client.Id,
                Type = ContactTypeEnum.Phone,
                Value = demo.Phone,
                IsMain = true,
            }), ct);
            await AddAddress(OwnerTypeEnum.Client, client.Id, demo.Address, cities, ct);
        }
    }

    private async Task SeedSupplierAddresses(Dictionary<string, Guid> cities, CancellationToken ct)
    {
        var documents = DemoCatalog.Suppliers.Select(s => TaxDocument.ParseCnpj(s.Cnpj)).ToList();
        var suppliers = (await supplierRepository.GetAll(s => documents.Contains(s.Cnpj), 0, int.MaxValue, ct: ct))
            .ToDictionary(s => s.Cnpj.Value, s => s.Id);

        for (var i = 0; i < DemoCatalog.Suppliers.Length && i < DemoRegistry.SupplierAddresses.Length; i++)
        {
            if (!suppliers.TryGetValue(documents[i].Value, out var supplierId))
                continue;
            if (await addressRepository.Exists(a => a.OwnerType == OwnerTypeEnum.Supplier && a.OwnerId == supplierId, ct))
                continue;
            await AddAddress(OwnerTypeEnum.Supplier, supplierId, DemoRegistry.SupplierAddresses[i], cities, ct);
        }
    }

    private async Task AddAddress(OwnerTypeEnum ownerType, Guid ownerId, DemoAddress address, Dictionary<string, Guid> cities,
        CancellationToken ct)
    {
        if (!cities.TryGetValue(address.CityIbgeCode, out var cityId))
            return;

        await addressRepository.AddAsync(new Address(new AddressRequestDto
        {
            OwnerType = ownerType,
            OwnerId = ownerId,
            ZipCode = address.ZipCode,
            Street = address.Street,
            Number = address.Number,
            Neighborhood = address.Neighborhood,
            CityId = cityId,
            IsMain = true,
        }), ct);
    }
}
