using Nextflow.Application.Filters;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Domain.Models;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Registry;

/// <summary>Regras comuns de endereço e contato, que pertencem a um dono (RN-CD05, decisão 4 de 29/10).</summary>
internal static class OwnerRules
{
    /// <summary>
    /// Dono existente (404) e ativo (400): endereço e contato só entram ou mudam em dono ativo (RN-CD05, RN-CD10).
    /// </summary>
    public static async Task<ResultDto<T>?> CheckOwner<T>(
        IOwnerDirectory ownerDirectory, IResultFactory<T> resultFactory, OwnerTypeEnum ownerType, Guid ownerId, CancellationToken ct)
    {
        if (!await ownerDirectory.Exists(ownerType, ownerId, ct))
            return resultFactory.Failure($"{ownerType.GetName()} não encontrado.", 404);

        return await ownerDirectory.IsActive(ownerType, ownerId, ct)
            ? null
            : resultFactory.Failure($"{ownerType.GetName()} está inativo e não pode receber nem alterar endereços e contatos.", 400);
    }

    /// <summary>
    /// No máximo um principal por dono (RN-CD06), ao criar: marcar desmarca os outros, e o primeiro registro do dono já
    /// nasce principal. Não faz commit.
    /// </summary>
    public static async Task ApplyMainFlagOnCreate<T>(IBaseRepository<T> repository, T entity, CancellationToken ct)
        where T : BaseModel, IOwned
    {
        var siblings = await repository.GetAllForUpdate(Siblings(entity), ct);

        if (entity.IsMain)
        {
            foreach (var sibling in siblings)
                sibling.SetMain(false);
        }
        else if (siblings.Count == 0)
        {
            entity.SetMain(true);
        }
    }

    /// <summary>
    /// No máximo um principal por dono (RN-CD06), ao editar: marcar desmarca os outros. Desmarcar não promove nenhum,
    /// nem quando o registro é o único do dono. Não faz commit.
    /// </summary>
    public static async Task ApplyMainFlagOnUpdate<T>(IBaseRepository<T> repository, T entity, CancellationToken ct)
        where T : BaseModel, IOwned
    {
        if (!entity.IsMain)
            return;

        foreach (var sibling in await repository.GetAllForUpdate(Siblings(entity), ct))
            sibling.SetMain(false);
    }

    /// <summary><c>ownerType</c> e <c>ownerId</c> válidos são obrigatórios na listagem (decisão 4 de 29/10).</summary>
    public static bool TryGetOwner(IReadOnlyDictionary<string, string>? filters, out OwnerTypeEnum ownerType, out Guid ownerId)
    {
        ownerType = default;
        ownerId = default;
        var set = new FilterSet(filters);

        return set.TryGetString("ownerType", out var type)
            && Enum.TryParse(type, ignoreCase: true, out ownerType)
            && Enum.IsDefined(ownerType)
            && set.TryGetString("ownerId", out var id)
            && Guid.TryParse(id, out ownerId);
    }

    private static System.Linq.Expressions.Expression<Func<T, bool>> Siblings<T>(T entity) where T : BaseModel, IOwned
    {
        var (ownerType, ownerId, id) = (entity.OwnerType, entity.OwnerId, entity.Id);
        return e => e.OwnerType == ownerType && e.OwnerId == ownerId && e.Id != id;
    }
}
