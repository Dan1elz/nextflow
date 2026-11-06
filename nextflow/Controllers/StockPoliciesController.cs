using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nextflow.Domain.Dtos.Stock;
using Nextflow.Domain.Interfaces.Application.UseCases;
using Nextflow.Domain.Interfaces.Application.UseCases.Base;

namespace Nextflow.Controllers;

/// <summary>Política de estoque por classe ABC (T13, RN-CE29). As três linhas vêm do seed: não há criar nem inativar.</summary>
[Authorize]
[Route("api/stock-policies")]
public class StockPoliciesController(
    IGetStockPoliciesUseCase getAllUseCase,
    IUpdateUseCase<StockPolicyRequestDto, StockPolicyDto> updateUseCase) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Handle(await getAllUseCase.Execute(ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] StockPolicyRequestDto dto, CancellationToken ct)
        => Handle(await updateUseCase.Execute(id, dto, ct));
}
