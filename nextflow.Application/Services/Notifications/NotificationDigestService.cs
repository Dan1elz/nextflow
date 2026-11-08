using Microsoft.Extensions.Logging;
using Nextflow.Domain.Dtos.Notifications;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;

namespace Nextflow.Application.Services.Notifications;

/// <summary>
/// Resumo diário (L-DF4): estoque baixo (variações × locais abaixo do mínimo, RN-CE33) e lotes vencendo em 30 dias, da empresa
/// inteira. Sai uma vez por dia de negócio a partir das 07:00; o índice único (usuário, tipo, dia) segura a repetição.
/// </summary>
public class NotificationDigestService(
    INotificationRepository notificationRepository,
    INotificationPublisher publisher,
    IStockBalanceRepository balanceRepository,
    IUnitOfWork unitOfWork,
    IBusinessClock clock,
    ILogger<NotificationDigestService> logger) : INotificationDigestService
{
    public const int ExpiringLotDays = 30;

    public async Task RunAsync(CancellationToken ct)
    {
        if (clock.Now.Hour < INotificationDigestService.DigestHour)
            return;

        var today = clock.Today;
        var published = 0;

        if (!await notificationRepository.Exists(n => n.Type == NotificationTypeEnum.LowStock && n.DigestDate == today, ct))
        {
            var (count, _) = await balanceRepository.GetLowStock(null, 0, ct);
            if (count > 0)
            {
                await publisher.PublishToAllAsync(new NewNotificationDto(
                    NotificationTypeEnum.LowStock,
                    "Estoque baixo",
                    count == 1 ? "1 variação está abaixo do mínimo em algum local." : $"{count} variações estão abaixo do mínimo em algum local.",
                    "/products?lowStock=true",
                    today), ct);
                published++;
            }
        }

        if (!await notificationRepository.Exists(n => n.Type == NotificationTypeEnum.LotExpiring && n.DigestDate == today, ct))
        {
            var lots = await balanceRepository.GetExpiringLots(today, today.AddDays(ExpiringLotDays), null, ct);
            if (lots.Count > 0)
            {
                var first = lots.Min(l => l.ExpiresOn);
                await publisher.PublishToAllAsync(new NewNotificationDto(
                    NotificationTypeEnum.LotExpiring,
                    "Lotes vencendo",
                    $"{lots.Count} lote(s) vencem nos próximos {ExpiringLotDays} dias; o primeiro em {first:dd/MM/yyyy}.",
                    "/products",
                    today), ct);
                published++;
            }
        }

        if (published > 0)
        {
            await unitOfWork.CommitAsync(ct);
            logger.LogInformation("Notificações: resumo diário de {Date:dd/MM/yyyy} publicado ({Count} tipo(s)).", today, published);
        }
    }
}
