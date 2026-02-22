using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Models;

namespace nextflow.Application.UseCases.Suppliers;
public class ReactivateSupplierUseCase(ISupplierRepository repository)
    : ReactivateUseCaseBase<Supplier, ISupplierRepository>(repository)
{ }
