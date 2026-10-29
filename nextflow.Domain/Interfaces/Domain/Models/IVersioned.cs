namespace Nextflow.Domain.Interfaces.Domain.Models;

/// <summary>
/// Documento editável com token de concorrência (coluna de sistema <c>xmin</c> do Postgres). O update envia a
/// <see cref="Version"/> lida; se mudou no meio, o EF lança <c>DbUpdateConcurrencyException</c> e o middleware devolve 409.
/// </summary>
public interface IVersioned
{
    uint Version { get; }
}
