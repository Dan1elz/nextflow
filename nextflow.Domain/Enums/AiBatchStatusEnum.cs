namespace Nextflow.Domain.Enums;

/// <summary>Situação de um lote enviado à IA (RN-DF20). Lote com falha cai para o cálculo.</summary>
public enum AiBatchStatusEnum : byte
{
    Pending = 1,
    Sent = 2,
    Completed = 3,
    Failed = 4,
}

public static class AiBatchStatusEnumExtensions
{
    public static string GetName(this AiBatchStatusEnum @enum) => Names[@enum];
    public static string GetApiName(this AiBatchStatusEnum @enum) => ApiNames[@enum];

    public static readonly Dictionary<AiBatchStatusEnum, string> Names = new()
    {
        { AiBatchStatusEnum.Pending, "Pendente" },
        { AiBatchStatusEnum.Sent, "Enviado" },
        { AiBatchStatusEnum.Completed, "Concluído" },
        { AiBatchStatusEnum.Failed, "Falhou" },
    };

    public static readonly Dictionary<AiBatchStatusEnum, string> ApiNames = new()
    {
        { AiBatchStatusEnum.Pending, "Pending" },
        { AiBatchStatusEnum.Sent, "Sent" },
        { AiBatchStatusEnum.Completed, "Completed" },
        { AiBatchStatusEnum.Failed, "Failed" },
    };
}
