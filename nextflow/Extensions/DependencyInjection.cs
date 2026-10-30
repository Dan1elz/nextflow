using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Nextflow.Domain.Interfaces.Application.Services;

namespace Nextflow.Extensions;

public static class DependencyInjection
{
    // Chave do appsettings.Development.json — pública para quem tem o repositório.
    public const string DevelopmentJwtKey = "nextflow-chave-de-desenvolvimento-nao-use-fora-do-dev-0001";

    // HS256 exige chave de pelo menos 256 bits (32 bytes).
    private const int MinimumJwtKeyLength = 32;

    public static IServiceCollection AddPresentation(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        AddCors(services, configuration);

        services.AddSwaggerGen(opt =>
        {
            opt.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Nextflow API",
                Version = "v1",
                Description = "Micro-ERP com inteligência de compras"
            });
            opt.CustomSchemaIds(type => type.FullName?.Replace("+", ".") ?? type.Name);
            opt.MapType(typeof(DateOnly), () => new OpenApiSchema { Type = "string", Format = "date" });
            opt.MapType(typeof(DateOnly?), () => new OpenApiSchema { Type = "string", Format = "date", Nullable = true });

            opt.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "Cabeçalho Authorization no formato \"Bearer {token}\"",
                In = ParameterLocation.Header,
                Name = "Authorization",
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer",
            });
            opt.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
                        Scheme = "oauth2",
                        Name = "Bearer",
                        In = ParameterLocation.Header
                    },
                    new List<string>()
                }
            });
        });
        services.AddSwaggerGenNewtonsoftSupport();

        var key = Encoding.ASCII.GetBytes(ValidateJwtKey(configuration["JwtSettings:Key"], environment));

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero,
                RoleClaimType = ClaimTypes.Role
            };
            options.Events = new JwtBearerEvents
            {
                // RN-AC02 (decisão de 29/10): usuário inativado perde o acesso na hora, mesmo com o token ainda válido.
                OnTokenValidated = async context =>
                {
                    var userAccess = context.HttpContext.RequestServices.GetRequiredService<IUserAccessService>();
                    var claim = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

                    if (!Guid.TryParse(claim, out var userId)
                        || !await userAccess.CanAccess(userId, context.HttpContext.RequestAborted))
                        context.Fail("Usuário inativo ou inexistente.");
                }
            };
        });
        services.AddAuthorization();

        services.AddControllers().AddNewtonsoftJson(options =>
        {
            options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            // Enum de entrada aceita o nome ("Phone"); a saída já vem como string pelo GetApiName() do DTO.
            options.SerializerSettings.Converters.Add(new StringEnumConverter());
            options.SerializerSettings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;
        });

        services.AddEndpointsApiExplorer();

        // Validação é do FluentValidation no use case (422 por campo), não do ModelState.
        services.Configure<ApiBehaviorOptions>(options => options.SuppressModelStateInvalidFilter = true);

        services.Configure<ForwardedHeadersOptions>(options =>
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

        return services;
    }

    /// <summary>
    /// Origens do front por ambiente. Aceita a lista (<c>Cors:AllowedOrigins:0</c>) ou um texto separado por vírgula
    /// (<c>Cors__AllowedOrigins</c> do compose de staging). Vazio = qualquer origem (a API autentica por Bearer, sem cookie).
    /// </summary>
    private static void AddCors(IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection("Cors:AllowedOrigins");
        var allowedOrigins = (section.Get<string[]>() ?? [section.Value ?? string.Empty])
            .SelectMany(origin => origin.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToArray();

        services.AddCors(options => options.AddDefaultPolicy(policy =>
        {
            if (allowedOrigins.Length == 0)
                policy.AllowAnyOrigin();
            else
                policy.WithOrigins(allowedOrigins);

            policy.AllowAnyHeader().AllowAnyMethod();
        }));
    }

    /// <summary>
    /// Fora de Development a API não sobe com chave JWT vazia, curta ou a de desenvolvimento: quem conhece a chave
    /// assina tokens válidos.
    /// </summary>
    private static string ValidateJwtKey(string? key, IHostEnvironment environment)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException(
                "A configuração 'JwtSettings:Key' é obrigatória. Configure a variável de ambiente 'JwtSettings__Key'.");

        if (environment.IsDevelopment())
            return key;

        if (key == DevelopmentJwtKey)
            throw new InvalidOperationException(
                "'JwtSettings__Key' está com a chave de desenvolvimento. Gere uma chave própria para este ambiente.");

        if (key.Length < MinimumJwtKeyLength)
            throw new InvalidOperationException(
                $"'JwtSettings__Key' precisa ter ao menos {MinimumJwtKeyLength} caracteres.");

        return key;
    }
}
