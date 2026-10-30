using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Users;

/// <summary>Inativa o usuário (soft delete). Não deixa a empresa sem nenhum Admin ativo (L-AC2).</summary>
public class DeleteUserUseCase(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IResultFactory<bool> resultFactory)
    : DeleteUseCaseBase<User, IUserRepository>(userRepository, unitOfWork, resultFactory)
{
    protected override async Task<ResultDto<bool>?> ValidateBusinessRules(User entity, Guid? userId, CancellationToken ct)
    {
        if (entity.Id == userId)
            return _resultFactory.Failure("Você não pode inativar o seu próprio usuário.", 400);

        if (entity.Role == RoleEnum.Admin
            && !await _repository.Exists(u => u.Id != entity.Id && u.IsActive && u.Role == RoleEnum.Admin, ct))
            return _resultFactory.Failure("Não é possível inativar o último administrador ativo.", 400);

        return null;
    }
}
