namespace Nextflow.Application.Forecast;

/// <summary>
/// Horários da análise semanal (RN-DF18, T-DF11), no relógio local de negócio. Classe pura: sem relógio nem banco.
/// </summary>
public static class ForecastSchedule
{
    /// <summary>Primeiro horário agendado estritamente depois de <paramref name="nowLocal"/>.</summary>
    public static DateTime NextSlot(DateTime nowLocal, DayOfWeek weekday, TimeOnly time)
    {
        var daysAhead = ((int)weekday - (int)nowLocal.DayOfWeek + 7) % 7;
        var candidate = nowLocal.Date.AddDays(daysAhead).Add(time.ToTimeSpan());
        return candidate > nowLocal ? candidate : candidate.AddDays(7);
    }

    /// <summary>Último horário agendado até <paramref name="nowLocal"/> (inclusive).</summary>
    public static DateTime LastSlot(DateTime nowLocal, DayOfWeek weekday, TimeOnly time)
        => NextSlot(nowLocal, weekday, time).AddDays(-7);

    /// <summary>Converte um horário local para UTC pela diferença atual entre o relógio local e o UTC (arredondada a 15 min).</summary>
    public static DateTime ToUtc(DateTime local, DateTime nowLocal, DateTime utcNow)
    {
        var offsetMinutes = Math.Round((nowLocal - utcNow).TotalMinutes / 15) * 15;
        return DateTime.SpecifyKind(local.AddMinutes(-offsetMinutes), DateTimeKind.Utc);
    }
}
