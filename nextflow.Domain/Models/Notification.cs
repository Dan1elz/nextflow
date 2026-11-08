using Nextflow.Domain.Dtos.Notifications;
using Nextflow.Domain.Enums;

namespace Nextflow.Domain.Models;

/// <summary>Notificação do sino (T-DF12), uma por usuário (L-DF8). Lida = <see cref="ReadAt"/> preenchido; nunca é apagada pela API.</summary>
public class Notification : BaseModel
{
    public const int TitleMaxLength = 120;
    public const int MessageMaxLength = 500;
    public const int LinkMaxLength = 300;

    public Guid UserId { get; private set; }
    public NotificationTypeEnum Type { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string? Link { get; private set; }
    /// <summary>Dia de negócio do resumo diário (L-DF4); nulo nas notificações por evento.</summary>
    public DateOnly? DigestDate { get; private set; }
    public DateTime? ReadAt { get; private set; }

    public virtual User User { get; private set; } = null!;

    public override string Preposition => "a";
    public override string Singular => "notificação";
    public override string Plural => "notificações";

    private Notification() { }

    public Notification(NewNotificationDto dto, Guid userId)
    {
        UserId = userId;
        Type = dto.Type;
        Title = Cut(dto.Title, TitleMaxLength);
        Message = Cut(dto.Message, MessageMaxLength);
        Link = dto.Link;
        DigestDate = dto.DigestDate;
    }

    public bool IsRead => ReadAt.HasValue;

    public void MarkRead(DateTime utcNow) => ReadAt ??= utcNow;

    private static string Cut(string value, int max) => value.Length <= max ? value : value[..(max - 1)].TrimEnd() + "…";
}
