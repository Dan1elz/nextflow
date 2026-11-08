using Nextflow.Domain.Utils;

namespace Nextflow.Infrastructure.Seeders.Demo;

/// <summary>Fornecedor fictício. O múltiplo é a caixa de compra que o vínculo com o produto herda (RN-CE30).</summary>
public sealed record DemoSupplier(string Name, string Cnpj, int LeadTimeDays, decimal OrderMultiple);

/// <summary>Opção da grade: o valor do eixo, o sufixo do SKU e o preço e custo daquela variação.</summary>
public sealed record DemoOption(string Value, string SkuSuffix, decimal Price, decimal Cost);

/// <summary>Grade de um eixo só (tamanho ou sabor).</summary>
public sealed record DemoGrid(string Attribute, DemoOption[] Options);

/// <summary>
/// Produto do catálogo de demonstração. Sem <see cref="Grid"/>, o produto tem só a variação padrão, com <see cref="Code"/>
/// como SKU e <see cref="Price"/>/<see cref="Cost"/>. <see cref="MinStock"/> é o mínimo padrão (RN-CE33) e a base do saldo
/// de abertura; zero = item de temporada que, fora dela, abre sem saldo.
/// </summary>
public sealed record DemoProduct(
    string Code,
    string Name,
    string Brand,
    string Description,
    string Category,
    int SupplierIndex,
    decimal Price,
    decimal Cost,
    decimal MinStock,
    bool IsFood,
    string[] Events,
    DemoGrid? Grid = null);

/// <summary>Coleção do seed (RN-CE20): agrupa produtos de uma temporada ou linha; cada produto fica em uma só.</summary>
public sealed record DemoCollection(string Name, string[] ProductCodes);

/// <summary>Variação já expandida da grade, como o seed grava.</summary>
public sealed record DemoVariant(string Sku, string? Value, decimal Price, decimal Cost);

/// <summary>
/// Catálogo fictício da loja de doces e presentes (F1.11). Nomes, marcas e sazonalidade vêm do <c>DemoStoreCatalog</c> do
/// código antigo (branch <c>task-17</c>); fornecedores, CNPJs e marcas são inventados. Vendas e compras históricas ficam
/// para a F2.9.
/// </summary>
public static class DemoCatalog
{
    /// <summary>Marca do seed: existindo a categoria raiz, o catálogo já foi criado.</summary>
    public const string RootCategory = "Doces e Presentes";

    public const string DepositName = "Depósito";

    public static readonly string[] StoreNames = ["Doce Encanto Centro", "Doce Encanto Shopping"];

    /// <summary>Fração do saldo e do mínimo da loja principal que vale para a segunda loja, menor.</summary>
    public const decimal SecondStoreFactor = 0.6m;

    /// <summary>Vida útil dos alimentos (F1.7): o produto alimentício controla lote, e cada chegada vence neste prazo.</summary>
    public const int FoodShelfLifeDays = 180;

    /// <summary>Código do lote do saldo de abertura (um por variação, nos dois locais das duas lojas).</summary>
    public const string OpeningLotCode = "ABERTURA";

    /// <summary>
    /// O depósito demo guarda só o que não vence: o seed não transfere do depósito para o salão, e alimento parado lá
    /// venceria inteiro (lote "ABERTURA" vencido há anos na aba Lotes). Alimento fica só no salão, com mínimo zero no depósito.
    /// </summary>
    public static bool KeepsInDeposit(DemoProduct product) => !product.IsFood;

    /// <summary>Vida útil do produto: só os alimentos controlam lote.</summary>
    public static int? ShelfLifeDays(DemoProduct product) => product.IsFood ? FoodShelfLifeDays : null;

    /// <summary>Código do lote de uma chegada de compra: a data de recebimento ("C261107").</summary>
    public static string PurchaseLotCode(DateOnly receivedOn) => $"C{receivedOn:yyMMdd}";

    /// <summary>Coleções sem datas (a temporada fica nos eventos do calendário); o resto do catálogo fica sem coleção.</summary>
    public static readonly DemoCollection[] Collections =
    [
        new("Páscoa", ["DP-013", "DP-014", "DP-015", "DP-016"]),
        new("Natal", ["DP-021", "DP-022", "DP-023", "DP-024", "DP-041", "DP-046"]),
        new("Presentes Românticos", ["DP-044", "DP-045", "DP-049", "DP-047"]),
    ];

    /// <summary>Itens principais: a partir deste mínimo padrão, cada local ganha um mínimo próprio (RN-CE33).</summary>
    public const decimal MainItemMinStock = 20m;

