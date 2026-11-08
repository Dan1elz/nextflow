using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nextflow.Domain.Dtos.Forecasts;
using Nextflow.Domain.Interfaces.Application.UseCases;

namespace Nextflow.Controllers;

/// <summary>Agendamento da análise automática e lote da IA (T-DF11, F3.3).</summary>
[Authorize]
[Route("api/settings/forecast")]
public class ForecastSettingsController(
    IGetForecastSettingsUseCase getUseCase,
    IUpdateForecastSettingsUseCase updateUseCase) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Handle(await getUseCase.Execute(ct));

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] ForecastSettingsRequestDto dto, CancellationToken ct)
        => Handle(await updateUseCase.Execute(dto, UserId, ct));
}
