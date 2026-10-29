using FluentValidation.Results;
using Nextflow.Domain.Dtos;

namespace Nextflow.Domain.Interfaces.Application.Services;

public interface IResultFactory<T>
{
    ResultDto<T> Failure(string error, int errorCode = 400);
    ResultDto<T> Failure(string error, T data, int errorCode = 400);
    ResultDto<T> Failure(IEnumerable<string> errors, int errorCode = 422);

    /// <summary>Falha de validação por campo: agrupa os erros do FluentValidation pelo nome do campo em camelCase.</summary>
    ResultDto<T> Failure(ValidationResult validationResult, int errorCode = 422);

    /// <summary>Erro de negócio ligado a um campo do formulário (ex.: "E-mail já cadastrado" em <c>email</c>).</summary>
    ResultDto<T> FieldFailure(string field, string error, int errorCode = 422);

    /// <summary>Aviso que pede confirmação (RN-VD13): 422 com <c>fieldErrors.confirm</c>; o front reenvia com <c>confirmWarnings</c>.</summary>
    ResultDto<T> Confirm(string warning);

    ResultDto<T> Success(T data);
    ResultDto<T> Success();
}
