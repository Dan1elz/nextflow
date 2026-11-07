namespace Nextflow.Application.Forecast;

/// <summary>
/// Horizonte automático da análise (RN-DF17): maior prazo de entrega dos produtos, em meses cheios, mais 3 meses, no máximo 6.
/// O horizonte é da análise inteira; a necessidade de compra de cada produto usa o prazo dele (L-DF2).
/// </summary>
public static class ForecastHorizon
{
    public const int ExtraMonths = 3;
    public const int MaxMonths = 6;
    public const int DaysPerMonth = 30;

    public static int Automatic(int maxLeadTimeDays)
    {
        var leadMonths = (int)Math.Ceiling(Math.Max(0, maxLeadTimeDays) / (double)DaysPerMonth);
        return Math.Clamp(leadMonths + ExtraMonths, 1, MaxMonths);
    }
}
