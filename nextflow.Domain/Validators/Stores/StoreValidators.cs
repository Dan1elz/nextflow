using FluentValidation;
using Nextflow.Domain.Dtos.Stores;
using Nextflow.Domain.Validators.Rules;

namespace Nextflow.Domain.Validators.Stores;

public class StoreRequestDtoValidator : AbstractValidator<StoreRequestDto>
{
    public StoreRequestDtoValidator()
    {
        RuleFor(x => x.Name).PersonName("nome");
    }
}

public class UpdateStoreRequestDtoValidator : AbstractValidator<UpdateStoreRequestDto>
{
    public UpdateStoreRequestDtoValidator()
    {
        RuleFor(x => x.Name).PersonName("nome");
        RuleFor(x => x.DefaultSaleLocationId).NotEmpty().WithMessage("Informe o local padrão de venda.");
        RuleFor(x => x.DefaultReceiveLocationId).NotEmpty().WithMessage("Informe o local padrão de recebimento.");
    }
}

public class UpdateLocationRequestDtoValidator : AbstractValidator<UpdateLocationRequestDto>
{
    public UpdateLocationRequestDtoValidator()
    {
        RuleFor(x => x.Name).PersonName("nome");
    }
}

public class LocationRequestDtoValidator : AbstractValidator<LocationRequestDto>
{
    public LocationRequestDtoValidator()
    {
        Include(new UpdateLocationRequestDtoValidator());
        RuleFor(x => x.StoreId).NotEmpty().WithMessage("Informe a loja.");
    }
}
