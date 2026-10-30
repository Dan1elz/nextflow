using FluentValidation;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Auth;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Application.UseCases;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Interfaces.Infrastructure.Services;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Auth;

/// <summary>Login (T-AC1): credencial inválida tem mensagem genérica (RN-AC03) e usuário inativo não entra (RN-AC02).</summary>
public class LoginUseCase(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IValidator<LoginRequestDto> validator,
    IResultFactory<LoginResponseDto> resultFactory,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IBusinessClock clock) : ILoginUseCase
{
    public const string InvalidCredentialsMessage = "E-mail ou senha inválidos.";
    public const string InactiveUserMessage = "Usuário inativo.";

    public async Task<ResultDto<LoginResponseDto>> Execute(LoginRequestDto dto, CancellationToken ct = default)
    {
        if (dto is null)
            return resultFactory.Failure("O corpo da requisição é obrigatório.", 400);

        var validationResult = await validator.ValidateAsync(dto, ct);
        if (!validationResult.IsValid)
            return resultFactory.Failure(validationResult, 422);

        var email = User.NormalizeEmail(dto.Email);
        var user = await userRepository.Get(u => u.Email == email, ct: ct);

        if (user is null || !passwordHasher.Verify(dto.Password, user.PasswordHash))
            return resultFactory.Failure(InvalidCredentialsMessage, 401);

        if (!user.IsActive)
            return resultFactory.Failure(InactiveUserMessage, 403);

        var tracked = await userRepository.GetByIdForUpdate(user.Id, ct: ct);
        tracked!.RegisterLogin(clock.UtcNow);
        await unitOfWork.CommitAsync(ct);

        var (token, expiresAt) = tokenService.GenerateAccessToken(tracked);
        return resultFactory.Success(new LoginResponseDto(token, expiresAt, new CurrentUserDto(tracked)));
    }
}
