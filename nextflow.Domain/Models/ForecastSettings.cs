using Nextflow.Domain.Dtos.Forecasts;

namespace Nextflow.Domain.Models;

/// <summary>
/// Agendamento da análise automática e tamanho do lote da IA (T-DF11, RN-DF18, RN-DF20). Linha única: enquanto ninguém salva, vale
/// o padrão (ligado, segunda às 06:00 no fuso de negócio, lote de 40).
/// </summary>
public class ForecastSettings : BaseModel
{
    public const DayOfWeek DefaultWeekday = DayOfWeek.Monday;
    public static readonly TimeOnly DefaultTime = new(6, 0);
    public const int DefaultAiBatchSize = 40;

    public bool ScheduleEnabled { get; private set; } = true;
    public DayOfWeek Weekday { get; private set; } = DefaultWeekday;
    public TimeOnly Time { get; private set; } = DefaultTime;
    public int AiBatchSize { get; private set; } = DefaultAiBatchSize;
    public Guid? UpdatedById { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public virtual User? UpdatedBy { get; private set; }

    public override string Preposition => "a";
    public override string Singular => "configuração da previsão";
    public override string Plural => "configurações da previsão";

    /// <summary>Padrão em memória (nada gravado ainda).</summary>
    public ForecastSettings() { }

    public ForecastSettings(ForecastSettingsRequestDto dto, Guid updatedById, DateTime utcNow) => Update(dto, updatedById, utcNow);

    public void Update(ForecastSettingsRequestDto dto, Guid updatedById, DateTime utcNow)
    {
        ScheduleEnabled = dto.ScheduleEnabled;
        Weekday = (DayOfWeek)dto.Weekday;
        Time = TimeOnly.ParseExact(dto.Time, "HH:mm");
        AiBatchSize = dto.AiBatchSize;
        UpdatedById = updatedById;
        UpdatedAt = utcNow;
    }
}
