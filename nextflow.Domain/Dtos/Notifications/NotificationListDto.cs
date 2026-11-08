namespace Nextflow.Domain.Dtos.Notifications;

/// <summary>Sino (T-DF12): contador de não lidas e as últimas notificações.</summary>
public class NotificationListDto
{
    public int UnreadCount { get; set; }
    public List<NotificationDto> Items { get; set; } = [];
}
