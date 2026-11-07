using System.Globalization;
using System.Net;
using System.Security.Claims; // <<-- MUDANÇA 1: Adicionar este using
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Nextflow.Application.Services;
using Nextflow.Application.Services.Ai;
using Nextflow.Application.UseCases.Users;
using Nextflow.Application.Utils;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Interfaces.Utils;
using Nextflow.Infrastructure.Database;
using Nextflow.Infrastructure.Repositories;
using Nextflow.Infrastructure.Seeders;
using Nextflow.Infrastructure.Seeders.Demo;
using Nextflow.Infrastructure.Storage;
using Nextflow.Middlewares;

namespace Nextflow;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // *** CONFIGURAÇÃO DO DATABASE CONTEXT ***
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

        // *** CONFIGURAÇÃO DE CORS ***
        // Cors:AllowedOrigins (env Cors__AllowedOrigins): origens separadas por vírgula,
        // ex. "https://nextflow.zanni.tech". Vazio = qualquer origem (dev local)
        var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
        {
            if (allowedOrigins.Length > 0)
                policy.WithOrigins(allowedOrigins);
            else
                policy.AllowAnyOrigin();

            policy
            .AllowAnyHeader()
            .AllowAnyMethod();
        }));

        // *** ATRÁS DO PROXY DE BORDA (nginx) ***
        // Quem conecta no container é o proxy. Lendo X-Forwarded-For/Proto, a aplicação
        // enxerga o IP do visitante e Request.Scheme = https (e o UseHttpsRedirection
        // não tenta redirecionar). Só confia no cabeçalho vindo de rede privada (Docker):
        // o padrão do ASP.NET confia só em loopback e ignoraria o proxy
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Parse("10.0.0.0"), 8));
            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Parse("172.16.0.0"), 12));
            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Parse("192.168.0.0"), 16));
            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Loopback, 8));
        });

        // *** CONFIGURAÇÃO DO SWAGGER ***
        builder.Services.AddSwaggerGen(opt =>
        {
            opt.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Nextflow API",
                Version = "v1",
                Description = "API do sistema Nextflow"
            });
            // Evita conflito de schema entre DTOs com mesmo nome em assemblies diferentes
            opt.CustomSchemaIds(type => type.FullName?.Replace("+", ".") ?? type.Name);
            // Schema de upload de arquivo (evita erro 500 ao gerar swagger.json com IFormFile/IFileData)
            opt.MapType(typeof(IFormFile), () => new OpenApiSchema { Type = "string", Format = "binary" });
            opt.MapType(typeof(IFileData), () => new OpenApiSchema { Type = "string", Format = "binary" });
            opt.MapType(typeof(DateOnly), () => new OpenApiSchema { Type = "string", Format = "date" });
            opt.MapType(typeof(DateOnly?), () => new OpenApiSchema { Type = "string", Format = "date", Nullable = true });
            opt.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "Standard Authorization header using the Bearer scheme (\"bearer {token}\")",
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
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        },
                        Scheme = "oauth2",
                        Name = "Bearer",
                        In = ParameterLocation.Header
                    },
                    new List<string>()
                }
            });
        });
        builder.Services.AddSwaggerGenNewtonsoftSupport();

        // *** CONFIGURAÇÃO DE AUTENTICAÇÃO JWT ***
        var key = Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:Key"]!);

        builder.Services.AddAuthentication(options =>
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
        });
        builder.Services.AddAuthorization();

        builder.Services.Configure<JwtUtils.JwtSettingsUseCase>(
            builder.Configuration.GetSection("JwtSettings"));

        // *** ADICIONANDO CONTROLLERS COM SUPORTE A NEWTONSOFT.JSON ***
        builder.Services.AddControllers().AddNewtonsoftJson(options =>
        {
            options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
        });

        builder.Services.AddEndpointsApiExplorer();

        // *** CONFIGURAÇÃO DE HEALTH CHECKS ***
        builder.Services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("database");

        // *** REGISTRO DE DEPENDÊNCIAS (SCRUTOR) ***
        builder.Services.Scan(scan => scan
            .FromAssemblyOf<CreateUserUseCase>()
            .AddClasses(classes => classes.Where(type => type.Name.EndsWith("UseCase")))
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        builder.Services.Scan(scan => scan
            .FromAssemblyOf<UserRepository>()
            .AddClasses(classes => classes.Where(type => type.Name.EndsWith("Repository")))
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        // Seeders: toda implementação de ISeeder, executadas em ordem pelo SeederRunner no startup
        builder.Services.Scan(scan => scan
            .FromAssemblyOf<ISeeder>()
            .AddClasses(classes => classes.AssignableTo<ISeeder>())
            .As<ISeeder>()
            .WithScopedLifetime());
        builder.Services.AddScoped<SeederRunner>();
        builder.Services.Configure<DemoSeedSettings>(builder.Configuration.GetSection(DemoSeedSettings.SectionName));

        builder.Services.AddStorage(builder.Configuration);
        builder.Services.AddScoped<IProductPricingService, ProductPricingService>();
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
        builder.Services.AddScoped<JwtUtils>();

        // *** IA (Previsão de demanda) — seção "Ai" (Provider, ApiKey, Model, TimeoutSeconds) ***
        builder.Services.AddHttpClient<IAiClient, GeminiAiClient>();

        builder.Services.Configure<ApiBehaviorOptions>(options =>
        {
            options.SuppressModelStateInvalidFilter = true;
        });

        // *** CONFIGURAÇÃO DO APP ***
        var app = builder.Build();

        // Primeiro middleware: os demais (HTTPS redirect, HSTS, logs) já enxergam o esquema e o IP reais
        app.UseForwardedHeaders();

        // *** MIGRATIONS E SEEDERS ***
        // Sem try/catch: se o banco ou o storage não estiverem prontos, a API não sobe (e o erro aparece no log)
        using (var scope = app.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
            await scope.ServiceProvider.GetRequiredService<SeederRunner>().RunAsync(CancellationToken.None);
        }



        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Nextflow API V1");
                c.RoutePrefix = string.Empty;
            });
        }
        else
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseCors();

        // *** CULTURA INVARIANTE ***
        // O model binding de form/query usa a cultura atual: num servidor pt-BR, "5.1" (multipart de produtos)
        // viraria 51. Fixa a cultura invariante por requisição — mesmo comportamento do container Linux.
        app.UseRequestLocalization(new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture(CultureInfo.InvariantCulture),
            SupportedCultures = [CultureInfo.InvariantCulture],
            SupportedUICultures = [CultureInfo.InvariantCulture]
        });

        app.UseRouting();

        // *** MIDDLEWARES ***
        app.UseMiddleware<GlobalExceptionMiddleware>();

        app.UseAuthentication();
        app.UseAuthorization();

        // *** MAPEAMENTO DE CONTROLLERS ***
        app.MapControllers();

        // *** MAPEAMENTO DE HEALTH CHECK ***
        app.MapHealthChecks("/health");

        // *** EXECUÇÃO DA APLICAÇÃO ***
        await app.RunAsync();
    }
}