using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Nextflow.Domain.Dtos;

namespace Nextflow.Middlewares;

/// <summary>
/// Só para o inesperado: concorrência vira 409 e qualquer outra exceção vira 500, sempre no formato
/// <see cref="ResultDto{T}"/>. Erro de negócio nunca chega aqui (vem pela <c>IResultFactory</c>).
/// </summary>
public class GlobalResultMiddleware(RequestDelegate next, IWebHostEnvironment env)
{
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver()
    };

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            Serilog.Log.Warning(ex, "[CONCURRENCY] Registro alterado por outra requisição");
            await WriteErrorAsync(context, "Este registro foi alterado por outra pessoa. Recarregue e tente novamente.", StatusCodes.Status409Conflict);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !context.RequestAborted.IsCancellationRequested)
        {
            Serilog.Log.Error(ex, "[CRITICAL ERROR] Unhandled exception");
            var message = env.IsDevelopment()
                ? $"Mensagem: {ex.Message} | StackTrace: {ex.StackTrace}"
                : "Ocorreu um erro interno no servidor.";
            await WriteErrorAsync(context, message, StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task WriteErrorAsync(HttpContext context, string message, int statusCode)
    {
        if (context.Response.HasStarted)
            return;

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var errorResponse = ResultDto<object>.Failure(message, statusCode);
        await context.Response.WriteAsync(JsonConvert.SerializeObject(errorResponse, JsonSettings));
    }
}
