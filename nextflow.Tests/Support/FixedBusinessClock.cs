using Nextflow.Application.Services;
using Nextflow.Domain.Interfaces.Application.Services;

namespace Nextflow.Tests.Support;

/// <summary>
/// Relógio de negócio parado num dia (meio-dia, no fuso de negócio): o seed histórico, o recálculo derivado e a previsão enxergam o
/// mesmo "hoje", e o teste não depende do dia em que roda. Conversões de fuso seguem o <see cref="BusinessClock"/> real.
/// </summary>
public sealed class FixedBusinessClock(DateOnly today) : IBusinessClock
{
    private readonly BusinessClock _real = new();

    public DateTime UtcNow => _real.StartOfDayUtc(today).AddHours(12);
    public DateTime Now => today.ToDateTime(new TimeOnly(12, 0));
    public DateOnly Today => today;
    public DateOnly ToBusinessDate(DateTime utc) => _real.ToBusinessDate(utc);
    public DateTime StartOfDayUtc(DateOnly date) => _real.StartOfDayUtc(date);
}
