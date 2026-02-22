using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Clients;

public class ReactivateClientUseCase(IClientRepository repository)
    : ReactivateUseCaseBase<Client, IClientRepository>(repository)
{ }
