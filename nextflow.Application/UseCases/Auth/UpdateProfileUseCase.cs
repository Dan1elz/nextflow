using FluentValidation;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Auth;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Application.UseCases;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Auth;

/// <summary>Meu perfil (T-AC4): o usuário logado altera nome, sobrenome e e-mail (único, RN-AC01).</summary>
public class UpdateProfileUseCase(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IValidator<UpdateProfileRequestDto> validator,
    IResultFactory<CurrentUserDto> resultFactory) : IUpdateProfileUseCase
{
    public async Task<ResultDto<CurrentUserDto>> Execute(Guid userId, UpdateProfileRequestDto dto, CancellationToken ct = default)
    {
        if (dto is null)
            return resultFactory.Failure("O corpo da requisição é obrigatório.", 400);

        var validationResult = await validator.ValidateAsync(dto, ct);
        if (!validationResult.IsValid)
            return resultFactory.Failure(validationResult, 422);

        var user = await userRepository.GetByIdForUpdate(userId, ct: ct);
        if (user is null)
            return resultFactory.Failure("Usuário não encontrado.", 404);

        var email = User.NormalizeEmail(dto.Email);
        if (await userRepository.Exists(u => u.Email == email && u.Id != userId, ct))
            return resultFactory.FieldFailure(nameof(dto.Email), "Já existe um usuário com este e-mail.");

        user.UpdateProfile(dto);
        await unitOfWork.CommitAsync(ct);

        return resultFactory.Success(new CurrentUserDto(user));
    }
}
