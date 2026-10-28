namespace Nextflow.Infrastructure.Seeders.Demo;

/// <summary>Sazonalidade de cada linha de produto (ver <see cref="DemoDemandModel"/>).</summary>
internal enum DemandProfile
{
    Bar,             // ano todo, alta na Páscoa e no Natal, queda jan–fev
    BonBon,          // picos Dia das Mães, Namorados e Natal
    EasterEgg,       // só ~6 semanas antes da Páscoa
    Truffle,         // Namorados e Natal
    Panettone,       // só nov–dez
    ChocolateDrink,  // alta no inverno (jun–ago), baixa no verão
    CocoaPowder,     // confeitaria: Páscoa, inverno e Natal
    Candy,           // Dia das Crianças e Páscoa
    Junina,          // festas juninas (pico em junho)
    Biscuit,         // estável, leve alta no inverno e no Natal
    GiftBasket,      // presentes: Mães, Namorados, Pais e Natal
    MothersKit,      // só nas semanas antes do Dia das Mães
    ValentinesKit,   // só nas semanas antes do Dia dos Namorados
    ChristmasHamper  // só nov–dez, pico até a véspera
}

/// <summary>
/// Situação que o produto deve mostrar na banca. Define a tendência, o lançamento e a política de reposição na simulação;
/// a recomendação final (Comprar/Promover/Manter) sai do motor de previsão, não daqui.
/// </summary>
internal enum DemoScenario
{
    Maintain,   // reposição normal: estoque equilibrado
    Buy,        // última reposição atrasada: chega no pico com pouco estoque
    Overstock,  // última compra exagerada entrando na baixa temporada
    NearExpiry, // validade próxima com estoque acima da demanda
    New,        // lançado há ~100 dias: histórico curto
    Rising,     // demanda crescendo forte (~60% a.a.)
    Falling,    // demanda caindo (~-35% a.a.)
    Stockout    // ruptura histórica: fornecedor falhou e o produto ficou semanas zerado
}

internal sealed record DemoSupplier(string Name, int LeadTimeDays);

internal sealed record DemoProduct(
    string Code,
    string Name,
    string Brand,
    string Description,
    string Category,
    int SupplierIndex,
    decimal Price,
    decimal Cost,
    decimal MinStock,
    int? ShelfLifeDays,      // null = sem validade
    DemandProfile Profile,
    double BaseMonthly,      // un./mês (EasterEgg/kits sazonais: total da temporada; Panettone/ChristmasHamper: un./mês em novembro)
    int MaxUnitsPerLine,
    DemoScenario Scenario,
    string PhotoId);         // id da foto no Unsplash (images.unsplash.com/photo-...), usada pelo ProductImagesSeeder

internal sealed record DemoPastPromotion(string ProductCode, string Reason, decimal DiscountPercent, DemoPromotionWindow Window);

internal enum DemoPromotionWindow { BlackFriday, SummerClearance, ChildrensDay, PostEaster }

/// <summary>
/// Catálogo fictício da loja de doces e presentes. Marcas, fornecedores e clientes são inventados;
/// as fotos são do Unsplash (licença livre).
/// </summary>
internal static class DemoStoreCatalog
{
    public const string RootCategory = "Doces e Presentes";

    public static readonly DemoSupplier[] Suppliers =
    [
        new("Doce Vale Distribuidora de Alimentos Ltda", 10),
        new("Cacau Real Indústria de Chocolates S.A.", 15),
        new("Casa Bellucci Importadora de Doces Finos", 25),
        new("Sabor da Roça Doces Regionais Ltda", 7),
        new("Ateliê Presente Perfeito Comércio de Presentes", 12),
    ];

    public static readonly string[] Categories =
    [
        RootCategory, "Barras", "Bombons", "Ovos de Páscoa", "Trufas", "Panetones", "Achocolatados e Confeitaria",
        "Balas e Confeitos", "Doces Juninos", "Biscoitos", "Cestas e Kits"
    ];

