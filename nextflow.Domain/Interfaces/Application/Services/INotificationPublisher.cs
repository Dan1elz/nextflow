using Nextflow.Domain.Dtos.Notifications;

namespace Nextflow.Domain.Interfaces.Application.Services;

/// <summary>
/// Cria a notificação para todos os usuários ativos (L-DF8: hoje todos são Admin). Só registra; quem chama faz o commit.
/// </summary>
public interface INotificationPublisher
{
    Task PublishToAllAsync(NewNotificationDto notification, CancellationToken ct = default);
}

/// <summary>
/// Resumo diário (L-DF4): uma notificação de estoque baixo e uma de lote vencendo (30 dias) por dia de negócio, só quando há o que
/// avisar. Chamado periodicamente pelo job; idempotente no dia.
/// </summary>
public interface INotificationDigestService
{
    /// <summary>Hora local a partir da qual o resumo do dia sai.</summary>
    const int DigestHour = 7;

    Task RunAsync(CancellationToken ct);
}
