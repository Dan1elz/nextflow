using Nextflow.Domain.Dtos.Notifications;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.Services.Notifications;

/// <summary>Uma notificação por usuário ativo (L-DF8). Só registra; o commit é de quem chama.</summary>
public class NotificationPublisher(IUserRepository userRepository, INotificationRepository notificationRepository) : INotificationPublisher
{
    public async Task PublishToAllAsync(NewNotificationDto notification, CancellationToken ct = default)
    {
        var users = await userRepository.GetAll(u => u.IsActive, 0, int.MaxValue, ct: ct);
        var rows = users.Select(u => new Notification(notification, u.Id)).ToList();
        if (rows.Count > 0)
            await notificationRepository.AddRangeAsync(rows, ct);
    }
}
