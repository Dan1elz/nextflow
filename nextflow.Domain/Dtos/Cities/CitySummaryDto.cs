namespace Nextflow.Domain.Dtos.Cities;

/// <summary>Nome e UF da cidade, lidos por projeção para compor a resposta do endereço.</summary>
public record CitySummaryDto(string Name, string StateAcronym);
