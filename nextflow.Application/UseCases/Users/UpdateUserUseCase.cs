using FluentValidation;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Users;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Interfaces.Infrastructure.Services;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Users;

public class UpdateUserUseCase(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IValidator<UpdateUserRequestDto> validator,
    IResultFactory<UserResponseDto> resultFactory,
    IPasswordHasher passwordHasher)
    : UpdateUseCaseBase<User, IUserRepository, UpdateUserRequestDto, UserResponseDto>(
        userRepository, unitOfWork, validator, resultFactory)
{
    protected override async Task<ResultDto<UserResponseDto>?> ValidateBusinessRules(User entity, UpdateUserRequestDto dto, CancellationToken ct)
    {
        var email = User.NormalizeEmail(dto.Email);
        if (await _repository.Exists(u => u.Email == email && u.Id != entity.Id, ct))
            return _resultFactory.FieldFailure(nameof(dto.Email), "Já existe um usuário com este e-mail.");

        return null;
    }

    protected override void UpdateEntity(User entity, UpdateUserRequestDto request)
    {
        entity.Update(request);
        if (!string.IsNullOrEmpty(request.NewPassword))
            entity.ChangePassword(passwordHasher.Hash(request.NewPassword));
    }

    protected override UserResponseDto MapToResponseDto(User entity) => new(entity);
}
