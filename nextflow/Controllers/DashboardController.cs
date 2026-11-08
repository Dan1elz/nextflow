using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nextflow.Domain.Interfaces.Application.UseCases;

namespace Nextflow.Controllers;

/// <summary>Painel inicial (T-DF1, F3.5): blocos independentes, cada um na sua rota.</summary>
[Authorize]
[Route("api/dashboard")]
public class DashboardController(
    IGetSalesKpisUseCase salesKpisUseCase,
    IGetLowStockSummaryUseCase lowStockUseCase,
    IGetExpiringLotsSummaryUseCase expiringLotsUseCase,
    IGetUpcomingEventsUseCase upcomingEventsUseCase,
    IGetSalesChartUseCase salesChartUseCase) : ApiController
{
    [HttpGet("sales-kpis")]
    public async Task<IActionResult> SalesKpis([FromQuery] Guid? storeId, CancellationToken ct)
        => Handle(await salesKpisUseCase.Execute(storeId, ct));

    [HttpGet("low-stock")]
    public async Task<IActionResult> LowStock([FromQuery] Guid? storeId, CancellationToken ct)
        => Handle(await lowStockUseCase.Execute(storeId, ct));

    [HttpGet("expiring-lots")]
    public async Task<IActionResult> ExpiringLots([FromQuery] Guid? storeId, [FromQuery] int days = 30, CancellationToken ct = default)
        => Handle(await expiringLotsUseCase.Execute(storeId, days, ct));

    [HttpGet("upcoming-events")]
    public async Task<IActionResult> UpcomingEvents([FromQuery] int days = 90, CancellationToken ct = default)
        => Handle(await upcomingEventsUseCase.Execute(days, ct));

    [HttpGet("sales-chart")]
    public async Task<IActionResult> SalesChart([FromQuery] Guid? storeId, CancellationToken ct)
        => Handle(await salesChartUseCase.Execute(storeId, ct));
}
