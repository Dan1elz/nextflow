using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Notifications;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Application.UseCases;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;

namespace Nextflow.Application.UseCases.Notifications;

/// <summary>Sino (T-DF12): contador de não lidas e as últimas (até 50; padrão 20), mais recentes primeiro, só do usuário.</summary>
public class GetNotificationsUseCase(
    INotificationRepository notificationRepository,
    IResultFactory<NotificationListDto> resultFactory) : IGetNotificationsUseCase
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 50;

    public async Task<ResultDto<NotificationListDto>> Execute(Guid? userId, bool unreadOnly, int limit, CancellationToken ct = default)
    {
        if (userId is null)
            return resultFactory.Failure("Usuário não identificado.", 401);

        var id = userId.Value;
        limit = limit is < 1 or > MaxLimit ? DefaultLimit : limit;
        var items = await notificationRepository.GetAll(
            n => n.UserId == id && (!unreadOnly || n.ReadAt == null), 0, limit,
            orderBy: q => q.OrderByDescending(n => n.CreatedAt).ThenBy(n => n.Id), ct: ct);

        return resultFactory.Success(new NotificationListDto
        {
            UnreadCount = await notificationRepository.Count(n => n.UserId == id && n.ReadAt == null, ct),
            Items = [.. items.Select(n => new NotificationDto(n))],
        });
    }
}

/// <summary>Marca como lida (T-DF12); notificação de outro usuário é 404.</summary>
public class MarkNotificationReadUseCase(
    INotificationRepository notificationRepository,
    IBusinessClock clock,
    IUnitOfWork unitOfWork,
    IResultFactory<bool> resultFactory) : IMarkNotificationReadUseCase
{
    public async Task<ResultDto<bool>> Execute(Guid notificationId, Guid? userId, CancellationToken ct = default)
    {
        if (userId is null)
            return resultFactory.Failure("Usuário não identificado.", 401);

        var notification = await notificationRepository.GetByIdForUpdate(notificationId, ct: ct);
        if (notification is null || notification.UserId != userId)
            return resultFactory.Failure("Notificação não encontrada.", 404);

        notification.MarkRead(clock.UtcNow);
        await unitOfWork.CommitAsync(ct);
        return resultFactory.Success(true);
    }
}

/// <summary>Marca todas as não lidas do usuário como lidas (T-DF12).</summary>
public class MarkAllNotificationsReadUseCase(
    INotificationRepository notificationRepository,
    IBusinessClock clock,
    IUnitOfWork unitOfWork,
    IResultFactory<bool> resultFactory) : IMarkAllNotificationsReadUseCase
{
    public async Task<ResultDto<bool>> Execute(Guid? userId, CancellationToken ct = default)
    {
        if (userId is null)
            return resultFactory.Failure("Usuário não identificado.", 401);

        var id = userId.Value;
        var unread = await notificationRepository.GetAllForUpdate(n => n.UserId == id && n.ReadAt == null, ct);
        var utcNow = clock.UtcNow;
        foreach (var notification in unread)
            notification.MarkRead(utcNow);
        if (unread.Count > 0)
            await unitOfWork.CommitAsync(ct);
        return resultFactory.Success(true);
    }
}
