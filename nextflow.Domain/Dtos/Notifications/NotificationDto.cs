using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Notifications;

/// <summary>Item do sino (T-DF12).</summary>
public class NotificationDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Link { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }

    public NotificationDto() { }

    public NotificationDto(Notification entity)
    {
        Id = entity.Id;
        Type = entity.Type.GetApiName();
        Title = entity.Title;
        Message = entity.Message;
        Link = entity.Link;
        CreatedAt = entity.CreatedAt;
        ReadAt = entity.ReadAt;
    }
}
