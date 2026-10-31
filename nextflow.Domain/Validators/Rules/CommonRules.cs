using FluentValidation;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.ValueObjects;

namespace Nextflow.Domain.Validators.Rules;

/// <summary>Regras de formato reaproveitadas por mais de um validator.</summary>
public static class CommonRules
{
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 100;

    /// <summary>Senha forte (L-AC3): mínimo de 8 caracteres, com ao menos uma letra e um número.</summary>
    public static IRuleBuilderOptions<T, string?> StrongPassword<T>(this IRuleBuilder<T, string?> rule)
        => rule
            .NotEmpty().WithMessage("Informe a senha.")
            .MinimumLength(PasswordMinLength).WithMessage($"A senha deve ter ao menos {PasswordMinLength} caracteres.")
            .MaximumLength(PasswordMaxLength).WithMessage($"A senha deve ter no máximo {PasswordMaxLength} caracteres.")
            .Must(p => p != null && p.Any(char.IsLetter)).WithMessage("A senha precisa ter ao menos uma letra.")
            .Must(p => p != null && p.Any(char.IsDigit)).WithMessage("A senha precisa ter ao menos um número.");

    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule)
        => rule
            .NotEmpty().WithMessage("Informe o e-mail.")
            .MaximumLength(150).WithMessage("O e-mail deve ter no máximo 150 caracteres.")
            .EmailAddress().WithMessage("Informe um e-mail válido.");

    public static IRuleBuilderOptions<T, string> PersonName<T>(this IRuleBuilder<T, string> rule, string label)
        => rule
            .NotEmpty().WithMessage($"Informe o {label}.")
            .MaximumLength(100).WithMessage($"O {label} deve ter no máximo 100 caracteres.");

    /// <summary>Data de nascimento no passado, pela data de negócio do momento da validação (não a do startup).</summary>
    public static IRuleBuilderOptions<T, DateOnly> PastBirthDate<T>(this IRuleBuilder<T, DateOnly> rule, IBusinessClock clock)
        => rule
            .NotEmpty().WithMessage("Informe a data de nascimento.")
            .Must(date => date < clock.Today).WithMessage("A data de nascimento precisa estar no passado.");

    public static IRuleBuilderOptions<T, string?> ValidCpf<T>(this IRuleBuilder<T, string?> rule)
        => rule.Must(TaxDocument.IsValidCpf).WithMessage("Informe um CPF válido.");

    /// <summary>Telefone com DDD: 10 ou 11 dígitos, com ou sem máscara.</summary>
    public static IRuleBuilderOptions<T, string> ValidPhone<T>(this IRuleBuilder<T, string> rule)
        => rule.Must(p => Utils.Digits.Only(p).Length is 10 or 11).WithMessage("Informe um telefone com DDD.");

    public static IRuleBuilderOptions<T, string?> ValidCnpj<T>(this IRuleBuilder<T, string?> rule)
        => rule.Must(TaxDocument.IsValidCnpj).WithMessage("Informe um CNPJ válido.");
}
