using FluentValidation.Results;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Interfaces.Application.Services;

namespace Nextflow.Application.Services;

public class ResultFactory<T> : IResultFactory<T>
{
    public ResultDto<T> Failure(string error, int errorCode = 400) => ResultDto<T>.Failure(error, errorCode);
    public ResultDto<T> Failure(string error, T data, int errorCode = 400) => ResultDto<T>.Failure(error, data, errorCode);
    public ResultDto<T> Failure(IEnumerable<string> errors, int errorCode = 422) => ResultDto<T>.Failure(errors, errorCode);

    public ResultDto<T> Failure(ValidationResult validationResult, int errorCode = 422)
        => ResultDto<T>.Failure(
            validationResult.Errors
                .GroupBy(e => ToCamelCasePath(e.PropertyName))
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray()),
            errorCode);

    public ResultDto<T> FieldFailure(string field, string error, int errorCode = 422)
        => ResultDto<T>.Failure(new Dictionary<string, string[]> { [ToCamelCasePath(field)] = [error] }, errorCode);

    public ResultDto<T> Confirm(string warning)
        => ResultDto<T>.Failure(new Dictionary<string, string[]> { [ResultDto<T>.ConfirmKey] = [warning] }, 422);

    public ResultDto<T> Success(T data) => ResultDto<T>.Success(data);
    public ResultDto<T> Success() => ResultDto<T>.Success();

    /// <summary>"Items[0].UnitCost" → "items[0].unitCost": o front casa a chave com o nome do campo no formulário.</summary>
    internal static string ToCamelCasePath(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
            return string.Empty;

        return string.Join('.', propertyName.Split('.')
            .Select(segment => segment.Length == 0 ? segment : char.ToLowerInvariant(segment[0]) + segment[1..]));
    }
}