    private const string Easter = CommercialCalendar.EasterName;
    private const string Mothers = CommercialCalendar.MothersDayName;
    private const string Valentines = CommercialCalendar.ValentinesDayName;
    private const string Christmas = CommercialCalendar.ChristmasName;

    public static readonly DemoSupplier[] Suppliers =
    [
        new("Doce Vale Distribuidora de Alimentos Ltda", Cnpj("112223330001"), 10, 12),
        new("Cacau Real Indústria de Chocolates S.A.", Cnpj("223334440001"), 15, 6),
        new("Casa Bellucci Importadora de Doces Finos", Cnpj("334445550001"), 25, 4),
        new("Sabor da Roça Doces Regionais Ltda", Cnpj("445556660001"), 7, 10),
        new("Ateliê Presente Perfeito Comércio de Presentes", Cnpj("556667770001"), 12, 1),
    ];

    public static readonly string[] Categories =
    [
        "Barras", "Bombons", "Ovos de Páscoa", "Trufas", "Panetones", "Achocolatados e Confeitaria", "Balas e Confeitos",
        "Doces Regionais", "Biscoitos", "Cestas e Kits", "Pelúcias e Canecas",
    ];

    public static readonly DemoProduct[] Products =
    [
        // Barras
        new("DP-001", "Barra Chocolate ao Leite 90g", "Cacau Real", "Barra de chocolate ao leite cremoso, 90g.", "Barras", 1, 8.90m, 5.10m, 40, true, [Easter, Christmas]),
        new("DP-002", "Barra Meio Amargo 70% 90g", "Cacau Real", "Barra de chocolate meio amargo 70% cacau, 90g.", "Barras", 1, 10.90m, 6.30m, 30, true, []),
        new("DP-003", "Barra Branca com Cookies 90g", "Doce Vale", "Barra de chocolate branco com pedaços de cookies, 90g.", "Barras", 0, 9.50m, 5.40m, 30, true, []),
        new("DP-004", "Barra ao Leite com Amendoim 90g", "Doce Vale", "Barra de chocolate ao leite com amendoim torrado, 90g.", "Barras", 0, 9.90m, 5.60m, 30, true, []),
        new("DP-005", "Barra 85% Cacau Intenso 80g", "Cacau Real", "Barra de chocolate amargo 85% cacau, 80g.", "Barras", 1, 13.90m, 7.90m, 20, true, []),
        new("DP-007", "Barra Zero Açúcar 80g", "Cacau Real", "Barra de chocolate ao leite sem adição de açúcar, 80g.", "Barras", 1, 12.90m, 7.40m, 15, true, []),

        // Bombons
        new("DP-008", "Caixa de Bombons Sortidos 250g", "Doce Vale", "Caixa com bombons sortidos, 250g.", "Bombons", 0, 19.90m, 11.20m, 40, true, [Mothers, Valentines, Christmas]),
        new("DP-009", "Caixa de Bombons Recheados (12 un.)", "Casa Bellucci", "Caixa com 12 bombons recheados.", "Bombons", 2, 39.90m, 22.00m, 15, true, [Mothers, Valentines, Christmas],
            new("Sabor", [new("Avelã", "AVE", 39.90m, 22.00m), new("Cereja ao licor", "CER", 39.90m, 22.40m), new("Maracujá", "MAR", 37.90m, 20.80m)])),
        new("DP-011", "Bombom de Coco 20g", "Doce Vale", "Bombom de chocolate ao leite recheado com coco, 20g.", "Bombons", 0, 2.90m, 1.40m, 60, true, []),
        new("DP-012", "Caixa Bombons Finos (18 un.)", "Casa Bellucci", "Seleção de 18 bombons finos de chocolate belga.", "Bombons", 2, 64.90m, 36.00m, 10, true, [Mothers, Christmas]),

        // Ovos de Páscoa: temporada, abrem sem saldo fora dela
        new("DP-013", "Ovo de Páscoa ao Leite", "Cacau Real", "Ovo de Páscoa de chocolate ao leite.", "Ovos de Páscoa", 1, 54.90m, 30.00m, 0, true, [Easter],
            new("Tamanho", [new("150g", "150", 29.90m, 16.00m), new("350g", "350", 54.90m, 30.00m), new("500g", "500", 74.90m, 41.00m)])),
        new("DP-014", "Ovo de Páscoa Trufado 400g", "Casa Bellucci", "Ovo de Páscoa trufado com casca recheada, 400g.", "Ovos de Páscoa", 2, 79.90m, 44.00m, 0, true, [Easter]),
        new("DP-015", "Ovo de Páscoa Infantil com Brinquedo 150g", "Doce Vale", "Ovo de Páscoa ao leite com brinquedo surpresa, 150g.", "Ovos de Páscoa", 0, 39.90m, 21.50m, 0, true, [Easter]),
        new("DP-016", "Mini Ovos Confeitados 100g", "Doce Vale", "Pacote de mini ovos de chocolate confeitados, 100g.", "Ovos de Páscoa", 0, 12.90m, 6.80m, 0, true, [Easter]),

        // Trufas
        new("DP-017", "Trufa Artesanal 30g", "Casa Bellucci", "Trufa artesanal de chocolate, 30g.", "Trufas", 2, 6.50m, 3.20m, 30, true, [Valentines, Christmas],
            new("Sabor", [new("Chocolate belga", "BEL", 6.50m, 3.20m), new("Maracujá", "MAR", 5.90m, 2.90m), new("Pistache", "PIS", 7.90m, 4.10m)])),
        new("DP-020", "Caixa de Trufas Sortidas (9 un.)", "Casa Bellucci", "Caixa com 9 trufas sortidas.", "Trufas", 2, 49.90m, 27.00m, 10, true, [Valentines, Christmas]),

        // Panetones
        new("DP-021", "Panetone Recheado de Chocolate 500g", "Cacau Real", "Panetone com gotas e recheio de chocolate, 500g.", "Panetones", 1, 34.90m, 19.00m, 20, true, [Christmas]),
        new("DP-022", "Chocotone Gotas de Chocolate 400g", "Doce Vale", "Chocotone com gotas de chocolate, 400g.", "Panetones", 0, 24.90m, 13.50m, 10, true, [Christmas]),
        new("DP-023", "Panetone de Frutas 500g", "Doce Vale", "Panetone tradicional com frutas cristalizadas e uvas-passas, 500g.", "Panetones", 0, 27.90m, 15.20m, 10, true, [Christmas]),
        new("DP-024", "Mini Chocotone 80g", "Cacau Real", "Mini chocotone individual com gotas de chocolate, 80g.", "Panetones", 1, 7.90m, 4.10m, 20, true, [Christmas]),

        // Achocolatados e confeitaria
        new("DP-025", "Achocolatado em Pó 400g", "Nobre Cacau", "Achocolatado em pó instantâneo, 400g.", "Achocolatados e Confeitaria", 0, 12.90m, 7.20m, 40, true, []),
        new("DP-026", "Cacau em Pó 50% 200g", "Nobre Cacau", "Cacau em pó 50% para receitas, 200g.", "Achocolatados e Confeitaria", 0, 15.90m, 9.10m, 20, true, []),
        new("DP-027", "Chocolate Quente Cremoso 200g", "Nobre Cacau", "Mistura para chocolate quente cremoso, 200g.", "Achocolatados e Confeitaria", 0, 14.50m, 8.10m, 20, true, []),
        new("DP-028", "Gotas de Chocolate Forneáveis 1kg", "Cacau Real", "Gotas de chocolate meio amargo que não derretem no forno, 1kg.", "Achocolatados e Confeitaria", 1, 39.90m, 23.00m, 10, true, []),

        // Balas e confeitos
        new("DP-030", "Bala de Goma Frutas 500g", "Doce Vale", "Balas de goma sabores de frutas, 500g.", "Balas e Confeitos", 0, 14.90m, 7.90m, 20, true, []),
        new("DP-031", "Pirulito Colorido (50 un.)", "Doce Vale", "Pacote com 50 pirulitos sortidos.", "Balas e Confeitos", 0, 18.90m, 10.20m, 15, true, []),
        new("DP-033", "Marshmallow Twist 250g", "Doce Vale", "Marshmallow torcido sabores morango e baunilha, 250g.", "Balas e Confeitos", 0, 9.90m, 5.10m, 15, true, []),
        new("DP-034", "Jujuba Sortida 1kg", "Doce Vale", "Jujubas sortidas, pacote de 1kg.", "Balas e Confeitos", 0, 19.90m, 10.40m, 15, true, []),

        // Doces regionais: fornecedor regional, prazo curto
        new("DP-035", "Paçoca Rolha (50 un.)", "Sabor da Roça", "Pote com 50 paçocas de amendoim tipo rolha.", "Doces Regionais", 3, 22.90m, 12.10m, 15, true, []),
        new("DP-036", "Pé de Moleque 500g", "Sabor da Roça", "Pé de moleque tradicional com amendoim, 500g.", "Doces Regionais", 3, 16.90m, 8.70m, 10, true, []),
        new("DP-038", "Doce de Leite em Tablete 500g", "Sabor da Roça", "Doce de leite em tabletes embalados individualmente, 500g.", "Doces Regionais", 3, 17.90m, 9.30m, 10, true, []),

        // Biscoitos
        new("DP-039", "Biscoito Wafer de Chocolate 140g", "Doce Vale", "Biscoito wafer recheado de chocolate, 140g.", "Biscoitos", 0, 4.90m, 2.50m, 40, true, []),
        new("DP-040", "Cookies com Gotas de Chocolate 200g", "Casa Bellucci", "Cookies amanteigados com gotas de chocolate, 200g.", "Biscoitos", 2, 11.90m, 6.20m, 25, true, []),
        new("DP-041", "Biscoito Amanteigado Lata 400g", "Casa Bellucci", "Biscoitos amanteigados dinamarqueses em lata decorada, 400g.", "Biscoitos", 2, 34.90m, 19.50m, 10, true, [Christmas]),
        new("DP-042", "Alfajor ao Leite 50g", "Doce Vale", "Alfajor recheado de doce de leite e coberto com chocolate ao leite, 50g.", "Biscoitos", 0, 5.50m, 2.80m, 40, true, []),

        // Cestas e kits
        new("DP-043", "Cesta Café da Manhã Premium", "Presente Perfeito", "Cesta com chocolates, biscoitos, geleia e caneca.", "Cestas e Kits", 4, 189.90m, 112.00m, 5, true, [Mothers]),
        new("DP-044", "Kit Dia das Mães: Bombons + Caneca", "Presente Perfeito", "Caixa de bombons com caneca de porcelana estampada.", "Cestas e Kits", 4, 79.90m, 44.00m, 0, true, [Mothers]),
        new("DP-045", "Kit Namorados: Trufas + Pelúcia", "Presente Perfeito", "Caixa de trufas com ursinho de pelúcia.", "Cestas e Kits", 4, 99.90m, 55.00m, 0, true, [Valentines]),
        new("DP-046", "Cesta de Natal Clássica", "Presente Perfeito", "Cesta com panetone, chocolates, biscoitos e espumante sem álcool.", "Cestas e Kits", 4, 229.90m, 135.00m, 5, true, [Christmas]),

        // Pelúcias e canecas: presentes que não vencem
        new("DP-049", "Urso de Pelúcia", "Presente Perfeito", "Urso de pelúcia antialérgico com laço de cetim.", "Pelúcias e Canecas", 4, 69.90m, 36.00m, 4, false, [Valentines, Christmas],
            new("Tamanho", [new("P", "P", 39.90m, 20.00m), new("M", "M", 69.90m, 36.00m), new("G", "G", 119.90m, 62.00m)])),
        new("DP-050", "Caneca de Porcelana Estampada", "Presente Perfeito", "Caneca de porcelana com estampa de corações.", "Pelúcias e Canecas", 4, 34.90m, 16.50m, 6, false, [Mothers, Christmas],
            new("Tamanho", [new("300 ml", "300", 34.90m, 16.50m), new("450 ml", "450", 44.90m, 21.00m)])),
        new("DP-047", "Caneca com Bombons Personalizada", "Presente Perfeito", "Caneca de porcelana com nome e 6 bombons.", "Pelúcias e Canecas", 4, 59.90m, 31.00m, 5, false, [Mothers, Valentines]),
    ];

    /// <summary>As variações do produto: a padrão, sem grade, ou uma por opção da grade.</summary>
    public static IReadOnlyList<DemoVariant> VariantsOf(DemoProduct product)
        => product.Grid is null
            ? [new DemoVariant(product.Code, null, product.Price, product.Cost)]
            : product.Grid.Options.Select(o => new DemoVariant($"{product.Code}-{o.SkuSuffix}", o.Value, o.Price, o.Cost)).ToList();

    /// <summary>Completa os dois dígitos verificadores de um CNPJ fictício.</summary>
    private static string Cnpj(string twelveDigits)
    {
        var digits = twelveDigits.Select(c => c - '0').ToList();
        foreach (var weights in new[] { new[] { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 }, new[] { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 } })
        {
            var rest = digits.Select((digit, index) => digit * weights[index]).Sum() % 11;
            digits.Add(rest < 2 ? 0 : 11 - rest);
        }
        return string.Concat(digits);
    }
}
