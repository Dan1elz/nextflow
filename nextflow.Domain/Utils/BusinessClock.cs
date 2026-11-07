namespace Nextflow.Domain.Utils;

/// <summary>
/// Relógio de negócio: "hoje" e "agora" no fuso America/Sao_Paulo.
/// As datas são persistidas em UTC; use este helper sempre que a regra depender do dia local
/// (vigência de promoções, agregação mensal de vendas, datas de previsão etc.).
/// </summary>
public static class BusinessClock
{
    public const string TimeZoneId = "America/Sao_Paulo";

    public static TimeZoneInfo TimeZone { get; } = ResolveTimeZone();

    /// <summary>Data/hora atual no fuso de negócio.</summary>
    public static DateTime Now => ToBusinessTime(DateTime.UtcNow);

    /// <summary>Data atual no fuso de negócio.</summary>
    public static DateOnly Today => DateOnly.FromDateTime(Now);

    /// <summary>Converte um instante UTC para o horário local de negócio.</summary>
    public static DateTime ToBusinessTime(DateTime utc)
    {
        var value = utc.Kind == DateTimeKind.Utc ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(value, TimeZone);
    }

    /// <summary>Data local de negócio correspondente a um instante UTC.</summary>
    public static DateOnly ToBusinessDate(DateTime utc) => DateOnly.FromDateTime(ToBusinessTime(utc));

    /// <summary>Converte uma data/hora local de negócio para UTC.</summary>
    public static DateTime ToUtc(DateOnly date, TimeOnly time)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, TimeZone);
    }

    private static TimeZoneInfo ResolveTimeZone()
    {
        foreach (var id in new[] { TimeZoneId, "E. South America Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        // Fallback: Brasília sem horário de verão (UTC-3).
        return TimeZoneInfo.CreateCustomTimeZone(TimeZoneId, TimeSpan.FromHours(-3), TimeZoneId, TimeZoneId);
    }
}
