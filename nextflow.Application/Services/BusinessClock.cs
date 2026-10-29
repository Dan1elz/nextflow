using Nextflow.Domain.Interfaces.Application.Services;

namespace Nextflow.Application.Services;

/// <summary>Implementação do <see cref="IBusinessClock"/> no fuso America/Sao_Paulo (portado de <c>Domain/Utils/BusinessClock</c>).</summary>
public class BusinessClock : IBusinessClock
{
    public const string TimeZoneId = "America/Sao_Paulo";

    private static readonly TimeZoneInfo TimeZone = ResolveTimeZone();

    public DateTime UtcNow => DateTime.UtcNow;

    public DateTime Now => ToBusinessTime(DateTime.UtcNow);

    public DateOnly Today => DateOnly.FromDateTime(Now);

    public DateOnly ToBusinessDate(DateTime utc) => DateOnly.FromDateTime(ToBusinessTime(utc));

    private static DateTime ToBusinessTime(DateTime utc)
    {
        var value = utc.Kind == DateTimeKind.Utc ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(value, TimeZone);
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
