namespace Nextflow.Domain.Dtos.Clients;

/// <summary>Cadastro rápido sem sair da venda (T-CD4): nome, telefone e, se o cliente quiser, e-mail.</summary>
public class QuickCreateClientRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    /// <summary>Opcional: vira um contato do tipo e-mail (não principal), para o aviso ao cliente fora da loja.</summary>
    public string? Email { get; set; }
}
