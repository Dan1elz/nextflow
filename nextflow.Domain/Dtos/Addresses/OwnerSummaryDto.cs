namespace Nextflow.Domain.Dtos.Addresses;

/// <summary>
/// O que as listagens e o combo de cliente mostram do dono (T-CD1, T-CD3). <see cref="MainPhone"/> só vem do contato
/// principal do tipo telefone ou WhatsApp: com zero principal ou principal e-mail fica nulo (RN-CD06).
/// </summary>
public record OwnerSummaryDto(string? CityState, string? MainContact, string? MainPhone);
