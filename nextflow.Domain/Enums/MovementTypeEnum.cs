namespace Nextflow.Domain.Enums;

/// <summary>Sentido da movimentação de estoque (contrato, enums do módulo). A quantidade gravada leva o sinal: entrada positiva, saída negativa.</summary>
public enum MovementTypeEnum : byte
{
    In = 1,
    Out = 2,
}

public static class MovementTypeEnumExtensions
{
    public static string GetName(this MovementTypeEnum @enum) => Names[@enum];
    public static string GetApiName(this MovementTypeEnum @enum) => ApiNames[@enum];

    public static readonly Dictionary<MovementTypeEnum, string> Names = new()
    {
        { MovementTypeEnum.In, "Entrada" },
        { MovementTypeEnum.Out, "Saída" },
    };

    public static readonly Dictionary<MovementTypeEnum, string> ApiNames = new()
    {
        { MovementTypeEnum.In, "In" },
        { MovementTypeEnum.Out, "Out" },
    };
}
