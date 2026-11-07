

using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Database;

namespace Nextflow.Infrastructure.Seeders;

public class CountriesSeeder(AppDbContext context) : ISeeder
{
    public int Order => 20;

    public async Task SeedAsync(CancellationToken ct)
    {
        if (await context.Countries.AnyAsync(ct))
        {
            return;
        }

        var countries = new Country[]
        {
            new(new CreateCountryDto()
            {
                Name = "Estados Unidos",
                AcronymIso = "US",
                BacenCode = "001",
            }),
            new(new CreateCountryDto()
            {
                Name = "Brasil",
                AcronymIso = "BR",
                BacenCode = "076",
            })
            {
                States =
                [
                    new(new CreateStateDto()
                    {
                        Name = "Rondônia",
                        Acronym = "RO",
                        IbgeCode = "11",
                    }),
                    new(new CreateStateDto()
                    {
                        Name = "Acre",
                        Acronym = "AC",
                        IbgeCode = "12",
                    }),
                    new(new CreateStateDto()
                    {
                        Name = "Amazonas",
                        Acronym = "AM",
                        IbgeCode = "13",
                    }),
                    new(new CreateStateDto()
                    {
                        Name = "Roraima",
                        Acronym = "RR",
                        IbgeCode = "14",
                    }),
                    new(new CreateStateDto()
                    {
                        Name = "Pará",
                        Acronym = "PA",
                        IbgeCode = "15",
                    }),
                    new(new CreateStateDto()
                    {
                        Name = "Amapá",
                        Acronym = "AP",
                        IbgeCode = "16",
                    }),
                    new(new CreateStateDto()
                    {
                        Name = "Tocantins",
                        Acronym = "TO",
                        IbgeCode = "17",
                    }),
                    new(new CreateStateDto()
                    {
                        Name = "Maranhão",
                        Acronym = "MA",
                        IbgeCode = "21",
                    }),
                    new(new CreateStateDto()
                    {
                        Name = "Piauí",
                        Acronym = "PI",
                        IbgeCode = "22",
                    }),
                    new(new CreateStateDto()
                    {
                        Name = "Ceará",
                        Acronym = "CE",
                        IbgeCode = "23",
                    }),
                    new(new CreateStateDto()
                    {
                        Name = "Rio Grande do Norte",
                        Acronym = "RN",
                        IbgeCode = "24",
                    }),
                    new(new CreateStateDto()
                    {
                        Name = "Paraíba",
                        Acronym = "PB",
                        IbgeCode = "25",
                    }),
                    new(new CreateStateDto()
                    {
                        Name = "Pernambuco",
                        Acronym = "PE",
                        IbgeCode = "26",
                    }),
                    new(new CreateStateDto()
                    {
                        Name = "Alagoas",
                        Acronym = "AL",
                        IbgeCode = "27",
                    }),
                    new(new CreateStateDto()
                    {
                        Name = "Sergipe",
                        Acronym = "SE",
                        IbgeCode = "28",
                    }),
                    new(new CreateStateDto()
                    {
                        Name = "Bahia",
                        Acronym = "BA",
                        IbgeCode = "29",
                    }),
                    new(new CreateStateDto()
                    {
                        Name = "Minas Gerais",
                        Acronym = "MG",
                        IbgeCode = "31",
                    }),
                    new(new CreateStateDto()
                    {
                        Name = "Espírito Santo",
                        Acronym = "ES",
                        IbgeCode = "32",
                    }),
                    new(new CreateStateDto()
                    {
                        Name = "Rio de Janeiro",
                        Acronym = "RJ",
                        IbgeCode = "33",
                    }),
                    new(new CreateStateDto()
                    {
                        Name = "São Paulo",
                        Acronym = "SP",
                        IbgeCode = "35",
                    }),
                    new(new CreateStateDto()
                    {
                        Name = "Paraná",
                        Acronym = "PR",
                        IbgeCode = "41",
                    }),
                    new(new CreateStateDto()
                    {
                        Name = "Santa Catarina",
                        Acronym = "SC",
                        IbgeCode = "42",
                    }),
                    new(new CreateStateDto()
                    {
                        Name = "Rio Grande do Sul",
                        Acronym = "RS",
                        IbgeCode = "43",
                    }),
                    new(new CreateStateDto()
                    {
                        Name = "Mato Grosso do Sul",
                        Acronym = "MS",
                        IbgeCode = "50",
                    }),
                    new(new CreateStateDto()
                    {
                        Name = "Mato Grosso",
                        Acronym = "MT",
                        IbgeCode = "51",
                    }),
                    new(new CreateStateDto()
                    {
                        Name = "Goiás",
                        Acronym = "GO",
                        IbgeCode = "52",
                    }),
                    new(new CreateStateDto()
                    {
                        Name = "Distrito Federal",
                        Acronym = "DF",
                        IbgeCode = "53",
                    }),
                ],
            },

        };

        context.Countries.AddRange(countries);

        await context.SaveChangesAsync(ct);
    }
}