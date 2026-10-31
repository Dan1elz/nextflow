using Nextflow.Domain.Enums;

namespace Nextflow.Domain.Interfaces.Domain.Models;

/// <summary>Registro que pertence a um cliente ou fornecedor (RN-CD05), com um principal por dono (decisão 4 de 29/10).</summary>
public interface IOwned
{
    OwnerTypeEnum OwnerType { get; }
    Guid OwnerId { get; }
    bool IsMain { get; }
    void SetMain(bool isMain);
}
