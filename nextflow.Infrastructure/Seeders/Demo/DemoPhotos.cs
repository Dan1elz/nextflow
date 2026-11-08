namespace Nextflow.Infrastructure.Seeders.Demo;

/// <summary>
/// Fotos do catálogo de demonstração: código do produto → id da foto no Unsplash (licença livre,
/// <c>images.unsplash.com/photo-{id}</c>). Os ids vêm do <c>DemoStoreCatalog</c> do código antigo (v1). Produto fora da
/// lista fica sem foto (urso de pelúcia e caneca estampada, que o catálogo antigo não tinha).
/// </summary>
public static class DemoPhotos
{
    public static readonly IReadOnlyDictionary<string, string> ByProductCode = new Dictionary<string, string>
    {
        ["DP-001"] = "1623660053975-cf75a8be0908",
        ["DP-002"] = "1610450949065-1f2841536c88",
        ["DP-003"] = "1627647227768-705244233b56",
        ["DP-004"] = "1679143121360-d2f8e948e996",
        ["DP-005"] = "1614088685112-0a760b71a3c8",
        ["DP-007"] = "1592290216331-620cbbbe0bce",
        ["DP-008"] = "1687795097254-f019f9d7fd17",
        ["DP-009"] = "1542843137-8791a6904d14",
        ["DP-011"] = "1656821991459-ae723d90d648",
        ["DP-012"] = "1769775264026-fbc4e974dea8",
        ["DP-013"] = "1521905621926-7c52537741be",
        ["DP-014"] = "1648022386629-224c9b86e2f9",
        ["DP-015"] = "1559142843-5b8fcb9de0af",
        ["DP-016"] = "1522742034131-3dca7544197c",
        ["DP-017"] = "1769433509476-bd68b1229916",
        ["DP-020"] = "1788521042909-3a7f486d0321",
        ["DP-021"] = "1762304887845-0eef96543910",
        ["DP-022"] = "1762304887659-7d4127c1e2e6",
        ["DP-023"] = "1762304887990-36fa522ae0b6",
        ["DP-024"] = "1762304887826-88a67a8f4a71",
        ["DP-025"] = "1637572815755-c4b80092dce1",
        ["DP-026"] = "1770123366817-743776f66d6a",
        ["DP-027"] = "1702648906616-b030f193edb9",
        ["DP-028"] = "1728310335347-082140001938",
        ["DP-030"] = "1635342219731-4ae2bf39e1e9",
        ["DP-031"] = "1687499466474-df90c4d7a54a",
        ["DP-033"] = "1518646455738-c17d10184f5e",
        ["DP-034"] = "1728414215651-c48b3001b42d",
        ["DP-035"] = "1772985503023-3f6516084966",
        ["DP-036"] = "1772985503334-62b9690905c5",
        ["DP-038"] = "1611516081814-55d97d5a7488",
        ["DP-039"] = "1662484844144-92710767c951",
        ["DP-040"] = "1558961363-fa8fdf82db35",
        ["DP-041"] = "1733486251708-c6409c32c93c",
        ["DP-042"] = "1517400847543-fd27a32c8d12",
        ["DP-043"] = "1508899203029-1c9eb493c9bd",
        ["DP-044"] = "1765534379403-5a052c172cff",
        ["DP-045"] = "1774464593573-d641812872ce",
        ["DP-046"] = "1668432321756-5109ad1f5916",
        ["DP-047"] = "1680337673561-531bca1cf5b7",
    };

    public static string Url(string photoId) => $"https://images.unsplash.com/photo-{photoId}?w=800&auto=format&fit=crop&q=80";
}
