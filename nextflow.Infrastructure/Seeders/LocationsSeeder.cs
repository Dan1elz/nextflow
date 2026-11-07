using System.Text.Json;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Infrastructure.Seeders;

/// <summary>
/// Países, estados e os 5.570 municípios do IBGE (decisão 5 de 29/10), lidos de <c>Seeders/Data/locations.json</c>,
/// embutido no assembly. Com a tabela vazia carrega tudo num commit só. Com a tabela já carregada e alguma cidade sem
/// chave de busca (banco anterior à migration AddCitySearchName), acerta nome e chave de todas pelo código IBGE.
/// </summary>
public class LocationsSeeder(ICountryRepository countryRepository, ICityRepository cityRepository, IUnitOfWork unitOfWork) : ISeeder
{
    private const string ResourceName = "Nextflow.Infrastructure.Seeders.Data.locations.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public int Order => 20;

    public async Task SeedAsync(CancellationToken ct)
    {
        if (await countryRepository.Exists(_ => true, ct))
        {
            await SyncCityNames(ct);
            return;
        }

        var data = await Load(ct);
        var countries = data.Countries.Select(country =>
        {
            var entity = new Country(country.Name, country.AcronymIso, country.BacenCode);
            foreach (var state in country.States)
            {
                var stateEntity = new State(state.Name, state.Acronym, state.IbgeCode);
                foreach (var city in state.Cities)
                    stateEntity.Cities.Add(new City(city.Name, city.IbgeCode));
                entity.States.Add(stateEntity);
            }
            return entity;
        });

        await countryRepository.AddRangeAsync(countries, ct);
        await unitOfWork.CommitAsync(ct);
    }

    private async Task SyncCityNames(CancellationToken ct)
    {
        if (!await cityRepository.Exists(c => c.SearchName == string.Empty, ct))
            return;

        var data = await Load(ct);
        var names = data.Countries.SelectMany(c => c.States).SelectMany(s => s.Cities)
            .ToDictionary(c => c.IbgeCode, c => c.Name);

        foreach (var city in await cityRepository.GetAllForUpdate(_ => true, ct))
            city.Rename(names.GetValueOrDefault(city.IbgeCode, city.Name));

        await unitOfWork.CommitAsync(ct);
    }

    private static async Task<LocationsData> Load(CancellationToken ct)
    {
        await using var stream = typeof(LocationsSeeder).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Recurso '{ResourceName}' não encontrado no assembly.");

        return await JsonSerializer.DeserializeAsync<LocationsData>(stream, JsonOptions, ct)
            ?? throw new InvalidOperationException($"Recurso '{ResourceName}' está vazio.");
    }

    private sealed record LocationsData(List<CountryData> Countries);
    private sealed record CountryData(string Name, string AcronymIso, string? BacenCode, List<StateData> States);
    private sealed record StateData(string Name, string Acronym, string IbgeCode, List<CityData> Cities);
    private sealed record CityData(string Name, string IbgeCode);
}
