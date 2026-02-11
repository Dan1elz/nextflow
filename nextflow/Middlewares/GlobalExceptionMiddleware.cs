using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Exceptions;

namespace Nextflow.Middlewares;

public class GlobalExceptionMiddleware(RequestDelegate next, IWebHostEnvironment env)
{
    private readonly RequestDelegate _next = next;
    private readonly IWebHostEnvironment _env = env;

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var response = context.Response;
        response.ContentType = "application/json";

        int statusCode;
        string message;
        IDictionary<string, string[]>? errors = null;

        switch (ex)
        {
            case BadRequestException:
                statusCode = (int)HttpStatusCode.BadRequest; // 400
                message = ex.Message;
                break;
            case NotAuthorizedException:
                statusCode = (int)HttpStatusCode.Unauthorized; // 401
                message = ex.Message;
                break;
            case NotFoundException:
                statusCode = (int)HttpStatusCode.NotFound; // 404
                message = ex.Message;
                break;
            case NextflowValidationException validationEx:
                statusCode = (int)HttpStatusCode.UnprocessableEntity; // 422
                message = validationEx.Message;
                errors = validationEx.Errors;
                break;
            default:
                statusCode = (int)HttpStatusCode.InternalServerError; // 500
                message = _env.IsDevelopment() ? ex.ToString() : "Erro interno do servidor. Contate o suporte.";
                if (_env.IsDevelopment())
                    Console.WriteLine("[GlobalExceptionMiddleware] " + ex.ToString());
                break;
        }
        response.StatusCode = statusCode;
        var result = JsonSerializer.Serialize(new ApiResponseMessage
        {
            Status = statusCode,
            Message = message,
            Errors = errors
        });

        return response.WriteAsync(result);
    }
}

