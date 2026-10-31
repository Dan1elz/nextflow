using System.Text.Json;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Infrastructure.Seeders;

/// <summary>
/// Países, estados e os 5.570 municípios do IBGE (decisão 5 de 29/10), lidos de <c>Seeders/Data/locations.json</c>,
/// embutido no assembly. Só roda com a tabela vazia, e tudo entra num commit só.
/// </summary>
public class LocationsSeeder(ICountryRepository countryRepository, IUnitOfWork unitOfWork) : ISeeder
{
    private const string ResourceName = "Nextflow.Infrastructure.Seeders.Data.locations.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public int Order => 20;

    public async Task SeedAsync(CancellationToken ct)
    {
        if (await countryRepository.Exists(_ => true, ct))
            return;

        await using var stream = typeof(LocationsSeeder).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Recurso '{ResourceName}' não encontrado no assembly.");

        var data = await JsonSerializer.DeserializeAsync<LocationsData>(stream, JsonOptions, ct)
            ?? throw new InvalidOperationException($"Recurso '{ResourceName}' está vazio.");

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

    private sealed record LocationsData(List<CountryData> Countries);
    private sealed record CountryData(string Name, string AcronymIso, string? BacenCode, List<StateData> States);
    private sealed record StateData(string Name, string Acronym, string IbgeCode, List<CityData> Cities);
    private sealed record CityData(string Name, string IbgeCode);
}
