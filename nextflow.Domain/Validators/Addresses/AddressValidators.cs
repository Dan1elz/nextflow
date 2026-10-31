using FluentValidation;
using Nextflow.Domain.Dtos.Addresses;
using Nextflow.Domain.Utils;

namespace Nextflow.Domain.Validators.Addresses;

public class UpdateAddressRequestDtoValidator : AbstractValidator<UpdateAddressRequestDto>
{
    public UpdateAddressRequestDtoValidator()
    {
        RuleFor(x => x.ZipCode).Must(z => Digits.Only(z).Length == 8).WithMessage("Informe um CEP com 8 dígitos.");
        RuleFor(x => x.Street)
            .NotEmpty().WithMessage("Informe a rua.")
            .MaximumLength(150).WithMessage("A rua deve ter no máximo 150 caracteres.");
        RuleFor(x => x.Number)
            .NotEmpty().WithMessage("Informe o número.")
            .MaximumLength(20).WithMessage("O número deve ter no máximo 20 caracteres.");
        RuleFor(x => x.Complement).MaximumLength(100).WithMessage("O complemento deve ter no máximo 100 caracteres.");
        RuleFor(x => x.Neighborhood)
            .NotEmpty().WithMessage("Informe o bairro.")
            .MaximumLength(100).WithMessage("O bairro deve ter no máximo 100 caracteres.");
        RuleFor(x => x.CityId).NotEmpty().WithMessage("Informe a cidade.");
    }
}

public class AddressRequestDtoValidator : AbstractValidator<AddressRequestDto>
{
    public AddressRequestDtoValidator()
    {
        Include(new UpdateAddressRequestDtoValidator());
        RuleFor(x => x.OwnerType).IsInEnum().WithMessage("Informe o tipo do dono: Client ou Supplier.");
        RuleFor(x => x.OwnerId).NotEmpty().WithMessage("Informe o dono do endereço.");
    }
}
