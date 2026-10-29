namespace Nextflow.Domain.Dtos;

/// <summary>
/// Envelope de toda resposta que sai da Application. Criado só pela <c>IResultFactory</c>.
/// <see cref="FieldErrors"/> é a extensão do Nextflow ao padrão do atrena (ADR 0010): erros de validação por campo
/// (camelCase) e o aviso que pede confirmação (<see cref="ConfirmKey"/>, RN-VD13).
/// </summary>
public class ResultDto<T>
{
    /// <summary>Chave de <see cref="FieldErrors"/> usada pelo aviso que pede confirmação.</summary>
    public const string ConfirmKey = "confirm";

    private ResultDto() { }

    public T? Data { get; private set; }

    public bool IsSuccess { get; protected set; }
    public List<string> Errors { get; protected set; } = [];
    public Dictionary<string, string[]>? FieldErrors { get; protected set; }
    public int ErrorCode { get; protected set; } = 400;

    public string Message => Errors.Count != 0 ? string.Join(" | ", Errors) : string.Empty;

    public static ResultDto<T> Failure(string error, int errorCode = 400)
        => new()
        {
            IsSuccess = false,
            Errors = [error],
            ErrorCode = errorCode
        };

    public static ResultDto<T> Failure(string error, T data, int errorCode = 400)
        => new()
        {
            IsSuccess = false,
            Errors = [error],
            ErrorCode = errorCode,
            Data = data
        };

    public static ResultDto<T> Failure(IEnumerable<string> errors, int errorCode = 422)
        => new()
        {
            IsSuccess = false,
            Errors = errors.ToList(),
            ErrorCode = errorCode
        };

    public static ResultDto<T> Failure(Dictionary<string, string[]> fieldErrors, int errorCode = 422)
        => new()
        {
            IsSuccess = false,
            Errors = [.. fieldErrors.Values.SelectMany(messages => messages).Distinct()],
            FieldErrors = fieldErrors,
            ErrorCode = errorCode
        };

    public static ResultDto<T> Success(T data)
        => new()
        {
            IsSuccess = true,
            Data = data
        };

    public static ResultDto<T> Success()
        => new()
        {
            IsSuccess = true,
            Data = default
        };

    public static implicit operator ResultDto<T>(T value) => Success(value);
}
