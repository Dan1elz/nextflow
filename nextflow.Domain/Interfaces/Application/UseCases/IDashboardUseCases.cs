using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Dashboard;
using Nextflow.Domain.Dtos.Notifications;

namespace Nextflow.Domain.Interfaces.Application.UseCases;

/// <summary>KPIs de venda do mês (T-DF1).</summary>
public interface IGetSalesKpisUseCase
{
    Task<ResultDto<SalesKpisDto>> Execute(Guid? storeId, CancellationToken ct = default);
}

/// <summary>Bloco "Estoque baixo" (T-DF1).</summary>
public interface IGetLowStockSummaryUseCase
{
    Task<ResultDto<LowStockSummaryDto>> Execute(Guid? storeId, CancellationToken ct = default);
}

/// <summary>Bloco "Lotes vencendo" (T-DF1).</summary>
public interface IGetExpiringLotsSummaryUseCase
{
    Task<ResultDto<ExpiringLotsSummaryDto>> Execute(Guid? storeId, int days, CancellationToken ct = default);
}

/// <summary>Bloco "Próximos eventos" (T-DF1).</summary>
public interface IGetUpcomingEventsUseCase
{
    Task<ResultDto<List<UpcomingEventDto>>> Execute(int days, CancellationToken ct = default);
}

/// <summary>Gráfico de vendas do painel (T-DF1).</summary>
public interface IGetSalesChartUseCase
{
    Task<ResultDto<SalesChartDto>> Execute(Guid? storeId, CancellationToken ct = default);
}

/// <summary>Card "Recomendações" do painel (T-DF1).</summary>
public interface IGetForecastTopUseCase
{
    Task<ResultDto<ForecastTopDto>> Execute(int limit, CancellationToken ct = default);
}

/// <summary>Sino (T-DF12): não lidas e as últimas do usuário.</summary>
public interface IGetNotificationsUseCase
{
    Task<ResultDto<NotificationListDto>> Execute(Guid? userId, bool unreadOnly, int limit, CancellationToken ct = default);
}

/// <summary>Marca uma notificação do usuário como lida (T-DF12).</summary>
public interface IMarkNotificationReadUseCase
{
    Task<ResultDto<bool>> Execute(Guid notificationId, Guid? userId, CancellationToken ct = default);
}

/// <summary>Marca todas as notificações do usuário como lidas (T-DF12).</summary>
public interface IMarkAllNotificationsReadUseCase
{
    Task<ResultDto<bool>> Execute(Guid? userId, CancellationToken ct = default);
}
