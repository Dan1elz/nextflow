using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Auth;

namespace Nextflow.Domain.Interfaces.Application.UseCases;

public interface ILoginUseCase
{
    Task<ResultDto<LoginResponseDto>> Execute(LoginRequestDto dto, CancellationToken ct = default);
}

public interface IGetCurrentUserUseCase
{
    Task<ResultDto<CurrentUserDto>> Execute(Guid userId, CancellationToken ct = default);
}

public interface IUpdateProfileUseCase
{
    Task<ResultDto<CurrentUserDto>> Execute(Guid userId, UpdateProfileRequestDto dto, CancellationToken ct = default);
}

public interface IChangePasswordUseCase
{
    Task<ResultDto<bool>> Execute(Guid userId, ChangePasswordRequestDto dto, CancellationToken ct = default);
}
