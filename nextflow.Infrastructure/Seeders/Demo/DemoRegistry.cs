namespace Nextflow.Infrastructure.Seeders.Demo;

/// <summary>Endereço fictício; a cidade é achada pelo código IBGE.</summary>
public sealed record DemoAddress(string ZipCode, string Street, string Number, string Neighborhood, string CityIbgeCode);

/// <summary>
/// Cliente fictício. <see cref="Weight"/> é a frequência relativa de compra no histórico (F2.9); zero = cadastrado e sem
/// compras, para a tela e o filtro "sem compras" terem caso.
/// </summary>
public sealed record DemoClient(string Name, string LastName, string Cpf, DateOnly BirthDate, string Phone, DemoAddress Address, int Weight);

/// <summary>Clientes e endereços dos fornecedores do seed demo: nomes, CPFs e endereços inventados.</summary>
public static class DemoRegistry
{
    /// <summary>Fração das vendas do histórico com cliente identificado (o resto é balcão anônimo).</summary>
    public const double IdentifiedSaleShare = 0.2;

    private const string SantaBarbara = "3545803";
    private const string Americana = "3501608";
    private const string Piracicaba = "3538709";
    private const string Campinas = "3509502";
    private const string Limeira = "3526902";
    private const string SaoPaulo = "3550308";

    /// <summary>Um endereço por fornecedor, na ordem de <see cref="DemoCatalog.Suppliers"/>.</summary>
    public static readonly DemoAddress[] SupplierAddresses =
    [
        new("13050000", "Rodovia Anhanguera, km 98", "1200", "Distrito Industrial", Campinas),
        new("03103000", "Rua do Cacau", "455", "Mooca", SaoPaulo),
        new("01310100", "Avenida Paulista", "2073", "Bela Vista", SaoPaulo),
        new("13400000", "Estrada da Serrinha", "88", "Zona Rural", Piracicaba),
        new("13465000", "Rua Fernando de Camargo", "910", "Centro", Americana),
    ];

    public static readonly DemoClient[] Clients =
    [
        new("Mariana", "Albuquerque", Cpf("381920475"), new(1988, 3, 14), "19991234501", new("13450021", "Rua Dona Margarida", "312", "Centro", SantaBarbara), 8),
        new("João Pedro", "Fernandes", Cpf("402719386"), new(1975, 11, 2), "19991234502", new("13456140", "Avenida Monte Castelo", "1500", "Jardim São Francisco", SantaBarbara), 6),
        new("Beatriz", "Lima", Cpf("519283746"), new(1995, 7, 21), "19991234503", new("13465000", "Rua Doutor Antônio Lobo", "48", "Centro", Americana), 5),
        new("Carlos Eduardo", "Moraes", Cpf("620394857"), new(1969, 1, 30), "19991234504", new("13418020", "Rua Governador Pedro de Toledo", "1020", "Centro", Piracicaba), 4),
        new("Fernanda", "Rocha", Cpf("731405968"), new(1990, 12, 8), "19991234505", new("13010111", "Rua Barão de Jaguara", "777", "Centro", Campinas), 4),
        new("Rafael", "Teixeira", Cpf("842516079"), new(1983, 5, 17), "19991234506", new("13480000", "Rua Santa Cruz", "250", "Centro", Limeira), 3),
        new("Luciana", "Barros", Cpf("953627180"), new(1979, 9, 25), "19991234507", new("13453000", "Rua do Ipiranga", "66", "Vila Pântano", SantaBarbara), 3),
        new("Gustavo", "Nogueira", Cpf("164738291"), new(2000, 2, 11), "19991234508", new("13467000", "Avenida Brasil", "3400", "Vila Santa Catarina", Americana), 2),
        new("Patrícia", "Campos", Cpf("275849302"), new(1986, 6, 3), "19991234509", new("13400100", "Avenida Independência", "900", "Alemães", Piracicaba), 2),
        new("Thiago", "Ribeiro", Cpf("386950413"), new(1992, 10, 19), "19991234510", new("13015000", "Avenida Francisco Glicério", "1400", "Centro", Campinas), 1),
        new("Camila", "Duarte", Cpf("497061524"), new(1998, 4, 27), "19991234511", new("13450000", "Rua Graça Martins", "15", "Centro", SantaBarbara), 0),
        new("Ricardo", "Pires", Cpf("508172635"), new(1972, 8, 9), "19991234512", new("13480100", "Rua Barão de Cascalho", "530", "Centro", Limeira), 0),
    ];

    private static string Cpf(string nineDigits)
    {
        var digits = nineDigits.Select(c => c - '0').ToList();
        for (var round = 0; round < 2; round++)
        {
            var weight = digits.Count + 1;
            var rest = digits.Select((digit, index) => digit * (weight - index)).Sum() % 11;
            digits.Add(rest < 2 ? 0 : 11 - rest);
        }
        return string.Concat(digits);
    }
}
