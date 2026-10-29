using Microsoft.EntityFrameworkCore;
using Nextflow.Application;
using Nextflow.Extensions;
using Nextflow.Infrastructure;
using Nextflow.Infrastructure.Database;
using Nextflow.Infrastructure.Seeders;
using Nextflow.Middlewares;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, config) =>
        config.ReadFrom.Configuration(context.Configuration)
              .ReadFrom.Services(services));

    builder.Services
        .AddInfrastructure(builder.Configuration)
        .AddApplication(builder.Configuration)
        .AddPresentation(builder.Configuration, builder.Environment);

    var app = builder.Build();

    app.UseForwardedHeaders();

    // Migrations são donas do schema e rodam no startup, seguidas dos seeders (idempotentes).
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        try
        {
            if (!await dbContext.Database.CanConnectAsync())
            {
                Log.Error("[DATABASE] Banco inacessível na inicialização: migrations e seeders não foram executados");
            }
            else
            {
                await dbContext.Database.MigrateAsync();
                await scope.ServiceProvider.GetRequiredService<SeederRunner>().RunAsync(CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "[DATABASE] Erro fatal na inicialização");
            throw;
        }
    }

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Nextflow API V1"));
    }

    app.UseCors();
    app.UseRouting();

    app.UseSerilogRequestLogging();

    app.UseMiddleware<GlobalResultMiddleware>();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    app.Run();
}
// HostAbortedException é como o `dotnet ef` interrompe o host de propósito ao ler o modelo — não é falha.
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Aplicação encerrada inesperadamente");
    Environment.ExitCode = 1;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
