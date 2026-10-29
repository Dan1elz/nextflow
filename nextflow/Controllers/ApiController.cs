using Microsoft.AspNetCore.Mvc;
using Nextflow.Domain.Dtos;
using Nextflow.Utils;

namespace Nextflow.Controllers;

/// <summary>
/// Base de todo controller: sucesso devolve o <c>Data</c> puro; falha devolve o <see cref="ResultDto{T}"/> com o status
/// do <c>ErrorCode</c> (`contracts/README.md`).
/// </summary>
public abstract class ApiController : ControllerBase
{
    public Guid UserId => TokenHelper.GetUserId(User);

    protected IActionResult Handle<T>(ResultDto<T> result)
        => result.IsSuccess ? Ok(result.Data) : StatusCode(result.ErrorCode, result);

    protected IActionResult HandleCreated<T>(ResultDto<T> result)
        => result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Data) : StatusCode(result.ErrorCode, result);

    protected IActionResult HandleNoContent<T>(ResultDto<T> result)
        => result.IsSuccess ? NoContent() : StatusCode(result.ErrorCode, result);
}
