using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Interfaces.Infrastructure.Repositories;

public interface IContactRepository : IBaseRepository<Contact>
{
    /// <summary>Contatos dos donos da lista, principal primeiro: o dono não tem navegação para o contato.</summary>
    Task<List<Contact>> GetByOwners(OwnerTypeEnum ownerType, IReadOnlyCollection<Guid> ownerIds, CancellationToken ct = default);

    /// <summary>Donos com telefone ou WhatsApp que contém os dígitos (busca de T-CD3).</summary>
    Task<List<Guid>> GetOwnerIdsByPhone(OwnerTypeEnum ownerType, string digits, CancellationToken ct = default);
}
