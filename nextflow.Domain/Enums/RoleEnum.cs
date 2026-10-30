namespace Nextflow.Domain.Enums;

/// <summary>Perfil do usuário. Só o Admin está em uso (ADR 0004, RN-AC04).</summary>
public enum RoleEnum : byte
{
    User = 1,
    Admin = 2,
}

public static class RoleEnumExtensions
{
    public static string GetName(this RoleEnum @enum) => Names[@enum];
    public static string GetApiName(this RoleEnum @enum) => ApiNames[@enum];

    public static readonly Dictionary<RoleEnum, string> Names = new()
    {
        { RoleEnum.User, "Usuário" },
        { RoleEnum.Admin, "Administrador" },
    };

    public static readonly Dictionary<RoleEnum, string> ApiNames = new()
    {
        { RoleEnum.User, "User" },
        { RoleEnum.Admin, "Admin" },
    };
}
