using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Auth;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Application.UseCases;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;

namespace Nextflow.Application.UseCases.Auth;

public class GetCurrentUserUseCase(
    IUserRepository userRepository,
    IResultFactory<CurrentUserDto> resultFactory) : IGetCurrentUserUseCase
{
    public async Task<ResultDto<CurrentUserDto>> Execute(Guid userId, CancellationToken ct = default)
    {
        var user = await userRepository.GetById(userId, ct: ct);

        if (user is null)
            return resultFactory.Failure("Usuário não encontrado.", 404);

        if (!user.IsActive)
            return resultFactory.Failure(LoginUseCase.InactiveUserMessage, 403);

        return resultFactory.Success(new CurrentUserDto(user));
    }
}
