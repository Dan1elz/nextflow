using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nextflow.Domain.Dtos;
using Nextflow.Utils;

namespace Nextflow.Controllers;

/// <summary>
/// Base de todo controller: sucesso devolve o <c>Data</c> puro; falha devolve o <see cref="ResultDto{T}"/> com o status
/// do <c>ErrorCode</c> (`contracts/README.md`).
/// </summary>
public abstract class ApiController : ControllerBase, IActionFilter
{
    public const string InvalidBodyMessage = "O corpo da requisição está vazio ou não é um JSON válido.";

    public Guid UserId => TokenHelper.GetUserId(User);

    protected IActionResult Handle<T>(ResultDto<T> result)
        => result.IsSuccess ? Ok(result.Data) : StatusCode(result.ErrorCode, result);

    protected IActionResult HandleCreated<T>(ResultDto<T> result)
        => result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Data) : StatusCode(result.ErrorCode, result);

    protected IActionResult HandleNoContent<T>(ResultDto<T> result)
        => result.IsSuccess ? NoContent() : StatusCode(result.ErrorCode, result);

    /// <summary>
    /// Corpo que nem chega a virar DTO (data "xx", JSON quebrado) não alcança o FluentValidation: o campo que falhou na
    /// conversão volta como 422 no campo, e corpo vazio ou JSON quebrado como 400. Implementação explícita para o MVC não
    /// tratar estes métodos como actions.
    /// </summary>
    void IActionFilter.OnActionExecuting(ActionExecutingContext context)
    {
        var body = context.ActionDescriptor.Parameters.FirstOrDefault(p => p.BindingInfo?.BindingSource == BindingSource.Body);
        if (body is null || (context.ActionArguments.TryGetValue(body.Name, out var argument) && argument is not null))
            return;

        // A entrada com o nome do parâmetro é o "campo obrigatório" implícito do próprio DTO, não um campo do formulário.
        var fieldErrors = ModelState
            .Where(entry => entry.Key.Length > 0 && entry.Key != body.Name && entry.Value!.Errors.Count > 0)
            .ToDictionary(entry => entry.Key, _ => new[] { "Valor inválido." });

        // JSON quebrado também cita um campo ({"email": aponta para email), mas o erro é do corpo.
        var result = fieldErrors.Count > 0 && IsWellFormedJson()
            ? ResultDto<object>.Failure(fieldErrors)
            : ResultDto<object>.Failure(InvalidBodyMessage);

        context.Result = StatusCode(result.ErrorCode, result);
    }

    void IActionFilter.OnActionExecuted(ActionExecutedContext context) { }

    /// <summary>Relê o corpo, que o pipeline deixa em buffer (Program.cs), para separar JSON quebrado de campo que não converte.</summary>
    private bool IsWellFormedJson()
    {
        if (!Request.Body.CanSeek)
            return false;

        Request.Body.Position = 0;
        using var reader = new JsonTextReader(new StreamReader(Request.Body, leaveOpen: true));
        try
        {
            JToken.ReadFrom(reader);
            return true;
        }
        catch (JsonReaderException)
        {
            return false;
        }
    }
}
