using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos.Users;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Users;

public class GetUserByIdUseCase(
    IUserRepository userRepository,
    IResultFactory<UserResponseDto> resultFactory)
    : GetByIdUseCaseBase<User, IUserRepository, UserResponseDto>(userRepository, resultFactory)
{
    protected override UserResponseDto MapToResponseDto(User entity) => new(entity);
}
