using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nextflow.Domain.Interfaces.Application.UseCases;

namespace Nextflow.Controllers;

/// <summary>Sino de notificações (T-DF12, F3.5): só as do usuário do token.</summary>
[Authorize]
[Route("api/notifications")]
public class NotificationsController(
    IGetNotificationsUseCase getUseCase,
    IMarkNotificationReadUseCase markReadUseCase,
    IMarkAllNotificationsReadUseCase markAllReadUseCase) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] bool unreadOnly = false, [FromQuery] int limit = 20, CancellationToken ct = default)
        => Handle(await getUseCase.Execute(UserId, unreadOnly, limit, ct));

    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
        => HandleNoContent(await markReadUseCase.Execute(id, UserId, ct));

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
        => HandleNoContent(await markAllReadUseCase.Execute(UserId, ct));
}
