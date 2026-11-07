namespace Nextflow.Infrastructure.Seeders.Demo;

/// <summary>
/// Seção <c>Seed:Demo</c> (env: <c>Seed__Demo__Enabled</c>, <c>Seed__Demo__AnchorDate</c>).
/// Desligado por padrão: dados de demonstração nunca entram num banco por acidente.
/// </summary>
public sealed class DemoSeedSettings
{
    public const string SectionName = "Seed:Demo";

    public bool Enabled { get; set; }

    /// <summary>
    /// "Hoje" da simulação (yyyy-MM-dd). Vazio = data atual no fuso de negócio.
    /// Fixar a data deixa o seed reproduzível: mesma âncora ⇒ mesmos dados (Random com semente fixa).
    /// </summary>
    public DateOnly? AnchorDate { get; set; }
}
