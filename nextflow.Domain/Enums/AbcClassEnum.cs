namespace Nextflow.Domain.Enums;

/// <summary>
/// Classe ABC do produto pela receita (RN-CE29): A = 80% da receita, B = os 15% seguintes, C = os 5% restantes.
/// Recalculada pela análise (F3); até lá o produto fica sem classe.
/// </summary>
public enum AbcClassEnum : byte
{
    A = 1,
    B = 2,
    C = 3,
}

public static class AbcClassEnumExtensions
{
    public static string GetName(this AbcClassEnum @enum) => Names[@enum];
    public static string GetApiName(this AbcClassEnum @enum) => ApiNames[@enum];

    public static readonly Dictionary<AbcClassEnum, string> Names = new()
    {
        { AbcClassEnum.A, "Classe A" },
        { AbcClassEnum.B, "Classe B" },
        { AbcClassEnum.C, "Classe C" },
    };

    public static readonly Dictionary<AbcClassEnum, string> ApiNames = new()
    {
        { AbcClassEnum.A, "A" },
        { AbcClassEnum.B, "B" },
        { AbcClassEnum.C, "C" },
    };
}
