namespace Nextflow.Domain.Interfaces.Application.Services;

/// <summary>
/// Relógio de negócio: "hoje" e "agora" no fuso America/Sao_Paulo. O banco guarda UTC; use este serviço sempre que a
/// regra depender do dia local (vigência, aniversariantes do mês, agregação mensal).
/// </summary>
public interface IBusinessClock
{
    DateTime UtcNow { get; }
    DateTime Now { get; }
    DateOnly Today { get; }
    DateOnly ToBusinessDate(DateTime utc);
}
