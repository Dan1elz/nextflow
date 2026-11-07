namespace Nextflow.Application.Forecast;

/// <summary>Parâmetros de uma execução do cálculo: o dia de negócio e o horizonte (1 a 6 meses, RN-DF17).</summary>
public sealed class ForecastCalculationContext
{
    /// <summary>Janela de histórico usada no cálculo (meses completos; mês corrente excluído, RN-DF02).</summary>
    public const int HistoryWindowMonths = 24;

    public const int MaxHorizonMonths = ForecastHorizon.MaxMonths;

    public DateOnly Today { get; }
    public int HorizonMonths { get; }

    /// <summary>Último mês completo (mês anterior ao corrente): fim da série histórica.</summary>
    public YearMonth LastCompleteMonth { get; }

    /// <summary>Mês corrente (a projeção só o usa pela fração restante de dias).</summary>
    public YearMonth CurrentMonth { get; }

    /// <summary>Meses do horizonte, a partir do próximo mês.</summary>
    public IReadOnlyList<YearMonth> Horizon { get; }

    public ForecastCalculationContext(DateOnly today, int horizonMonths)
    {
        if (horizonMonths is < 1 or > MaxHorizonMonths)
            throw new ArgumentOutOfRangeException(nameof(horizonMonths), $"O horizonte deve estar entre 1 e {MaxHorizonMonths} meses.");

        Today = today;
        HorizonMonths = horizonMonths;
        CurrentMonth = YearMonth.From(today);
        LastCompleteMonth = CurrentMonth.AddMonths(-1);
        Horizon = [.. Enumerable.Range(1, horizonMonths).Select(CurrentMonth.AddMonths)];
    }

    /// <summary>Mês correspondente à posição <paramref name="index"/> de uma série de <see cref="HistoryWindowMonths"/> meses.</summary>
    public YearMonth MonthAt(int index) => LastCompleteMonth.AddMonths(index - (HistoryWindowMonths - 1));
}
