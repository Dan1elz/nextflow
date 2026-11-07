namespace Nextflow.Application.Forecast;

/// <summary>
/// Chaves do cálculo. <see cref="OffSeasonGuard"/> liga a correção da F3.7 e vem ligada; desligá-la devolve o comportamento antigo,
/// o que os testes usam para reproduzir o defeito ("Comprar alta" de item de temporada zerado fora da época).
/// </summary>
public sealed record ForecastOptions(bool OffSeasonGuard = true)
{
    public static ForecastOptions Default { get; } = new();
}
