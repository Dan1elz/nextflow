using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Users;

public class ReactivateUserUseCase(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IResultFactory<bool> resultFactory)
    : ReactivateUseCaseBase<User, IUserRepository>(userRepository, unitOfWork, resultFactory);