    public static readonly DemoProduct[] Products =
    [
        // Barras
        new("DP-001", "Barra Chocolate ao Leite 90g", "Cacau Real", "Barra de chocolate ao leite cremoso, 90g.", "Barras", 1, 8.90m, 5.10m, 40, 240, DemandProfile.Bar, 300, 12, DemoScenario.Maintain, "1623660053975-cf75a8be0908"),
        new("DP-002", "Barra Meio Amargo 70% 90g", "Cacau Real", "Barra de chocolate meio amargo 70% cacau, 90g.", "Barras", 1, 10.90m, 6.30m, 30, 240, DemandProfile.Bar, 170, 10, DemoScenario.Maintain, "1610450949065-1f2841536c88"),
        new("DP-003", "Barra Branca com Cookies 90g", "Doce Vale", "Barra de chocolate branco com pedaços de cookies, 90g.", "Barras", 0, 9.50m, 5.40m, 30, 210, DemandProfile.Bar, 150, 10, DemoScenario.Buy, "1627647227768-705244233b56"),
        new("DP-004", "Barra ao Leite com Amendoim 90g", "Doce Vale", "Barra de chocolate ao leite com amendoim torrado, 90g.", "Barras", 0, 9.90m, 5.60m, 30, 210, DemandProfile.Bar, 90, 10, DemoScenario.Rising, "1679143121360-d2f8e948e996"),
        new("DP-005", "Barra 85% Cacau Intenso 80g", "Cacau Real", "Barra de chocolate amargo 85% cacau, 80g.", "Barras", 1, 13.90m, 7.90m, 20, 300, DemandProfile.Bar, 70, 8, DemoScenario.Rising, "1614088685112-0a760b71a3c8"),
        new("DP-006", "Barra Recheada de Morango 100g", "Doce Vale", "Barra de chocolate ao leite recheada com creme de morango, 100g.", "Barras", 0, 8.50m, 4.80m, 30, 180, DemandProfile.Bar, 160, 10, DemoScenario.Falling, "1646902823849-1aa943a17f46"),
        new("DP-007", "Barra Zero Açúcar 80g", "Cacau Real", "Barra de chocolate ao leite sem adição de açúcar, 80g.", "Barras", 1, 12.90m, 7.40m, 15, 240, DemandProfile.Bar, 90, 6, DemoScenario.New, "1592290216331-620cbbbe0bce"),

        // Bombons
        new("DP-008", "Caixa de Bombons Sortidos 250g", "Doce Vale", "Caixa com bombons sortidos, 250g.", "Bombons", 0, 19.90m, 11.20m, 40, 150, DemandProfile.BonBon, 200, 6, DemoScenario.Buy, "1687795097254-f019f9d7fd17"),
        new("DP-009", "Bombom Recheado de Avelã (12 un.)", "Casa Bellucci", "Caixa com 12 bombons recheados de creme de avelã.", "Bombons", 2, 39.90m, 22.00m, 15, 180, DemandProfile.BonBon, 70, 4, DemoScenario.Maintain, "1542843137-8791a6904d14"),
        new("DP-010", "Bombom Licor de Cereja (8 un.)", "Casa Bellucci", "Caixa com 8 bombons recheados com licor de cereja.", "Bombons", 2, 32.90m, 18.50m, 15, 180, DemandProfile.BonBon, 50, 3, DemoScenario.Buy, "1481391319762-47dff72954d9"),
        new("DP-011", "Bombom de Coco 20g", "Doce Vale", "Bombom de chocolate ao leite recheado com coco, 20g.", "Bombons", 0, 2.90m, 1.40m, 60, 120, DemandProfile.BonBon, 260, 20, DemoScenario.Stockout, "1656821991459-ae723d90d648"),
        new("DP-012", "Caixa Bombons Finos (18 un.)", "Casa Bellucci", "Seleção de 18 bombons finos de chocolate belga.", "Bombons", 2, 64.90m, 36.00m, 10, 180, DemandProfile.BonBon, 35, 3, DemoScenario.Maintain, "1769775264026-fbc4e974dea8"),

        // Ovos de Páscoa
        new("DP-013", "Ovo de Páscoa ao Leite 350g", "Cacau Real", "Ovo de Páscoa de chocolate ao leite, 350g.", "Ovos de Páscoa", 1, 54.90m, 30.00m, 0, 200, DemandProfile.EasterEgg, 900, 4, DemoScenario.Overstock, "1521905621926-7c52537741be"),
        new("DP-014", "Ovo de Páscoa Trufado 400g", "Casa Bellucci", "Ovo de Páscoa trufado com casca recheada, 400g.", "Ovos de Páscoa", 2, 79.90m, 44.00m, 0, 200, DemandProfile.EasterEgg, 420, 3, DemoScenario.Overstock, "1648022386629-224c9b86e2f9"),
        new("DP-015", "Ovo de Páscoa Infantil com Brinquedo 150g", "Doce Vale", "Ovo de Páscoa ao leite com brinquedo surpresa, 150g.", "Ovos de Páscoa", 0, 39.90m, 21.50m, 0, 200, DemandProfile.EasterEgg, 650, 3, DemoScenario.Maintain, "1559142843-5b8fcb9de0af"),
        new("DP-016", "Mini Ovos Confeitados 100g", "Doce Vale", "Pacote de mini ovos de chocolate confeitados, 100g.", "Ovos de Páscoa", 0, 12.90m, 6.80m, 0, 240, DemandProfile.EasterEgg, 800, 8, DemoScenario.Maintain, "1522742034131-3dca7544197c"),

        // Trufas
        new("DP-017", "Trufa de Chocolate Belga 30g", "Casa Bellucci", "Trufa artesanal de chocolate belga, 30g.", "Trufas", 2, 6.50m, 3.20m, 50, 90, DemandProfile.Truffle, 230, 15, DemoScenario.Buy, "1769433509476-bd68b1229916"),
        new("DP-018", "Trufa de Maracujá 30g", "Doce Vale", "Trufa de chocolate com recheio de maracujá, 30g.", "Trufas", 0, 5.90m, 2.90m, 30, 45, DemandProfile.Truffle, 170, 15, DemoScenario.NearExpiry, "1639158924965-7be3bb57506b"),
        new("DP-019", "Trufa de Pistache 30g", "Casa Bellucci", "Trufa de chocolate branco com creme de pistache, 30g.", "Trufas", 2, 7.90m, 4.10m, 30, 90, DemandProfile.Truffle, 80, 10, DemoScenario.Rising, "1765946025181-8b7a16ab93f9"),
        new("DP-020", "Caixa de Trufas Sortidas (9 un.)", "Casa Bellucci", "Caixa com 9 trufas sortidas.", "Trufas", 2, 49.90m, 27.00m, 10, 90, DemandProfile.Truffle, 45, 3, DemoScenario.Maintain, "1788521042909-3a7f486d0321"),

        // Panetones
        new("DP-021", "Panetone Recheado de Chocolate 500g", "Cacau Real", "Panetone com gotas e recheio de chocolate, 500g.", "Panetones", 1, 34.90m, 19.00m, 20, 120, DemandProfile.Panettone, 340, 5, DemoScenario.Buy, "1762304887845-0eef96543910"),
        new("DP-022", "Chocotone Gotas de Chocolate 400g", "Doce Vale", "Chocotone com gotas de chocolate, 400g.", "Panetones", 0, 24.90m, 13.50m, 10, 120, DemandProfile.Panettone, 200, 5, DemoScenario.Maintain, "1762304887659-7d4127c1e2e6"),
        new("DP-023", "Panetone de Frutas 500g", "Doce Vale", "Panetone tradicional com frutas cristalizadas e uvas-passas, 500g.", "Panetones", 0, 27.90m, 15.20m, 10, 120, DemandProfile.Panettone, 180, 5, DemoScenario.Falling, "1762304887990-36fa522ae0b6"),
        new("DP-024", "Mini Chocotone 80g", "Cacau Real", "Mini chocotone individual com gotas de chocolate, 80g.", "Panetones", 1, 7.90m, 4.10m, 20, 120, DemandProfile.Panettone, 260, 10, DemoScenario.Maintain, "1762304887826-88a67a8f4a71"),

        // Achocolatados e confeitaria
        new("DP-025", "Achocolatado em Pó 400g", "Nobre Cacau", "Achocolatado em pó instantâneo, 400g.", "Achocolatados e Confeitaria", 0, 12.90m, 7.20m, 40, 300, DemandProfile.ChocolateDrink, 250, 12, DemoScenario.Overstock, "1637572815755-c4b80092dce1"),
        new("DP-026", "Cacau em Pó 50% 200g", "Nobre Cacau", "Cacau em pó 50% para receitas, 200g.", "Achocolatados e Confeitaria", 0, 15.90m, 9.10m, 20, 365, DemandProfile.CocoaPowder, 110, 6, DemoScenario.Maintain, "1770123366817-743776f66d6a"),
        new("DP-027", "Chocolate Quente Cremoso 200g", "Nobre Cacau", "Mistura para chocolate quente cremoso, 200g.", "Achocolatados e Confeitaria", 0, 14.50m, 8.10m, 20, 300, DemandProfile.ChocolateDrink, 100, 6, DemoScenario.Maintain, "1702648906616-b030f193edb9"),
        new("DP-028", "Gotas de Chocolate Forneáveis 1kg", "Cacau Real", "Gotas de chocolate meio amargo que não derretem no forno, 1kg.", "Achocolatados e Confeitaria", 1, 39.90m, 23.00m, 10, 365, DemandProfile.CocoaPowder, 55, 4, DemoScenario.Rising, "1728310335347-082140001938"),
        new("DP-029", "Granulado de Chocolate 500g", "Nobre Cacau", "Granulado de chocolate para confeitaria, 500g.", "Achocolatados e Confeitaria", 0, 11.90m, 6.30m, 20, 365, DemandProfile.CocoaPowder, 90, 6, DemoScenario.Maintain, "1633679535523-fae61171f5bc"),

        // Balas e confeitos
        new("DP-030", "Bala de Goma Frutas 500g", "Doce Vale", "Balas de goma sabores de frutas, 500g.", "Balas e Confeitos", 0, 14.90m, 7.90m, 20, 270, DemandProfile.Candy, 130, 6, DemoScenario.Maintain, "1635342219731-4ae2bf39e1e9"),
        new("DP-031", "Pirulito Colorido (50 un.)", "Doce Vale", "Pacote com 50 pirulitos sortidos.", "Balas e Confeitos", 0, 18.90m, 10.20m, 15, 365, DemandProfile.Candy, 90, 4, DemoScenario.Falling, "1687499466474-df90c4d7a54a"),
        new("DP-032", "Confeito de Chocolate Colorido 500g", "Cacau Real", "Confeitos de chocolate ao leite cobertos de açúcar colorido, 500g.", "Balas e Confeitos", 1, 21.90m, 12.00m, 15, 270, DemandProfile.Candy, 85, 5, DemoScenario.Maintain, "1612262501903-1add4a20dc04"),
        new("DP-033", "Marshmallow Twist 250g", "Doce Vale", "Marshmallow torcido sabores morango e baunilha, 250g.", "Balas e Confeitos", 0, 9.90m, 5.10m, 15, 240, DemandProfile.Candy, 120, 6, DemoScenario.New, "1518646455738-c17d10184f5e"),
        new("DP-034", "Jujuba Sortida 1kg", "Doce Vale", "Jujubas sortidas, pacote de 1kg.", "Balas e Confeitos", 0, 19.90m, 10.40m, 15, 270, DemandProfile.Candy, 70, 4, DemoScenario.Stockout, "1728414215651-c48b3001b42d"),

        // Doces juninos (fornecedor regional, lead time curto)
        new("DP-035", "Paçoca Rolha (50 un.)", "Sabor da Roça", "Pote com 50 paçocas de amendoim tipo rolha.", "Doces Juninos", 3, 22.90m, 12.10m, 15, 180, DemandProfile.Junina, 90, 4, DemoScenario.Maintain, "1772985503023-3f6516084966"),
        new("DP-036", "Pé de Moleque 500g", "Sabor da Roça", "Pé de moleque tradicional com amendoim, 500g.", "Doces Juninos", 3, 16.90m, 8.70m, 10, 150, DemandProfile.Junina, 60, 4, DemoScenario.Maintain, "1772985503334-62b9690905c5"),
        new("DP-037", "Cocada Cremosa 300g", "Sabor da Roça", "Cocada cremosa de coco queimado, 300g.", "Doces Juninos", 3, 13.90m, 7.10m, 10, 90, DemandProfile.Junina, 55, 4, DemoScenario.Falling, "1676364000560-eefe24b03c45"),
        new("DP-038", "Doce de Leite em Tablete 500g", "Sabor da Roça", "Doce de leite em tabletes embalados individualmente, 500g.", "Doces Juninos", 3, 17.90m, 9.30m, 10, 180, DemandProfile.Junina, 65, 4, DemoScenario.Maintain, "1611516081814-55d97d5a7488"),

        // Biscoitos
        new("DP-039", "Biscoito Wafer de Chocolate 140g", "Doce Vale", "Biscoito wafer recheado de chocolate, 140g.", "Biscoitos", 0, 4.90m, 2.50m, 40, 240, DemandProfile.Biscuit, 210, 12, DemoScenario.Maintain, "1662484844144-92710767c951"),
        new("DP-040", "Cookies com Gotas de Chocolate 200g", "Casa Bellucci", "Cookies amanteigados com gotas de chocolate, 200g.", "Biscoitos", 2, 11.90m, 6.20m, 25, 180, DemandProfile.Biscuit, 90, 8, DemoScenario.Rising, "1558961363-fa8fdf82db35"),
        new("DP-041", "Biscoito Amanteigado Lata 400g", "Casa Bellucci", "Biscoitos amanteigados dinamarqueses em lata decorada, 400g.", "Biscoitos", 2, 34.90m, 19.50m, 10, 300, DemandProfile.Biscuit, 40, 3, DemoScenario.Buy, "1733486251708-c6409c32c93c"),
        new("DP-042", "Alfajor ao Leite 50g", "Doce Vale", "Alfajor recheado de doce de leite e coberto com chocolate ao leite, 50g.", "Biscoitos", 0, 5.50m, 2.80m, 40, 120, DemandProfile.Biscuit, 180, 12, DemoScenario.Maintain, "1517400847543-fd27a32c8d12"),

        // Cestas e kits (presentes)
        new("DP-043", "Cesta Café da Manhã Premium", "Presente Perfeito", "Cesta com chocolates, biscoitos, geleia e caneca.", "Cestas e Kits", 4, 189.90m, 112.00m, 5, 60, DemandProfile.GiftBasket, 25, 2, DemoScenario.Maintain, "1508899203029-1c9eb493c9bd"),
        new("DP-044", "Kit Dia das Mães: Bombons + Caneca", "Presente Perfeito", "Caixa de bombons com caneca de porcelana estampada.", "Cestas e Kits", 4, 79.90m, 44.00m, 0, 120, DemandProfile.MothersKit, 260, 2, DemoScenario.Maintain, "1765534379403-5a052c172cff"),
        new("DP-045", "Kit Namorados: Trufas + Pelúcia", "Presente Perfeito", "Caixa de trufas com ursinho de pelúcia.", "Cestas e Kits", 4, 99.90m, 55.00m, 0, 90, DemandProfile.ValentinesKit, 220, 2, DemoScenario.Maintain, "1774464593573-d641812872ce"),
        new("DP-046", "Cesta de Natal Clássica", "Presente Perfeito", "Cesta com panetone, chocolates, biscoitos e espumante sem álcool.", "Cestas e Kits", 4, 229.90m, 135.00m, 5, 90, DemandProfile.ChristmasHamper, 70, 2, DemoScenario.Buy, "1668432321756-5109ad1f5916"),
        new("DP-047", "Caneca com Bombons Personalizada", "Presente Perfeito", "Caneca de porcelana com nome e 6 bombons.", "Cestas e Kits", 4, 59.90m, 31.00m, 5, 120, DemandProfile.GiftBasket, 40, 2, DemoScenario.New, "1680337673561-531bca1cf5b7"),
        new("DP-048", "Kit Chocolate Quente: Caneca + Mix", "Presente Perfeito", "Caneca com mistura para chocolate quente e marshmallows.", "Cestas e Kits", 4, 69.90m, 38.00m, 5, 240, DemandProfile.ChocolateDrink, 30, 2, DemoScenario.Maintain, "1606423125989-d745da36eb05"),
    ];

