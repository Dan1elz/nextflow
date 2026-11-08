using Nextflow.Domain.Enums;

namespace Nextflow.Domain.Dtos.Notifications;

/// <summary>
/// Notificação a criar para cada usuário ativo (L-DF8). <c>DigestDate</c> marca o resumo diário (L-DF4): um por tipo e dia de negócio.
/// </summary>
public record NewNotificationDto(NotificationTypeEnum Type, string Title, string Message, string? Link, DateOnly? DigestDate = null);
