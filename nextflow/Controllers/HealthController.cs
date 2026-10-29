using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nextflow.Domain.Interfaces.Application.UseCases;

namespace Nextflow.Controllers;

// Anônimo de propósito: usado pelo healthcheck do Docker e do proxy, que não têm token.
[AllowAnonymous]
[Route("health")]
public class HealthController(IGetHealthUseCase getHealthUseCase) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Handle(await getHealthUseCase.Execute(ct));
}