    /// <summary>Promoções já encerradas: baixam o preço e aumentam o volume no período (aparecem no histórico da previsão).</summary>
    public static readonly DemoPastPromotion[] PastPromotions =
    [
        new("DP-001", "Black Friday", 15m, DemoPromotionWindow.BlackFriday),
        new("DP-009", "Black Friday", 10m, DemoPromotionWindow.BlackFriday),
        new("DP-025", "Queima de estoque de verão", 20m, DemoPromotionWindow.SummerClearance),
        new("DP-030", "Semana das Crianças", 15m, DemoPromotionWindow.ChildrensDay),
        new("DP-016", "Pós-Páscoa", 30m, DemoPromotionWindow.PostEaster),
    ];

    public static readonly (string Name, string LastName)[] Clients =
    [
        ("Mariana", "Albuquerque"), ("Rafael", "Monteiro"), ("Juliana", "Prado"), ("Thiago", "Bastos"),
        ("Camila", "Rezende"), ("Lucas", "Fontana"), ("Beatriz", "Carvalhal"), ("Gustavo", "Siqueira"),
        ("Larissa", "Teixeira Lima"), ("Felipe", "Andrade Nunes"), ("Patrícia", "Moreira"), ("Eduardo", "Lacerda"),
        ("Fernanda", "Quintela"), ("Rodrigo", "Vasconcelos"), ("Aline", "Barreto"), ("Marcelo", "Dantas"),
        ("Vanessa", "Coutinho"), ("Bruno", "Figueira"), ("Letícia", "Sampaio"), ("Diego", "Arruda"),
        ("Natália", "Peixoto"), ("André", "Brandão"), ("Carolina", "Mattos"), ("Vinícius", "Rocha Leal"),
        ("Isabela", "Guimarães"), ("Renato", "Paiva"), ("Gabriela", "Toledo"), ("Leonardo", "Furtado"),
        ("Priscila", "Valente"), ("Otávio", "Saldanha"),
    ];
}
