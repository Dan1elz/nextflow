using Microsoft.Extensions.Configuration;
using Nextflow.Domain.Dtos.Addresses;
using Nextflow.Domain.Dtos.Clients;
using Nextflow.Domain.Dtos.Contacts;
using Nextflow.Domain.Dtos.Users;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Interfaces.Infrastructure.Services;
using Nextflow.Domain.Models;
using Nextflow.Domain.ValueObjects;

namespace Nextflow.Infrastructure.Seeders.Demo;

/// <summary>
/// Cadastros do seed demo (só com <c>Seed:Demo:Enabled</c>): os clientes de <see cref="DemoRegistry"/> com telefone e
/// endereço principais, os vendedores de <see cref="DemoRegistry.Sellers"/> (só com o histórico ligado, vinculados às lojas) e o endereço principal
/// dos fornecedores do catálogo que ainda não têm nenhum. Roda depois do catálogo e antes do histórico, que liga parte das
/// vendas a esses clientes e reparte as vendas entre os vendedores. Cliente ou usuário já cadastrado com o mesmo CPF é mantido.
/// </summary>
public class DemoRegistrySeeder(
    IConfiguration configuration,
    IClientRepository clientRepository,
    ISupplierRepository supplierRepository,
    IAddressRepository addressRepository,
    IContactRepository contactRepository,
    ICityRepository cityRepository,
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IStoreMembership storeMembership,
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
        if (DemoHistorySeeder.HistoryMonths(configuration) > 0)
            await SeedSellers(ct);
        await unitOfWork.CommitAsync(ct);
    }

    /// <summary>
    /// Vendedores com senha aleatória (ninguém loga com eles: o seed demo também roda no staging) e vinculados às lojas
    /// ativas, como o <c>CreateUserUseCase</c>.
    /// </summary>
    private async Task SeedSellers(CancellationToken ct)
    {
        var documents = DemoRegistry.Sellers.Select(s => TaxDocument.ParseCpf(s.Cpf)).ToList();
        var emails = DemoRegistry.Sellers.Select(s => s.Email).ToList();
        var existing = await userRepository.GetAll(u => documents.Contains(u.Cpf) || emails.Contains(u.Email), 0, int.MaxValue, ct: ct);

        foreach (var demo in DemoRegistry.Sellers.Where((s, i) => !existing.Any(u => u.Email == s.Email || u.Cpf.Value == documents[i].Value)))
        {
            var dto = new UserRequestDto
            {
                Name = demo.Name,
                LastName = demo.LastName,
                Cpf = demo.Cpf,
                BirthDate = demo.BirthDate,
                Email = demo.Email,
                Password = Guid.NewGuid().ToString("N"),
            };
            var user = new User(dto, passwordHasher.Hash(dto.Password));
            await userRepository.AddAsync(user, ct);
            await storeMembership.LinkUser(user.Id, ct);
        }
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
