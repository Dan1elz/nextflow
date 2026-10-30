using FluentValidation;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Users;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Interfaces.Infrastructure.Services;
using Nextflow.Domain.Models;
using Nextflow.Domain.ValueObjects;

namespace Nextflow.Application.UseCases.Users;

public class CreateUserUseCase(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IValidator<UserRequestDto> validator,
    IResultFactory<UserResponseDto> resultFactory,
    IPasswordHasher passwordHasher)
    : CreateUseCaseBase<User, IUserRepository, UserRequestDto, UserResponseDto>(
        userRepository, unitOfWork, validator, resultFactory)
{
    /// <summary>E-mail e CPF únicos (RN-AC01): erro no campo, para o formulário marcar o lugar certo.</summary>
    protected override async Task<ResultDto<UserResponseDto>?> ValidateBusinessRules(UserRequestDto dto, CancellationToken ct)
    {
        var email = User.NormalizeEmail(dto.Email);
        if (await _repository.Exists(u => u.Email == email, ct))
            return _resultFactory.FieldFailure(nameof(dto.Email), "Já existe um usuário com este e-mail.");

        var cpf = TaxDocument.ParseCpf(dto.Cpf);
        if (await _repository.Exists(u => u.Cpf == cpf, ct))
            return _resultFactory.FieldFailure(nameof(dto.Cpf), "Já existe um usuário com este CPF.");

        return null;
    }

    protected override User MapToEntity(UserRequestDto dto) => new(dto, passwordHasher.Hash(dto.Password));

    protected override UserResponseDto MapToResponseDto(User entity) => new(entity);
}
