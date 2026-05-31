using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nextflow.Application.Utils;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Interfaces.UseCases;
using Nextflow.Utils;

namespace Nextflow.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class DashboardController(IGetDashboardStatsUseCase getStatsUseCase) : ControllerBase
{
    private readonly IGetDashboardStatsUseCase _getStatsUseCase = getStatsUseCase;

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var userId = TokenHelper.GetUserId(this.User);
        var stats = await _getStatsUseCase.Execute(userId, ct);

        return Ok(new ApiResponse<DashboardStatsDto>
        {
            Status = 200,
            Message = "Estatísticas do dashboard recuperadas com sucesso.",
            Data = stats
        });
    }
}
