using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Interfaces.Application.UseCases.Base;
using Nextflow.Domain.Models;
using Nextflow.Utils;

namespace Nextflow.Controllers;

/// <summary>Dado de referência, só leitura (decisão 5 de 29/10).</summary>
[Authorize]
[Route("api/states")]
public class StatesController(IGetOptionsUseCase<State, Option> getOptionsUseCase) : ApiController
{
    [HttpGet("options")]
    public async Task<IActionResult> Options([FromQuery] string? search = null, [FromQuery] string? filters = null, CancellationToken ct = default)
        => Handle(await getOptionsUseCase.Execute(search, FilterHelper.Parse(filters), ct));
}
