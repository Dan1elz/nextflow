using System.Text;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Npgsql;
using Nextflow.Controllers;
using Nextflow.Domain.Dtos;

namespace Nextflow.Middlewares;

/// <summary>
/// Só para o inesperado: concorrência (token de versão ou chave única disputada por duas gravações) vira 409 e qualquer
/// outra exceção vira 500, sempre no formato
/// <see cref="ResultDto{T}"/>. Erro de negócio nunca chega aqui (vem pela <c>IResultFactory</c>).
/// </summary>
public class GlobalResultMiddleware(RequestDelegate next, IWebHostEnvironment env)
{
    public const string ConflictMessage = "Este registro foi alterado por outra pessoa. Recarregue e tente novamente.";

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
            await WriteErrorAsync(context, ConflictMessage, StatusCodes.Status409Conflict);
        }
        // Duas requisições criando a mesma linha (ex.: o primeiro saldo de uma variação num local): a segunda esbarra no
        // índice único. Regra de negócio de unicidade continua no use case; aqui só chega a corrida.
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            Serilog.Log.Warning(ex, "[CONCURRENCY] Chave única disputada por outra requisição");
            await WriteErrorAsync(context, ConflictMessage, StatusCodes.Status409Conflict);
        }
        // UTF-8 inválido estoura na leitura do corpo, antes de o ApiController ver o ModelState.
        catch (DecoderFallbackException)
        {
            await WriteErrorAsync(context, ApiController.InvalidBodyMessage, StatusCodes.Status400BadRequest);
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
