namespace Nextflow.Infrastructure.Seeders;

/// <summary>
/// Dados iniciais aplicados no startup, depois das migrations.
/// Toda classe que implementa esta interface é registrada automaticamente (Program.cs) e executada pelo <see cref="SeederRunner"/>.
/// Cada seeder deve ser idempotente: rodar de novo não pode duplicar dados.
/// </summary>
public interface ISeeder
{
    /// <summary>Ordem de execução (menor primeiro). Use dezenas para sobrar espaço entre os seeders.</summary>
    int Order { get; }

    Task SeedAsync(CancellationToken ct);
}
