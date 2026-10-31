using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Users;

/// <summary>Reativa o usuário e vincula as lojas criadas enquanto ele estava inativo (decisão 6 de 29/10).</summary>
public class ReactivateUserUseCase(
    IUserRepository userRepository,
    IStoreMembership storeMembership,
    IUnitOfWork unitOfWork,
    IResultFactory<bool> resultFactory)
    : ReactivateUseCaseBase<User, IUserRepository>(userRepository, unitOfWork, resultFactory)
{
    protected override Task BeforePersistence(User entity, Guid? userId, CancellationToken ct)
        => storeMembership.LinkUser(entity.Id, ct);
}
