using FluentValidation;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Auth;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Application.UseCases;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Interfaces.Infrastructure.Services;

namespace Nextflow.Application.UseCases.Auth;

/// <summary>Troca da própria senha (T-AC4): exige a senha atual e aplica a política de senha forte (L-AC3).</summary>
public class ChangePasswordUseCase(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IValidator<ChangePasswordRequestDto> validator,
    IResultFactory<bool> resultFactory,
    IPasswordHasher passwordHasher) : IChangePasswordUseCase
{
    public async Task<ResultDto<bool>> Execute(Guid userId, ChangePasswordRequestDto dto, CancellationToken ct = default)
    {
        if (dto is null)
            return resultFactory.Failure("O corpo da requisição é obrigatório.", 400);

        var validationResult = await validator.ValidateAsync(dto, ct);
        if (!validationResult.IsValid)
            return resultFactory.Failure(validationResult, 422);

        var user = await userRepository.GetByIdForUpdate(userId, ct: ct);
        if (user is null)
            return resultFactory.Failure("Usuário não encontrado.", 404);

        if (!passwordHasher.Verify(dto.CurrentPassword, user.PasswordHash))
            return resultFactory.FieldFailure(nameof(dto.CurrentPassword), "Senha atual incorreta.");

        user.ChangePassword(passwordHasher.Hash(dto.NewPassword));
        await unitOfWork.CommitAsync(ct);

        return resultFactory.Success(true);
    }
}
